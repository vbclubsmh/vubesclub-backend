using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalog(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("").ForMembers();
        routes.MapGet("/event-filters", async (ClubDb db, CancellationToken ct) =>
            new Filters(await db.Set<SportRow>().OrderBy(s => s.Label).Select(s => new Sport(s.Code, s.Label,
                s.FallbackCover == null ? null : new Media(s.FallbackCover, null))).ToListAsync(ct),
                [new("easy", "Kolay"), new("moderate", "Orta"), new("hard", "Zor")]));
        routes.MapGet("/events", List);
        routes.MapGet("/events/{eventId:guid}", (Guid eventId, Guid? bookingId, CatalogQueries catalog, CancellationToken ct) =>
            catalog.Detail(eventId, bookingId, ct));
        routes.MapPut("/me/saved-events/{eventId:guid}", async (Guid eventId, Caller caller, ClubDb db, CancellationToken ct) =>
        {
            if (!await db.Set<ClubEvent>().AnyAsync(e => e.Id == eventId && e.Published && !e.Cancelled, ct))
                throw ApiFault.Missing();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO saved_event (id, member_id, event_id) VALUES ({Guid.NewGuid()}, {caller.MemberId}, {eventId})
                ON CONFLICT (member_id, event_id) DO NOTHING
                """, ct);
            return Results.NoContent();
        });
        routes.MapDelete("/me/saved-events/{eventId:guid}", async (Guid eventId, Caller caller, ClubDb db, CancellationToken ct) =>
        {
            await db.Set<SavedEvent>().Where(s => s.MemberId == caller.MemberId && s.EventId == eventId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<EventPage> List(string? sport, string? level, bool? savedOnly, bool? hasLocation,
        string? bbox, string? cursor, int? limit, Caller caller, ClubDb db, CatalogQueries catalog, CancellationToken ct)
    {
        var page = Paging.Parse(cursor, limit);
        var now = catalog.Now;
        var query = catalog.PublishedVersions().Where(v => v.Starts > now);

        if (sport != null) query = query.Where(v => db.Set<SportRow>().Any(s => s.Id == v.SportId && s.Code == sport));
        if (level != null) query = query.Where(v => v.Level == level);
        if (hasLocation == true) query = query.Where(v => v.Latitude != null);
        if (savedOnly == true) query = query.Where(v => db.Set<SavedEvent>()
            .Any(s => s.MemberId == caller.MemberId && s.EventId == v.EventId));

        if (bbox != null)
        {
            var parts = bbox.Split(',');
            var bounds = new double[4];
            if (parts.Length != 4 || !parts.Select((s, i) => double.TryParse(s, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out bounds[i]) && double.IsFinite(bounds[i])).All(ok => ok) ||
                bounds[0] < -180 || bounds[2] > 180 || bounds[1] < -90 || bounds[3] > 90 ||
                bounds[0] > bounds[2] || bounds[1] > bounds[3])
                throw new ApiFault(400, "INVALID_BOUNDS", "Harita sınırları geçersiz.");

            query = query.Where(v => v.Longitude >= bounds[0] && v.Longitude <= bounds[2] &&
                                     v.Latitude >= bounds[1] && v.Latitude <= bounds[3]);
        }

        var total = await query.CountAsync(ct);
        var versions = query.OrderBy(v => v.Starts).ThenBy(v => v.EventId).Skip(page.Skip).Take(page.Limit + 1);
        var cards = (await catalog.Cards(versions).ToListAsync(ct)).OrderBy(v => v.StartsAt).ThenBy(v => v.Id).ToArray();
        return new EventPage(cards.Take(page.Limit).ToArray(), page.Next(cards.Length), total);
    }
}
