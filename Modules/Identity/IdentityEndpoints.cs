using VubesClub.Api.Persistence;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Contracts;
using Microsoft.EntityFrameworkCore;
namespace VubesClub.Api.Modules.Identity;

public static class IdentityEndpoints
{
    public static void MapIdentity(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");
        auth.MapPost("/otp-challenges", async (LoginOtp body, IdentityService service, CancellationToken ct) =>
            Results.Json(await service.CreateLoginChallenge(body, ct), statusCode: 202));
        auth.MapPost("/application-otp-challenges", async (ApplicationOtp body, IdentityService service, CancellationToken ct) =>
            Results.Json(await service.CreateApplicationChallenge(body, ct), statusCode: 202));
        auth.MapPost("/otp-challenges/{challengeId:guid}/resend",
            (Guid challengeId, IdentityService service, CancellationToken ct) => service.Resend(challengeId, ct));
        auth.MapPost("/otp-challenges/{challengeId:guid}/verify",
            (Guid challengeId, VerifyRequest body, IdentityService service, CancellationToken ct) =>
                service.Verify(challengeId, body.Code, ct));
        auth.MapPost("/session/refresh", (RefreshRequest body, IdentityService service, CancellationToken ct) =>
            service.Refresh(body.RefreshToken, ct));

        var session = auth.MapGroup("/session").ForMembers(false);
        session.MapGet("", (Caller caller) => IdentityService.Describe(caller.Membership, caller.Expires));
        session.MapDelete("", async (Caller caller, ClubDb db, CancellationToken ct) =>
        {
            await db.Set<MobileSession>().Where(s => s.Id == caller.SessionId)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Revoked, true), ct);
            return Results.NoContent();
        });

        var me = api.MapGroup("/me").ForMembers();
        me.MapGet("", async (Caller caller, ClubDb db, CancellationToken ct) =>
        {
            var member = await db.Set<Member>().SingleAsync(m => m.Id == caller.MemberId, ct);
            var now = DateTime.UtcNow;
            var premium = await db.Set<Subscription>().AnyAsync(s =>
                s.MemberId == member.Id && s.Starts <= now && s.Ends > now, ct);
            var trainer = await db.Set<Trainer>().AnyAsync(t => t.MemberId == member.Id, ct);

            return new MyProfile(member.Id, member.FullName, Asset(member.Avatar), Asset(member.Cover),
                "•••• " + member.Phone[^4..], member.Status, premium ? "premium" : "standard", trainer);
        });
        me.MapGet("/privacy", (Caller caller, ClubDb db, CancellationToken ct) =>
            db.Set<Member>().Where(m => m.Id == caller.MemberId).Select(m => new Privacy(m.Visibility)).SingleAsync(ct));
    }

    private static Media? Asset(string? url) => url == null ? null : new Media(url, null);
}
