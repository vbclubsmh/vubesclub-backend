using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Community;

public static class CommunityEndpoints
{
    public static void MapCommunity(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("").ForMembers();
        routes.MapGet("/club/trainers", (bool? followingOnly, string? cursor, int? limit, CommunityService service, CancellationToken ct) =>
            service.List(followingOnly, cursor, limit, ct));
        routes.MapGet("/club/feed", (CommunityService service, CancellationToken ct) => service.Feed(ct));
        routes.MapGet("/club/trainers/{trainerId:guid}", (Guid trainerId, CommunityService service, CancellationToken ct) =>
            service.Detail(trainerId, ct));
        routes.MapPut("/me/followed-trainers/{trainerId:guid}", async (Guid trainerId, CommunityService service, CancellationToken ct) =>
        {
            await service.Follow(trainerId, ct);
            return Results.NoContent();
        });
        routes.MapDelete("/me/followed-trainers/{trainerId:guid}", async (Guid trainerId, ClubDb db, Caller caller, CancellationToken ct) =>
        {
            await db.Set<Follow>().Where(f => f.MemberId == caller.MemberId && f.TrainerId == trainerId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        routes.MapPut("/me/trainer-introductions/{trainerId:guid}", (Guid trainerId, CommunityService service, CancellationToken ct) =>
            service.Introduce(trainerId, ct));
        routes.MapPut("/events/{eventId:guid}/me/rating", (Guid eventId, RateRequest body, CommunityService service, CancellationToken ct) =>
            service.Rate(eventId, body.Stars, ct));
    }
}
