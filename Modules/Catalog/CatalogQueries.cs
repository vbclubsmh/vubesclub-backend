using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Catalog;

public sealed class CatalogQueries(ClubDb db, Caller caller, TimeProvider clock)
{
    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    public IQueryable<ClubEvent> EligibleEvents()
    {
        var now = Now;
        return db.Set<ClubEvent>().Where(e =>
            db.Set<Persistence.BookingRow>().Any(b => b.MemberId == caller.MemberId && b.EventId == e.Id && b.Status == "confirmed") ||
            db.Set<Assignment>().Any(a => a.MemberId == caller.MemberId && a.EventId == e.Id && a.Starts <= now && a.Ends > now));
    }

    public IQueryable<TrainerCard> Trainers(IQueryable<Trainer> source)
    {
        return from t in source
               join m in db.Set<Member>() on t.MemberId equals m.Id
               where m.Status == "active"
               select new TrainerCard(t.Id, m.FullName, t.Specialty, t.Experience, t.Location, t.Bio,
                   m.Avatar == null ? null : new Media(m.Avatar, null),
                   t.Cover == null ? null : new Media(t.Cover, null),
                   db.Set<Follow>().Any(f => f.MemberId == caller.MemberId && f.TrainerId == t.Id),
                   db.Set<IntroductionRow>().Where(i => i.TrainerId == t.Id && i.MemberId == caller.MemberId)
                       .Select(i => i.State).FirstOrDefault() ?? "none");
    }

    public IQueryable<EventCard> Cards(IQueryable<EventVersion> versions)
    {
        var now = Now;
        var publishedRatings = db.Set<RatingRow>().Where(r => r.Moderation == "published");

        return from version in versions
               join e in db.Set<ClubEvent>() on version.EventId equals e.Id
               join s in db.Set<SportRow>() on version.SportId equals s.Id
               select new EventCard(e.Id, version.Title, version.Place, version.Starts, version.Ends, version.TimeZone,
                   new Sport(s.Code, s.Label, s.FallbackCover == null ? null : new Media(s.FallbackCover, null)),
                   version.Level, version.Cover == null ? null : new Media(version.Cover, null), version.DistanceMeters,
                   new Money(version.Amount, version.Currency),
                   e.Cancelled ? "cancelled" : version.Ends <= now ? "past" : version.Starts <= now ? "inProgress" : "upcoming",
                   version.Latitude == null ? null : new Point(version.Latitude.Value, version.Longitude!.Value),
                   db.Set<SavedEvent>().Any(sv => sv.EventId == e.Id && sv.MemberId == caller.MemberId),
                   version.Ends <= now ? new RatingSummary(
                       publishedRatings.Where(r => r.EventId == e.Id).Average(r => (double?)r.Stars),
                       publishedRatings.Count(r => r.EventId == e.Id)) : null,
                   e.Featured);
    }

    public IQueryable<EventVersion> PublishedVersions() =>
        from v in db.Set<EventVersion>()
        join e in db.Set<ClubEvent>() on v.Id equals e.CurrentVersionId
        where e.Published && !e.Cancelled
        select v;

    public async Task<bool> CanJoin(ClubEvent e, CancellationToken ct)
    {
        var now = Now;
        if (!e.Published || e.Cancelled || now >= e.Closes) return false;
        if (now >= e.Opens) return true;
        return e.PremiumOpens <= now && await db.Set<Subscription>()
            .AnyAsync(s => s.MemberId == caller.MemberId && s.Starts <= now && s.Ends > now, ct);
    }

    public Task<bool> CanRate(Guid eventId, CancellationToken ct) =>
        db.Set<Persistence.BookingRow>().AnyAsync(b => b.MemberId == caller.MemberId && b.EventId == eventId
            && b.Status == "confirmed" && b.Attendance == "attended"
            && db.Set<EventVersion>().Any(v => v.Id == b.VersionId && v.Ends <= Now), ct);

    public async Task<EventDetail> Detail(Guid eventId, Guid? bookingId, CancellationToken ct)
    {
        var e = await db.Set<ClubEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct)
            ?? throw ApiFault.Missing();
        var bookings = db.Set<Persistence.BookingRow>().AsNoTracking()
            .Where(b => b.MemberId == caller.MemberId && b.EventId == eventId);
        var booking = bookingId.HasValue
            ? await bookings.SingleOrDefaultAsync(b => b.Id == bookingId, ct) ?? throw ApiFault.Missing()
            : await bookings.SingleOrDefaultAsync(b => b.Status == "confirmed", ct);

        if (!e.Published && booking == null) throw ApiFault.Missing();
        var versionId = bookingId.HasValue ? booking!.VersionId : e.CurrentVersionId;
        var versions = db.Set<EventVersion>().Where(v => v.Id == versionId);
        var v = await versions.SingleOrDefaultAsync(ct) ?? throw ApiFault.Missing();
        var card = await Cards(versions).SingleAsync(ct);
        var trainer = await Trainers(db.Set<Trainer>().Where(t => t.Id == e.TrainerId)).SingleOrDefaultAsync(ct)
            ?? throw ApiFault.Missing();

        var days = await db.Set<ProgramDayRow>().Where(d => d.VersionId == v.Id).OrderBy(d => d.Ordinal).ToListAsync(ct);
        var slots = await db.Set<ProgramSlot>().Where(s => db.Set<ProgramDayRow>()
            .Any(d => d.Id == s.DayId && d.VersionId == v.Id)).OrderBy(s => s.Ordinal).ToListAsync(ct);
        var program = days.Select(d => new ProgramDay(d.Id, d.Day, slots.Where(s => s.DayId == d.Id)
            .Select(s => new ProgramSession(s.Id, s.Starts, s.Ends, s.Title, s.Details)).ToArray())).ToArray();
        var extras = await (from extra in db.Set<AdditionalSport>() join sport in db.Set<SportRow>()
            on extra.SportId equals sport.Id where extra.VersionId == v.Id
            select new Sport(sport.Code, sport.Label, null)).ToListAsync(ct);
        var now = Now;
        var hold = await db.Set<HoldRow>().Where(h => h.EventId == eventId && h.MemberId == caller.MemberId &&
            ((h.State == "reserved" && h.Expires > now) || (h.State == "paymentPending" && h.VerifyUntil > now)))
            .OrderByDescending(h => h.Expires).FirstOrDefaultAsync(ct);
        var booked = booking?.Status == "confirmed";
        var primary = booked ? "viewPass" : hold != null ? "resumeCheckout" :
            card.Phase == "upcoming" && await CanJoin(e, ct) ? "join" : "none";
        var ownRating = await db.Set<RatingRow>().Where(r => r.MemberId == caller.MemberId && r.EventId == eventId)
            .Select(r => new MyRating(r.Stars, r.Moderation)).SingleOrDefaultAsync(ct);
        var viewer = new Viewer(primary, primary == "none" ? "BOOKING_CLOSED" : null,
            booked ? booking!.Id : null, hold?.Id,
            card.Phase == "inProgress" && await EligibleEvents().AnyAsync(x => x.Id == eventId, ct),
            await CanRate(eventId, ct), ownRating);
        return new EventDetail(card, v.Revision, extras, trainer, program, v.Preparation,
            new MeetingPoint(v.Meeting, v.Directions, card.Position), v.Transport, v.Lodging,
            v.Gallery.Select(url => new Media(url, null)).ToArray(), viewer);
    }
}
