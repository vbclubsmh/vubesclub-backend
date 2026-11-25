using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Booking;
using VubesClub.Api.Persistence;
using BookingEntity = VubesClub.Api.Persistence.BookingRow;

namespace VubesClub.Api.Modules.Payments;

public sealed class PaymentService(ClubDb db, BookingService bookings, IPaymentGateway gateway, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<(Payment Payment, bool Created)> Start(Guid holdId, StartPayment input, string key, CancellationToken ct)
    {
        var owner = await bookings.Owned(holdId, ct);
        if (!gateway.IsConfigured) throw UnconfiguredGateway.Unavailable();
        if (!input.TermsAccepted || input.AcceptedTermsVersion != owner.TermsVersion)
            throw ApiFault.Invalid("Güncel katılım koşullarını onaylayın.");

        PaymentRow payment;
        bool created;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.LockAsync($"payment-key:{owner.MemberId}:{key}", ct);
            await db.LockAsync(BookingService.InventoryKey(owner.EventId), ct);
            var fingerprint = $"{holdId:D}/{input.AcceptedTermsVersion}";
            var existingKey = await bookings.Lookup("payment", key, fingerprint, ct);
            var hold = await db.Set<HoldRow>().SingleAsync(h => h.Id == holdId, ct);
            var current = await db.Set<PaymentRow>().SingleOrDefaultAsync(p => p.HoldId == holdId, ct);
            created = current == null;

            if (current == null)
            {
                if (hold.State != "reserved" || hold.Expires <= Now)
                    throw new ApiFault(410, "HOLD_EXPIRED", "Ödeme süresi doldu.");
                payment = new PaymentRow { HoldId = holdId, Provider = gateway.Name,
                    Amount = hold.Amount, Currency = hold.Currency, Status = "processing" };
                db.Add(payment);
                hold.AcceptedAt = Now;
                hold.State = "paymentPending";
                hold.VerifyUntil = hold.Expires.AddMinutes(5);
            }
            else payment = current;

            if (existingKey == null) bookings.Remember("payment", key, fingerprint, payment.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        if (payment.Reference == null && payment.Status == "processing" && owner.Expires > Now)
        {
            // A crash between the remote call and local save is recoverable using the same merchant order ID.
            var checkout = await gateway.StartAsync(Charge(payment, owner), ct);
            if (checkout.Redirect != null && (!checkout.Redirect.IsAbsoluteUri || checkout.Redirect.Scheme != "https"))
                throw new ApiFault(502, "INVALID_CHECKOUT_URL", "Ödeme sağlayıcısı geçersiz adres döndürdü.");

            if (checkout.Settlement is { } settlement)
            {
                if (settlement.PaymentId != payment.Id || settlement.Reference != checkout.Reference)
                    throw ApiFault.Conflict("PAYMENT_MISMATCH", "Ödeme kaydı doğrulanamadı.");

                await Apply(settlement, ct);
            }
            else
            {
                string? redirect = checkout.Redirect?.AbsoluteUri;
                var status = redirect == null ? "processing" : "requiresAction";
                await db.Set<PaymentRow>().Where(p => p.Id == payment.Id && p.Status == "processing" && p.Reference == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(p => p.Reference, checkout.Reference)
                        .SetProperty(p => p.Redirect, redirect)
                        .SetProperty(p => p.Status, status), ct);
            }
        }

        var saved = await db.Set<PaymentRow>().AsNoTracking().SingleAsync(p => p.Id == payment.Id, ct);
        return (new Payment(saved.Id, holdId, saved.Status,
            saved.Status == "requiresAction" && saved.Redirect != null && owner.Expires > Now
                ? new PaymentAction("redirect", saved.Redirect) : null), created);
    }

    public async Task ReconcileOwned(Guid holdId, CancellationToken ct)
    {
        var hold = await bookings.Owned(holdId, ct);
        if (!gateway.IsConfigured) return;
        var payment = await db.Set<PaymentRow>().AsNoTracking().SingleOrDefaultAsync(p => p.HoldId == holdId, ct);
        if (payment == null) return;
        if (payment.Status == "refundPending")
        {
            await CompleteRefund(payment.Id, ct);
            return;
        }
        if (payment.Status is "processing" or "requiresAction")
        {
            var update = await gateway.FindAsync(payment.Id, ct);
            if (update != null && update.PaymentId == payment.Id) await Apply(update, ct);
        }
    }

    public async Task Apply(VerifiedSettlement notice, CancellationToken ct)
    {
        if (!gateway.IsConfigured) throw UnconfiguredGateway.Unavailable();
        var candidate = await (from p in db.Set<PaymentRow>() join h in db.Set<HoldRow>() on p.HoldId equals h.Id
            where p.Id == notice.PaymentId select new { h.EventId }).SingleOrDefaultAsync(ct) ?? throw ApiFault.Missing();

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.LockAsync(BookingService.InventoryKey(candidate.EventId), ct);
            var payment = await db.Set<PaymentRow>().SingleAsync(p => p.Id == notice.PaymentId, ct);
            await db.Entry(payment).ReloadAsync(ct);
            var hold = await db.Set<HoldRow>().SingleAsync(h => h.Id == payment.HoldId, ct);
            await db.Entry(hold).ReloadAsync(ct);
            if (payment.Provider != gateway.Name || notice.AmountMinor != payment.Amount ||
                notice.Currency != payment.Currency || string.IsNullOrWhiteSpace(notice.ExternalEventId) ||
                string.IsNullOrWhiteSpace(notice.Reference) || payment.Reference != null && payment.Reference != notice.Reference)
                throw ApiFault.Conflict("PAYMENT_MISMATCH", "Ödeme kaydı doğrulanamadı.");

            var seen = await db.Set<PaymentNotice>().AsNoTracking().SingleOrDefaultAsync(n =>
                n.Provider == gateway.Name && n.ExternalId == notice.ExternalEventId, ct);
            if (seen != null && seen.PaymentId != payment.Id)
                throw ApiFault.Conflict("NOTICE_CONFLICT", "Bildirim başka bir ödemeye ait.");
            if (seen == null)
            {
                db.Add(new PaymentNotice { Provider = gateway.Name, ExternalId = notice.ExternalEventId,
                    PaymentId = payment.Id, Received = Now });
                payment.Reference ??= notice.Reference;
                await Transition(payment, hold, notice, ct);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        await CompleteRefund(notice.PaymentId, ct);
    }

    private async Task Transition(PaymentRow payment, HoldRow hold, VerifiedSettlement notice, CancellationToken ct)
    {
        if (payment.Status == "refundPending" && notice.Outcome == SettlementOutcome.Refunded)
        {
            var refund = await db.Set<RefundRow>().SingleAsync(r => r.PaymentId == payment.Id, ct);
            refund.Status = hold.State = payment.Status = "refunded";
            return;
        }
        if (payment.Status is "succeeded" or "refundPending" or "refunded") return;
        if (notice.Outcome == SettlementOutcome.Failed)
        {
            payment.Status = "failed";
            hold.State = "paymentFailed";
            return;
        }
        if (notice.Outcome != SettlementOutcome.Paid) return;
        if (notice.PaidAt is not DateTime paidAt || paidAt.Kind != DateTimeKind.Utc)
            throw ApiFault.Invalid("Doğrulanmış ödeme zamanı gerekli.");

        payment.PaidAt = paidAt;
        var e = await db.Set<ClubEvent>().SingleAsync(e => e.Id == hold.EventId, ct);
        var alreadyBooked = await db.Set<BookingEntity>().AnyAsync(b => b.MemberId == hold.MemberId &&
            b.EventId == e.Id && b.Status == "confirmed", ct);
        if (paidAt > hold.Expires || e.Cancelled || alreadyBooked ||
            await BookingService.Occupied(db, e.Id, Now, hold.Id, ct) >= e.Capacity)
        {
            hold.State = payment.Status = "refundPending";
            db.Add(new RefundRow { PaymentId = payment.Id, Reason = paidAt > hold.Expires ? "late" : "unavailable", Status = "pending" });
            return;
        }

        var booking = new BookingEntity { MemberId = hold.MemberId, EventId = e.Id, VersionId = hold.VersionId,
            PaymentId = payment.Id, Status = "confirmed", Attendance = "unrecorded" };
        db.Add(booking);
        await db.SaveChangesAsync(ct);
        db.Add(new PassRow { BookingId = booking.Id, Nonce = Secrets.RandomToken() });
        payment.Status = "succeeded";
        hold.State = "confirmed";
    }

    private async Task CompleteRefund(Guid paymentId, CancellationToken ct)
    {
        var refund = await db.Set<RefundRow>().AsNoTracking().SingleOrDefaultAsync(r => r.PaymentId == paymentId && r.Status == "pending", ct);
        if (refund == null) return;
        var payment = await db.Set<PaymentRow>().AsNoTracking().SingleAsync(p => p.Id == paymentId, ct);
        var hold = await db.Set<HoldRow>().AsNoTracking().SingleAsync(h => h.Id == payment.HoldId, ct);
        if (!await gateway.RefundAsync(refund.Id, Charge(payment, hold), payment.Reference!, ct)) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(BookingService.InventoryKey(hold.EventId), ct);
        await db.Set<RefundRow>().Where(r => r.Id == refund.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "refunded"), ct);
        await db.Set<PaymentRow>().Where(p => p.Id == paymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "refunded"), ct);
        await db.Set<HoldRow>().Where(h => h.Id == hold.Id).ExecuteUpdateAsync(s => s.SetProperty(h => h.State, "refunded"), ct);
        await tx.CommitAsync(ct);
    }

    private static EventCharge Charge(PaymentRow payment, HoldRow hold) =>
        new(payment.Id, hold.EventId, payment.Amount, payment.Currency, hold.Expires);
}
