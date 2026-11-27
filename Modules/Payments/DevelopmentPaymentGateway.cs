using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Payments;

// Development checkout simulates a successful payment when the member accepts the order.
// Its receipt can be reconstructed from the persisted order after a restart.
public class DevelopmentPaymentGateway(ClubDb db) : IPaymentGateway
{
    public virtual string Name => "development";
    public bool IsConfigured => true;

    public async Task<CheckoutSession> StartAsync(EventCharge charge, CancellationToken ct)
    {
        var settlement = await FindAsync(charge.PaymentId, ct) ?? throw ApiFault.Missing();
        return new CheckoutSession(settlement.Reference, null, settlement);
    }

    public async Task<VerifiedSettlement?> FindAsync(Guid paymentId, CancellationToken ct)
    {
        var order = await (
            from payment in db.Set<PaymentRow>().AsNoTracking()
            join hold in db.Set<HoldRow>() on payment.HoldId equals hold.Id
            where payment.Id == paymentId && payment.Provider == Name && hold.AcceptedAt != null
            select new { payment.Amount, payment.Currency, hold.AcceptedAt }
        ).SingleOrDefaultAsync(ct);

        if (order == null) return null;

        return new VerifiedSettlement(paymentId, Reference(paymentId), $"development-paid-{paymentId:N}",
            order.Amount, order.Currency, SettlementOutcome.Paid, order.AcceptedAt);
    }

    // The test provider uses server-side reconciliation only.
    public Task<VerifiedSettlement> VerifyNotificationAsync(HttpRequest request, CancellationToken ct) =>
        throw ApiFault.Missing();

    public Task<bool> RefundAsync(Guid refundId, EventCharge charge, string reference, CancellationToken ct) =>
        Task.FromResult(reference == Reference(charge.PaymentId));

    private static string Reference(Guid paymentId) => $"development-{paymentId:N}";
}
