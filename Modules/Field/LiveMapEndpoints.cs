using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Catalog;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Field;

public static class LiveMapEndpoints
{
    public static void MapField(this RouteGroupBuilder api)
    {
        api.MapGroup("/field").ForMembers().MapPost("/check-ins", (CheckInRequest body, FieldCheckInService service,
            CancellationToken ct) => service.CheckIn(body.Payload, ct));

        var routes = api.MapGroup("/events/{eventId:guid}").ForMembers();
        routes.MapGet("/activity-map", (Guid eventId, LiveMapService service, CancellationToken ct) => service.Read(eventId, ct));
        routes.MapPut("/me/location-sharing", (Guid eventId, ShareRequest body, LiveMapService service, CancellationToken ct) =>
            service.Share(eventId, body.Enabled, ct));
        routes.MapPut("/me/location", async (Guid eventId, LocationUpdate body, LiveMapService service, CancellationToken ct) =>
        {
            await service.Record(eventId, body, ct);
            return Results.NoContent();
        });
    }
}
