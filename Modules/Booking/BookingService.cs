using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Persistence;
using BookingEntity = VubesClub.Api.Persistence.BookingRow;

namespace VubesClub.Api.Modules.Booking;

public sealed class BookingService(ClubDb db, Caller caller, CatalogQueries catalog, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    internal static string InventoryKey(Guid eventId) => "inventory:" + eventId;

    internal static async Task<int> Occupied(ClubDb db, Guid eventId, DateTime now, Guid exceptHold, CancellationToken ct)
    {
        var confirmed = await db.Set<BookingEntity>().CountAsync(b => b.EventId == eventId && b.Status == "confirmed", ct);
        var reserved = await db.Set<HoldRow>().CountAsync(h => h.EventId == eventId && h.Id != exceptHold &&
            (h.State == "reserved" && h.Expires > now || h.State == "paymentPending" && h.VerifyUntil > now), ct);
        return confirmed + reserved;
    }

    public async Task<(Hold Hold, bool Created)> Create(CreateHold input, string key, CancellationToken ct)
    {
        var fingerprint = $"{input.EventId:D}/{input.ProgramVersion}";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync($"request:{caller.MemberId}:hold:{key}", ct);
        var previous = await Lookup("hold", key, fingerprint, ct);
        if (previous != null) return (await Read(previous.Value, ct), false);

        await db.LockAsync(InventoryKey(input.EventId), ct);
        var now = Now;
        var e = await db.Set<ClubEvent>().SingleOrDefaultAsync(e => e.Id == input.EventId && e.Published, ct)
            ?? throw ApiFault.Missing();
        var version = await db.Set<EventVersion>().SingleOrDefaultAsync(v => v.Id == e.CurrentVersionId, ct)
            ?? throw ApiFault.Missing();
        if (version.Revision != input.ProgramVersion)
            throw ApiFault.Conflict("PROGRAM_CHANGED", "Etkinlik programı güncellendi.");
        if (version.Starts <= now || !await catalog.CanJoin(e, ct)) throw ApiFault.Forbidden();
        if (await db.Set<BookingEntity>().AnyAsync(b => b.EventId == e.Id && b.MemberId == caller.MemberId && b.Status == "confirmed", ct))
            throw ApiFault.Conflict("ALREADY_BOOKED", "Bu etkinliğe zaten katıldınız.");

        var hold = await db.Set<HoldRow>().Where(h => h.MemberId == caller.MemberId && h.EventId == e.Id &&
            (h.State == "reserved" && h.Expires > now || h.State == "paymentPending" && h.VerifyUntil > now))
            .SingleOrDefaultAsync(ct);
        var created = hold == null;
        if (created)
        {
            if (await Occupied(db, e.Id, now, Guid.Empty, ct) >= e.Capacity)
                throw ApiFault.Conflict("EVENT_FULL", "Etkinlik kontenjanı doldu.");

            hold = new HoldRow
            {
                MemberId = caller.MemberId, EventId = e.Id, VersionId = version.Id,
                State = "reserved", Expires = now.AddMinutes(10), Amount = version.Amount,
                Currency = version.Currency, TermsVersion = version.TermsVersion
            };
            db.Add(hold);
        }

        Remember("hold", key, fingerprint, hold!.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await Read(hold.Id, ct), created);
    }

    public async Task<Hold> Read(Guid id, CancellationToken ct)
    {
        var h = await Owned(id, ct);
        var v = await db.Set<EventVersion>().SingleAsync(v => v.Id == h.VersionId, ct);
        var days = await db.Set<ProgramDayRow>().CountAsync(d => d.VersionId == v.Id, ct);
        var paymentId = await db.Set<PaymentRow>().Where(p => p.HoldId == id).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct);
        var bookingId = await db.Set<BookingEntity>().Where(b => b.PaymentId == paymentId).Select(b => (Guid?)b.Id).SingleOrDefaultAsync(ct);
        var state = h.State;
        if (state == "reserved" && h.Expires <= Now) state = "expired";
        if (state == "paymentPending" && h.Expires <= Now)
            state = h.VerifyUntil > Now ? "verificationPending" : "expired";

        var snapshot = new CheckoutSnapshot(v.EventId, v.Title, v.Place, v.Cover == null ? null : new Media(v.Cover, null),
            v.Starts, v.Ends, v.TimeZone, v.Revision, days, 1, v.Lodging != null, new Money(h.Amount, h.Currency),
            new Terms(h.TermsVersion, v.TermsUrl));
        return new Hold(h.Id, state, h.Expires, h.VerifyUntil, bookingId, paymentId, snapshot);
    }

    internal async Task<HoldRow> Owned(Guid id, CancellationToken ct) =>
        await db.Set<HoldRow>().AsNoTracking().SingleOrDefaultAsync(h => h.Id == id && h.MemberId == caller.MemberId, ct)
        ?? throw ApiFault.Missing();

    internal async Task<Guid?> Lookup(string operation, string key, string fingerprint, CancellationToken ct)
    {
        var found = await db.Set<RequestKey>().SingleOrDefaultAsync(r =>
            r.MemberId == caller.MemberId && r.Operation == operation && r.Key == key, ct);
        if (found != null && found.Fingerprint != fingerprint)
            throw ApiFault.Conflict("KEY_REUSED", "İstek anahtarı başka bir işlem için kullanıldı.");
        return found?.ResourceId;
    }

    internal void Remember(string operation, string key, string fingerprint, Guid resourceId) =>
        db.Add(new RequestKey
        {
            MemberId = caller.MemberId, Operation = operation, Key = key,
            Fingerprint = fingerprint, ResourceId = resourceId
        });

    public async Task<BookingPage> List(string bucket, string? cursor, int? limit, CancellationToken ct)
    {
        var page = Paging.Parse(cursor, limit);
        var now = Now;
        var rows = from b in db.Set<BookingEntity>() join v in db.Set<EventVersion>() on b.VersionId equals v.Id
                   where b.MemberId == caller.MemberId select new { Booking = b, Version = v };
        rows = bucket switch
        {
            "upcoming" => rows.Where(x => x.Version.Starts > now),
            "inProgress" => rows.Where(x => x.Version.Starts <= now && x.Version.Ends > now),
            "past" => rows.Where(x => x.Version.Ends <= now),
            _ => throw ApiFault.Invalid("Katılım grubu geçersiz.")
        };
        var batch = await rows.OrderBy(x => x.Version.Starts).ThenBy(x => x.Booking.Id)
            .Skip(page.Skip).Take(page.Limit + 1).ToListAsync(ct);
        var items = new List<Contracts.BookingRow>();
        foreach (var x in batch.Take(page.Limit))
        {
            var card = await catalog.Cards(db.Set<EventVersion>().Where(v => v.Id == x.Version.Id)).SingleAsync(ct);
            var canShowPass = x.Booking.Status == "confirmed" && x.Version.Ends > now &&
                card.Phase != "cancelled" && await db.Set<PassRow>().AnyAsync(p => p.BookingId == x.Booking.Id && !p.Revoked, ct);
            var preview = x.Booking.Status == "confirmed" && card.Phase == "inProgress"
                ? await ParticipantPreview(card.Id, ct) : [];
            items.Add(new Contracts.BookingRow(x.Booking.Id, card, x.Booking.Status, x.Booking.Attendance,
                canShowPass, preview));
        }
        return new BookingPage(items, page.Next(batch.Count));
    }

    private Task<List<Person>> ParticipantPreview(Guid eventId, CancellationToken ct) =>
        (from booking in db.Set<BookingEntity>()
         join member in db.Set<Member>() on booking.MemberId equals member.Id
         where booking.EventId == eventId && booking.Status == "confirmed" &&
               member.Status == "active" && member.Visibility == "club"
         orderby member.FullName, member.Id
         select new Person(member.FullName, member.Avatar == null ? null : new Media(member.Avatar, null)))
        .Take(3).ToListAsync(ct);

    public async Task<Pass> Pass(Guid id, CancellationToken ct)
    {
        var booking = await db.Set<BookingEntity>().SingleOrDefaultAsync(b => b.Id == id && b.MemberId == caller.MemberId, ct)
            ?? throw ApiFault.Missing();
        var v = await db.Set<EventVersion>().SingleAsync(v => v.Id == booking.VersionId, ct);
        if (booking.Status != "confirmed" || v.Ends <= Now ||
            await db.Set<ClubEvent>().AnyAsync(e => e.Id == booking.EventId && e.Cancelled, ct))
            throw ApiFault.Conflict("PASS_INACTIVE", "Katılım kartı etkin değil.");
        var pass = await db.Set<PassRow>().SingleOrDefaultAsync(p => p.BookingId == id && !p.Revoked, ct)
            ?? throw ApiFault.Missing();
        return new Pass(id, v.EventId, v.Title, v.Starts, v.TimeZone,
            new MeetingPoint(v.Meeting, v.Directions, v.Latitude is double lat ? new Point(lat, v.Longitude!.Value) : null),
            pass.Nonce, v.Ends);
    }
}
