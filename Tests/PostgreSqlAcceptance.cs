using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Payments;
using VubesClub.Api.Persistence;

namespace VubesClub.Verification;

internal static class PostgreSqlAcceptance
{
    private const string Api = "/api/v1";
    private const string MemberPhone = "+905555555555";
    private const string ApplicantPhone = "+905555555556";

    public static async Task Run(string connection)
    {
        var schema = "acceptance_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin);
        await create.ExecuteNonQueryAsync();

        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
            await RunInSchema(settings.ConnectionString);
        }
        finally
        {
            // Only the schema allocated to this verification run in the dedicated test DB.
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task RunInSchema(string connection)
    {
        var server = new InProcessServer();
        var gateway = new TestGateway();
        await using var app = ClubHost.Build(
            ["--environment=Development", "--ConnectionStrings:Club=" + connection,
             "--DevelopmentSms:Code=1111", "--DevelopmentSms:Phones:0=" + MemberPhone,
             "--DevelopmentSms:Phones:1=" + ApplicantPhone, "--Security:OtpKey=" + Secrets.RandomToken()],
            builder =>
            {
                builder.Services.AddSingleton<IServer>(server);
                builder.Services.AddSingleton<IPaymentGateway>(gateway);
                builder.Logging.ClearProviders();
            });
        await app.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubDb>();
        await db.Database.MigrateAsync();

        using var stream = typeof(ClubDb).Assembly.GetManifestResourceStream("VubesClub.Api.Database.DevelopmentSeed.sql")!;
        using var reader = new StreamReader(stream);
        var seed = await reader.ReadToEndAsync();
        // NpgsqlCommand uses the SQL verbatim, including JSON literals in the fixture.
        await using (var connectionToSchema = new NpgsqlConnection(connection))
        {
            await connectionToSchema.OpenAsync();
            await using var cmd = new NpgsqlCommand(seed, connectionToSchema);
            await cmd.ExecuteNonQueryAsync();
            var before = await db.Set<ClubEvent>().CountAsync();
            await cmd.ExecuteNonQueryAsync();
            Checks.Require(before > 0 && before == await db.Set<ClubEvent>().CountAsync(), "Seed duplicated catalog data.");
        }

        var member = await VerifyOtp(server, new OtpRequest("signIn", MemberPhone, null, null, null));
        var applicant = await VerifyOtp(server, new OtpRequest("invitation", ApplicantPhone, "AFTERGLOW", "Test Üyesi", true));
        Checks.Require(applicant.Session.NextAction == "review", "A new application must wait for review.");
        (await server.Send("GET", Api + "/events", bearer: applicant.Tokens.AccessToken)).Expect(HttpStatusCode.Forbidden);
        await db.Set<Member>().Where(m => m.Phone == ApplicantPhone).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "active"));
        Checks.Passed("seed replay, sign-in and invitation with 1111, pending membership enforcement");

        await VerifyScreens(server, db, member.Tokens.AccessToken);

        var version = await MakeEvent(db);
        string updatedTitle = "SQL ile güncellenen başlık";
        await db.Set<EventVersion>().Where(v => v.Id == version.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Title, updatedTitle));
        var token = member.Tokens.AccessToken;
        var details = (await server.Send("GET", Api + "/events/" + version.EventId, bearer: token)).Expect(HttpStatusCode.OK).Read<EventDetail>();
        var list = (await server.Send("GET", Api + "/events?limit=100", bearer: token)).Expect(HttpStatusCode.OK).Read<EventPage>();
        Checks.Require(details.Event.Title == updatedTitle && list.Items.Any(c => c.Id == version.EventId && c.Title == updatedTitle), "SQL change was not reflected in both endpoints.");
        Checks.Passed("database title change appears in catalog and event detail via HTTP");

        var tokens = new[] { token, applicant.Tokens.AccessToken };
        var reserve = new CreateHold(version.EventId, details.ProgramVersion);
        var attempts = await Task.WhenAll(tokens.Select(t => server.Send("POST", Api + "/booking-holds", reserve, t, "last-seat")));
        int winner = Array.FindIndex(attempts, a => a.Status == HttpStatusCode.Created);
        Checks.Require(winner >= 0, "Neither member acquired the last seat: " + string.Join(" / ", attempts.Select(a => a.Body)));
        var loser = attempts[1 - winner].Expect(HttpStatusCode.Conflict);
        Checks.Require(loser.Read<ApiError>().Code == "EVENT_FULL", "Unexpected contention result.");
        var hold = attempts[winner].Read<Hold>();
        var winningToken = tokens[winner];
        var replay = (await server.Send("POST", Api + "/booking-holds", reserve, winningToken, "last-seat")).Expect(HttpStatusCode.OK).Read<Hold>();
        Checks.Require(replay.Id == hold.Id, "Replaying a reservation allocated another hold.");

        var payment = await StartPayment(server, hold, winningToken, HttpStatusCode.Created);
        var repeated = await StartPayment(server, hold, winningToken, HttpStatusCode.OK);
        Checks.Require(payment.PaymentId == repeated.PaymentId, "Payment replay opened another order.");
        var notice = gateway.Paid(payment.PaymentId, DateTime.UtcNow);
        await SendNoticeTwice(server, gateway, notice);

        string holdPath = Api + "/booking-holds/" + hold.Id;
        var confirmed = (await server.Send("GET", holdPath, bearer: winningToken)).Expect(HttpStatusCode.OK).Read<Hold>();
        Checks.Require(confirmed.State == "confirmed" && confirmed.BookingId.HasValue, "Paid hold did not confirm.");
        var passPath = Api + "/me/bookings/" + confirmed.BookingId + "/pass";
        var pass = (await server.Send("GET", passPath, bearer: winningToken)).Expect(HttpStatusCode.OK).Read<Pass>();
        Checks.Require(pass.EventId == version.EventId && pass.QrPayload.Length > 0, "Pass missing or incorrect.");
        (await server.Send("GET", passPath, bearer: tokens[1 - winner])).Expect(HttpStatusCode.NotFound);
        var bookings = (await server.Send("GET", Api + "/me/bookings?bucket=upcoming", bearer: winningToken)).Expect(HttpStatusCode.OK).Read<BookingPage>();
        Checks.Require(bookings.Items.Count(b => b.Id == pass.BookingId) == 1, "Expected exactly one confirmed booking.");
        Checks.Passed("concurrent last seat, reservation and payment replay, repeated webhook, booking and owner-only pass");

        var lateEvent = await MakeEvent(db);
        var lateHold = (await server.Send("POST", Api + "/booking-holds", new CreateHold(lateEvent.EventId, lateEvent.Revision), token, "late-seat")).Expect(HttpStatusCode.Created).Read<Hold>();
        var latePayment = await StartPayment(server, lateHold, token, HttpStatusCode.Created);
        await SendNoticeTwice(server, gateway, gateway.Paid(latePayment.PaymentId, lateHold.ExpiresAt.AddSeconds(1)));
        var refunded = (await server.Send("GET", Api + "/booking-holds/" + lateHold.Id, bearer: token)).Expect(HttpStatusCode.OK).Read<Hold>();
        Checks.Require(gateway.RefundCount == 1 && refunded.State == "refunded" && refunded.BookingId == null, "Late capture must generate exactly one refund and no booking.");
        Checks.Passed("late payment replay yields one full refund and no booking");
        var refreshed = (await server.Send("POST", Api + "/auth/session/refresh",
            new RefreshRequest(member.Tokens.RefreshToken))).Expect(HttpStatusCode.OK).Read<Tokens>();
        (await server.Send("GET", Api + "/auth/session", bearer: refreshed.AccessToken)).Expect(HttpStatusCode.OK);
        (await server.Send("POST", Api + "/auth/session/refresh",
            new RefreshRequest(member.Tokens.RefreshToken))).Expect(HttpStatusCode.Unauthorized);
        (await server.Send("DELETE", Api + "/auth/session", bearer: refreshed.AccessToken)).Expect(HttpStatusCode.NoContent);
        (await server.Send("GET", Api + "/auth/session", bearer: refreshed.AccessToken)).Expect(HttpStatusCode.Unauthorized);
        Checks.Passed("refresh rotates tokens, rejects reuse and logout revokes the session");
        await app.StopAsync();
    }

    private static async Task VerifyScreens(InProcessServer server, ClubDb db, string token)
    {
        var profile = (await server.Send("GET", Api + "/me", bearer: token)).Expect(HttpStatusCode.OK).Read<MyProfile>();
        Checks.Require(profile.FullName == "Ada Kaya", "Profile differs from seeded member.");
        (await server.Send("GET", Api + "/me/privacy", bearer: token)).Expect(HttpStatusCode.OK);
        var filters = (await server.Send("GET", Api + "/event-filters", bearer: token)).Expect(HttpStatusCode.OK).Read<Filters>();
        Checks.Require(filters.Sports.Count == 6, "Seed sports missing.");
        var events = (await server.Send("GET", Api + "/events?hasLocation=true&limit=100", bearer: token)).Expect(HttpStatusCode.OK).Read<EventPage>();
        Checks.Require(events.Items.Count > 0 && events.Items.All(e => e.Position != null), "Explore pins missing.");
        var id = events.Items[0].Id;
        (await server.Send("PUT", Api + "/me/saved-events/" + id, bearer: token)).Expect(HttpStatusCode.NoContent);
        var saved = (await server.Send("GET", Api + "/events?savedOnly=true", bearer: token)).Expect(HttpStatusCode.OK).Read<EventPage>();
        Checks.Require(saved.Items.Any(e => e.Id == id), "Save not persisted.");
        (await server.Send("DELETE", Api + "/me/saved-events/" + id, bearer: token)).Expect(HttpStatusCode.NoContent);
        saved = (await server.Send("GET", Api + "/events?savedOnly=true", bearer: token)).Expect(HttpStatusCode.OK).Read<EventPage>();
        Checks.Require(saved.Items.All(e => e.Id != id), "Unsave not persisted.");
        var trainers = (await server.Send("GET", Api + "/club/trainers", bearer: token)).Expect(HttpStatusCode.OK).Read<TrainerPage>();
        Checks.Require(trainers.Items.Count > 0, "Club trainers missing.");
        var trainer = trainers.Items[0].Id;
        (await server.Send("GET", Api + "/club/trainers/" + trainer, bearer: token)).Expect(HttpStatusCode.OK);
        (await server.Send("PUT", Api + "/me/followed-trainers/" + trainer, bearer: token)).Expect(HttpStatusCode.NoContent);
        var following = (await server.Send("GET", Api + "/club/trainers?followingOnly=true", bearer: token)).Expect(HttpStatusCode.OK).Read<TrainerPage>();
        Checks.Require(following.Items.Any(t => t.Id == trainer), "Follow not persisted.");
        (await server.Send("DELETE", Api + "/me/followed-trainers/" + trainer, bearer: token)).Expect(HttpStatusCode.NoContent);
        var introduction = (await server.Send("PUT", Api + "/me/trainer-introductions/" + trainer, bearer: token)).Expect(HttpStatusCode.OK).Read<Introduction>();
        var replay = (await server.Send("PUT", Api + "/me/trainer-introductions/" + trainer, bearer: token)).Expect(HttpStatusCode.OK).Read<Introduction>();
        Checks.Require(introduction == replay, "Introduction retry duplicated the request.");

        foreach (var bucket in new[] { "upcoming", "inProgress", "past" })
            (await server.Send("GET", Api + "/me/bookings?bucket=" + bucket, bearer: token)).Expect(HttpStatusCode.OK);
        var attended = await db.Set<VubesClub.Api.Persistence.BookingRow>()
            .FirstAsync(b => b.MemberId == profile.MemberId && b.Attendance == "attended");
        (await server.Send("PUT", Api + "/events/" + attended.EventId + "/me/rating", new RateRequest(5), token)).Expect(HttpStatusCode.OK);
        (await server.Send("PUT", Api + "/events/" + attended.EventId + "/me/rating", new RateRequest(6), token)).Expect(HttpStatusCode.UnprocessableEntity);
        var live = await db.Set<VubesClub.Api.Persistence.BookingRow>().Join(db.Set<EventVersion>(),
            b => b.VersionId, v => v.Id, (b, v) => new { b, v })
            .FirstAsync(x => x.b.MemberId == profile.MemberId && x.v.Starts <= DateTime.UtcNow && x.v.Ends > DateTime.UtcNow);
        var mapPath = Api + "/events/" + live.b.EventId;
        var map = (await server.Send("GET", mapPath + "/activity-map", bearer: token)).Expect(HttpStatusCode.OK).Read<ActivityMap>();
        Checks.Require(map.Route.Count > 0, "Activity route missing.");
        var sharing = (await server.Send("PUT", mapPath + "/me/location-sharing", new ShareRequest(true), token)).Expect(HttpStatusCode.OK).Read<Sharing>();
        Checks.Require(sharing.Enabled && sharing.SharingId.HasValue, "Location share did not open.");
        var position = new Point(41.05, 29.01);
        (await server.Send("PUT", mapPath + "/me/location", new LocationUpdate(sharing.SharingId!.Value, position, DateTime.UtcNow, 1), token)).Expect(HttpStatusCode.NoContent);
        map = (await server.Send("GET", mapPath + "/activity-map", bearer: token)).Expect(HttpStatusCode.OK).Read<ActivityMap>();
        Checks.Require(map.Participants.Any(p => p.DisplayName == profile.FullName && p.Position == position), "Location update not visible.");
        (await server.Send("PUT", mapPath + "/me/location-sharing", new ShareRequest(false), token)).Expect(HttpStatusCode.OK);
        map = (await server.Send("GET", mapPath + "/activity-map", bearer: token)).Expect(HttpStatusCode.OK).Read<ActivityMap>();
        Checks.Require(!map.MySharing.Enabled && map.Participants.All(p => p.DisplayName != profile.FullName), "Revocation left location visible.");
        Checks.Passed("all screen reads, saved events, follows, introduction replay, ratings, live route and location opt-in/update/revocation");
    }

    private static async Task<VerifiedSession> VerifyOtp(InProcessServer server, OtpRequest request)
    {
        var challenge = (await server.Send("POST", Api + "/auth/otp-challenges", request)).Expect(HttpStatusCode.Accepted).Read<Challenge>();
        Checks.Require(challenge.CodeLength == 4, "Expected four-digit OTP.");
        var path = Api + "/auth/otp-challenges/" + challenge.ChallengeId + "/verify";
        (await server.Send("POST", path, new VerifyRequest("0000"))).Expect(HttpStatusCode.UnprocessableEntity);
        return (await server.Send("POST", path, new VerifyRequest("1111"))).Expect(HttpStatusCode.OK).Read<VerifiedSession>();
    }

    private static async Task<EventVersion> MakeEvent(ClubDb db)
    {
        var now = DateTime.UtcNow;
        var trainer = await db.Set<Trainer>().FirstAsync();
        var sport = await db.Set<SportRow>().FirstAsync();
        var clubEvent = new ClubEvent { TrainerId = trainer.Id, Published = true, Capacity = 1, MinimumConfirmed = 1, Opens = now.AddDays(-1), Closes = now.AddDays(1) };
        db.Add(clubEvent);
        await db.SaveChangesAsync();
        var version = new EventVersion
        {
            EventId = clubEvent.Id, Revision = "v1", Title = "Kabul testi", Place = "İstanbul",
            Starts = now.AddDays(2), Ends = now.AddDays(2).AddHours(1), TimeZone = "Europe/Istanbul",
            SportId = sport.Id, Level = "easy", Amount = 10000, Currency = "TRY",
            Meeting = "Giriş", TermsVersion = "v1", TermsUrl = "https://example.invalid/terms"
        };
        db.Add(version);
        await db.SaveChangesAsync();
        clubEvent.CurrentVersionId = version.Id;
        await db.SaveChangesAsync();
        return version;
    }

    private static async Task<Payment> StartPayment(InProcessServer server, Hold hold, string token, HttpStatusCode status)
    {
        var input = new StartPayment(hold.Checkout.Terms.Version, true);
        var reply = await server.Send("POST", Api + "/booking-holds/" + hold.Id + "/payments", input, token, "order-" + hold.Id);
        return reply.Expect(status).Read<Payment>();
    }

    private static async Task SendNoticeTwice(InProcessServer server, TestGateway gateway, string notice)
    {
        string path = "/integrations/payments/" + gateway.Name + "/webhooks";
        foreach (var result in await Task.WhenAll(server.Send("POST", path, notice), server.Send("POST", path, notice)))
            result.Expect(HttpStatusCode.OK);
    }
}
