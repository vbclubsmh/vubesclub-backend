using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Infrastructure;

public static class HttpPipeline
{
    public static void UseClubAccess(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                if (context.GetEndpoint()?.Metadata.GetMetadata<MemberAccess>() is { } rule)
                    await Identify(context, rule);

                await next(context);
            }
            catch (ApiFault fault) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = fault.Status;
                await context.Response.WriteAsJsonAsync(new ApiError(fault.Code, fault.Message, context.TraceIdentifier));
            }
        });
    }

    private static async Task Identify(HttpContext context, MemberAccess access)
    {
        var bearer = context.Request.Headers.Authorization.ToString();
        if (!bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || bearer.Length != 71)
            throw ApiFault.Unauthorized();

        var digest = Secrets.Digest(bearer[7..]);
        var db = context.RequestServices.GetRequiredService<ClubDb>();
        var now = DateTime.UtcNow;
        var result = await (
            from session in db.Set<MobileSession>()
            join member in db.Set<Member>() on session.MemberId equals member.Id
            where !session.Revoked && session.AccessDigest == digest && session.AccessExpires > now && session.Expires > now
            select new { Session = session.Id, Member = member.Id, member.Status, session.Expires }
        ).SingleOrDefaultAsync(context.RequestAborted) ?? throw ApiFault.Unauthorized();

        if (access.ActiveRequired && result.Status != "active")
            throw ApiFault.Forbidden();

        var caller = context.RequestServices.GetRequiredService<Caller>();
        caller.MemberId = result.Member;
        caller.SessionId = result.Session;
        caller.Membership = result.Status;
        caller.Expires = result.Expires;
        context.Response.Headers.CacheControl = "no-store";
    }
}
