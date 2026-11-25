using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Field;

public sealed class LiveMapService(ClubDb db, Caller caller, CatalogQueries catalog, IHostEnvironment environment)
{
    private async Task<EventVersion> LiveEvent(Guid id, CancellationToken ct)
    {
        var now = catalog.Now;
        var eligible = catalog.EligibleEvents();
        var v = await (from e in eligible join version in db.Set<EventVersion>() on e.CurrentVersionId equals version.Id
            where e.Id == id && !e.Cancelled && e.Published select version).SingleOrDefaultAsync(ct) ?? throw ApiFault.Forbidden();
        if (v.Starts > now || v.Ends <= now) throw ApiFault.Conflict("EVENT_NOT_LIVE", "Etkinlik şu anda aktif değil.");
        return v;
    }

    public async Task<ActivityMap> Read(Guid id, CancellationToken ct)
    {
        await LiveEvent(id, ct);
        if (environment.IsDevelopment())
            await DevelopmentSamples.RefreshPeerLocations(db, id, ct);
        var now = catalog.Now;
        var freshness = now.AddMinutes(-2);
        var eligiblePins = from share in db.Set<LocationShare>()
            join last in db.Set<LastLocation>() on share.Id equals last.ShareId
            join member in db.Set<Member>() on share.MemberId equals member.Id
            where share.EventId == id && !share.Revoked && share.Expires > now && last.Recorded >= freshness &&
                  member.Status == "active" &&
                  (db.Set<Persistence.BookingRow>().Any(b => b.MemberId == member.Id && b.EventId == id && b.Status == "confirmed") ||
                   db.Set<Assignment>().Any(a => a.MemberId == member.Id && a.EventId == id && a.Starts <= now && a.Ends > now))
            select new Pin(share.Alias, member.FullName, member.Avatar == null ? null : new Media(member.Avatar, null),
                new Point(last.Latitude, last.Longitude), last.Recorded);
        var route = await db.Set<RoutePoint>().Where(r => r.EventId == id).OrderBy(r => r.Ordinal)
            .Select(r => new Point(r.Latitude, r.Longitude)).ToListAsync(ct);
        var mine = await Open(id).Where(s => s.Expires > now).SingleOrDefaultAsync(ct);
        return new ActivityMap(id, now, route, await eligiblePins.ToListAsync(ct), State(mine));
    }

    public async Task<Sharing> Share(Guid id, bool enabled, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync($"location:{id}:{caller.MemberId}", ct);
        var share = await Open(id).SingleOrDefaultAsync(ct);
        if (share != null && (!enabled || share.Expires <= catalog.Now))
        {
            await db.Set<LastLocation>().Where(l => l.ShareId == share.Id).ExecuteDeleteAsync(ct);
            share.Revoked = true;
            await db.SaveChangesAsync(ct);
            share = null;
        }
        if (enabled)
        {
            var v = await LiveEvent(id, ct);
            if (share == null)
            {
                share = new LocationShare { EventId = id, MemberId = caller.MemberId, Alias = Guid.NewGuid(), Expires = v.Ends };
                db.Add(share);
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return State(share);
    }

    public async Task Record(Guid id, LocationUpdate input, CancellationToken ct)
    {
        var now = catalog.Now;
        if (input.Position == null || !double.IsFinite(input.Position.Latitude) || !double.IsFinite(input.Position.Longitude) ||
            input.Position.Latitude is < -90 or > 90 || input.Position.Longitude is < -180 or > 180 ||
            input.Sequence <= 0 || input.RecordedAt.Kind != DateTimeKind.Utc ||
            input.RecordedAt > now.AddSeconds(15) || input.RecordedAt < now.AddMinutes(-2))
            throw ApiFault.Invalid("Konum ölçümü geçersiz veya güncel değil.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync($"location:{id}:{caller.MemberId}", ct);
        await LiveEvent(id, ct);
        var active = await Open(id).AnyAsync(s => s.Id == input.SharingId && s.Expires > now, ct);
        if (!active) throw ApiFault.Forbidden();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO last_location (id, share_id, sequence, recorded, latitude, longitude)
            VALUES ({Guid.NewGuid()}, {input.SharingId}, {input.Sequence}, {input.RecordedAt}, {input.Position.Latitude}, {input.Position.Longitude})
            ON CONFLICT (share_id) DO UPDATE SET sequence = EXCLUDED.sequence, recorded = EXCLUDED.recorded,
                latitude = EXCLUDED.latitude, longitude = EXCLUDED.longitude
            WHERE last_location.sequence < EXCLUDED.sequence AND last_location.recorded <= EXCLUDED.recorded
            """, ct);
        await tx.CommitAsync(ct);
    }

    private IQueryable<LocationShare> Open(Guid id) =>
        db.Set<LocationShare>().Where(s => s.MemberId == caller.MemberId && s.EventId == id && !s.Revoked);

    private static Sharing State(LocationShare? share) => new(share != null, share?.Id, share?.Expires);
}
