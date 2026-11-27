using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Community;

public sealed class CommunityService(ClubDb db, Caller caller, CatalogQueries catalog)
{
    private IQueryable<Trainer> Accessible() => db.Set<Trainer>().Where(trainer => trainer.Active &&
        db.Set<ClubEvent>().Any(e => e.TrainerId == trainer.Id && e.Published && !e.Cancelled));

    public async Task<TrainerPage> List(bool? followingOnly, string? cursor, int? limit, CancellationToken ct)
    {
        var trainers = Accessible();
        if (followingOnly == true)
            trainers = trainers.Where(t => db.Set<Follow>().Any(f => f.TrainerId == t.Id && f.MemberId == caller.MemberId));

        var page = Paging.Parse(cursor, limit);
        var rows = await catalog.Trainers(trainers.OrderBy(t => t.Id).Skip(page.Skip).Take(page.Limit + 1)).ToListAsync(ct);
        return new TrainerPage(rows.OrderBy(t => t.Id).Take(page.Limit).ToArray(), page.Next(rows.Count));
    }

    public async Task<ClubFeed> Feed(CancellationToken ct)
    {
        var followed = await db.Set<Follow>().Where(f => f.MemberId == caller.MemberId)
            .Select(f => f.TrainerId).ToListAsync(ct);
        var linked = await db.Set<TrainerLink>().Where(l => l.MemberId == caller.MemberId)
            .Select(l => l.TrainerId).ToListAsync(ct);
        var wanted = followed.Union(linked).ToList();

        var cards = await catalog.Trainers(Accessible().Where(t => wanted.Contains(t.Id)).OrderBy(t => t.Id)).ToListAsync(ct);
        var trainers = cards.Select(card => new FeedTrainer(card,
            followed.Contains(card.Id) && linked.Contains(card.Id) ? "both" : followed.Contains(card.Id) ? "following" : "linked")).ToArray();
        // Takip edilen veya bağlı PT yoksa akış tüm PT'lerin etkinliklerinden oluşur.
        var sources = cards.Count > 0
            ? cards
            : await catalog.Trainers(Accessible().OrderBy(t => t.Id)).ToListAsync(ct);
        var ids = sources.Select(c => c.Id).ToList();
        if (ids.Count == 0) return new ClubFeed(trainers, [], []);

        var owners = await db.Set<ClubEvent>().Where(e => ids.Contains(e.TrainerId))
            .Select(e => new { e.Id, e.TrainerId }).ToDictionaryAsync(e => e.Id, e => e.TrainerId, ct);
        var versions = catalog.PublishedVersions().Where(v => owners.Keys.Contains(v.EventId));
        var now = catalog.Now;
        var upcoming = await catalog.Cards(versions.Where(v => v.Starts > now)).ToListAsync(ct);
        var rated = await catalog.Cards(versions.Where(v => v.Ends <= now &&
            db.Set<RatingRow>().Any(r => r.EventId == v.EventId && r.Moderation == "published"))).ToListAsync(ct);

        var byId = sources.ToDictionary(c => c.Id);
        FeedEvent Wrap(EventCard e)
        {
            var trainer = byId[owners[e.Id]];
            return new FeedEvent(e, new TrainerRef(trainer.Id, trainer.DisplayName, trainer.Avatar));
        }

        return new ClubFeed(trainers,
            upcoming.OrderBy(e => e.StartsAt).Select(Wrap).ToArray(),
            rated.OrderByDescending(e => e.StartsAt).Select(Wrap).ToArray());
    }

    public async Task<TrainerDetail> Detail(Guid id, CancellationToken ct)
    {
        var trainer = await catalog.Trainers(Accessible().Where(t => t.Id == id)).SingleOrDefaultAsync(ct)
            ?? throw ApiFault.Missing();
        var crew = await db.Set<CrewRow>().SingleOrDefaultAsync(c => c.TrainerId == id, ct);
        Crew? preview = null;
        if (crew != null)
        {
            var members = await (from cm in db.Set<CrewMate>() join m in db.Set<Member>() on cm.MemberId equals m.Id
                where cm.CrewId == crew.Id && m.Status == "active" && (m.Visibility == "club" || m.Id == caller.MemberId)
                orderby m.FullName
                select new Person(m.FullName, m.Avatar == null ? null : new Media(m.Avatar, null))).ToListAsync(ct);
            preview = new Crew(crew.Name, members);
        }

        var versions = catalog.PublishedVersions().Where(v => db.Set<ClubEvent>().Any(e => e.Id == v.EventId && e.TrainerId == id));
        var now = catalog.Now;
        var upcoming = await catalog.Cards(versions.Where(v => v.Starts > now)).ToListAsync(ct);
        var rated = await catalog.Cards(versions.Where(v => v.Ends <= now &&
            db.Set<RatingRow>().Any(r => r.EventId == v.EventId && r.Moderation == "published"))).ToListAsync(ct);
        return new TrainerDetail(trainer, preview, upcoming.OrderBy(v => v.StartsAt).ToArray(), rated.OrderByDescending(v => v.StartsAt).ToArray());
    }

    public async Task Follow(Guid id, CancellationToken ct)
    {
        await VisibleTrainer(id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO follow (id, member_id, trainer_id) VALUES ({Guid.NewGuid()}, {caller.MemberId}, {id})
            ON CONFLICT (member_id, trainer_id) DO NOTHING
            """, ct);
    }

    public async Task<Introduction> Introduce(Guid id, CancellationToken ct)
    {
        await VisibleTrainer(id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO introduction_row (id, member_id, trainer_id, state, requested_at)
            VALUES ({Guid.NewGuid()}, {caller.MemberId}, {id}, 'requested', {catalog.Now})
            ON CONFLICT (member_id, trainer_id) DO NOTHING
            """, ct);
        return await db.Set<IntroductionRow>().Where(i => i.MemberId == caller.MemberId && i.TrainerId == id)
            .Select(i => new Introduction(i.TrainerId, i.State, i.RequestedAt)).SingleAsync(ct);
    }

    public async Task<RatingResult> Rate(Guid eventId, int stars, CancellationToken ct)
    {
        if (stars is < 1 or > 5) throw ApiFault.Invalid("Puan 1–5 arasında olmalı.");
        if (!await catalog.CanRate(eventId, ct)) throw ApiFault.Forbidden();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO rating_row (id, member_id, event_id, stars, moderation)
            VALUES ({Guid.NewGuid()}, {caller.MemberId}, {eventId}, {stars}, 'pending')
            ON CONFLICT (member_id, event_id) DO UPDATE SET stars = EXCLUDED.stars, moderation = 'pending'
            WHERE rating_row.stars <> EXCLUDED.stars
            """, ct);
        var summary = await db.Set<RatingRow>().Where(r => r.EventId == eventId && r.Moderation == "published")
            .GroupBy(r => r.EventId).Select(g => new RatingSummary(g.Average(r => (double)r.Stars), g.Count())).SingleOrDefaultAsync(ct);
        var mine = await db.Set<RatingRow>().Where(r => r.EventId == eventId && r.MemberId == caller.MemberId)
            .Select(r => new MyRating(r.Stars, r.Moderation)).SingleAsync(ct);
        return new RatingResult(summary ?? new RatingSummary(null, 0), mine);
    }

    private async Task VisibleTrainer(Guid id, CancellationToken ct)
    {
        if (!await db.Set<Trainer>().AnyAsync(t => t.Id == id && t.Active &&
                db.Set<Member>().Any(m => m.Id == t.MemberId && m.Status == "active") &&
                db.Set<ClubEvent>().Any(e => e.TrainerId == id && e.Published && !e.Cancelled), ct))
            throw ApiFault.Missing();
    }
}
