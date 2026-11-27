using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Payments;
using VubesClub.Api.Modules.Identity;
using VubesClub.Api.Persistence;

namespace VubesClub.Verification;

internal sealed class TestSmsCodes : VubesClub.Api.Modules.Identity.ISmsCodes
{
    public Task<VubesClub.Api.Modules.Identity.OtpDispatch> SendAsync(
        VubesClub.Api.Modules.Identity.OtpTarget target, CancellationToken ct) =>
        Task.FromResult(new VubesClub.Api.Modules.Identity.OtpDispatch("111111", "test", 6));
}

/// <summary>Gönderilen e-postaları saklar; testler kodu konu satırından okur.</summary>
internal sealed class Mailbox : VubesClub.Api.Modules.Identity.IMailSender
{
    public bool IsConfigured => true;
    public string? LastTo { get; private set; }
    public string? LastSubject { get; private set; }
    public int Count { get; private set; }

    public string LastCode => System.Text.RegularExpressions.Regex.Match(LastSubject ?? "", @"(\d{6})$").Groups[1].Value;

    public Task SendAsync(string to, string? toName, string subject, string html, string text, CancellationToken ct)
    {
        LastTo = to; LastSubject = subject; Count++;
        return Task.CompletedTask;
    }
}

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
        var mailbox = new Mailbox();
        await using var app = ClubHost.Build(
            ["--environment=Development", "--ConnectionStrings:Club=" + connection,
             "--Security:OtpKey=" + Secrets.RandomToken(), "--Identity:AllowFixedOtp=false"],
            builder =>
            {
                builder.Services.AddSingleton<IServer>(server);
                builder.Services.AddSingleton<ISmsCodes, TestSmsCodes>();
                builder.Services.AddSingleton<VubesClub.Api.Modules.Identity.IMailSender>(mailbox);
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

        var member = await VerifyOtp(server, new LoginOtp(MemberPhone));
        await VerifyApplicationOtp(server, new ApplicationOtp(ApplicantPhone, "Test Üyesi", "test@example.com",
            "Test University", "Engineer", 178, 72, new DateOnly(1995, 3, 14), null, null, null, "AFTERGLOW", true));
        var applicationMember = await db.Set<Member>().SingleAsync(member => member.Phone == ApplicantPhone);
        Checks.Require(applicationMember.BirthDate == new DateOnly(1995, 3, 14) && applicationMember.HeightCm == 178 && applicationMember.WeightKg == 72,
            "Application body data must be stored on the member.");
        foreach (var birth in new[] { default(DateOnly), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120) })
            (await server.Send("POST", Api + "/auth/application-otp-challenges", new ApplicationOtp("+905557770009", "Genç Aday", "genc@example.com",
                "Okul", "Öğrenci", 170, 60, birth, null, null, null, "AFTERGLOW", true))).Expect(HttpStatusCode.UnprocessableEntity);
        Checks.Require(applicationMember.Status == "pending" && applicationMember.Email == "test@example.com",
            "A verified application must create a pending member with its email.");
        Checks.Require(await db.Set<ApplicationEntry>().AnyAsync(entry => entry.MemberId == applicationMember.Id &&
            entry.InvitationId != null && entry.University == "Test University"), "Application profile data was not persisted.");
        await db.Set<Member>().Where(m => m.Phone == ApplicantPhone).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "active"));
        // The 60 second resend window of the application code would otherwise throttle the first sign-in.
        await db.Set<OtpChallenge>().Where(c => c.Phone == ApplicantPhone)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ResendAt, DateTime.UtcNow.AddMinutes(-1)));
        var applicant = await VerifyOtp(server, new LoginOtp(ApplicantPhone));
        Checks.Passed("seed replay, application intake, approval gate and approved sign-in");

        await VerifyScreens(server, db, member.Tokens.AccessToken);
        await VerifyProfile(server, db, member.Tokens.AccessToken, mailbox);

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

        var checkInPath = Api + "/field/check-ins";
        var scanBody = new CheckInRequest(pass.QrPayload);
        (await server.Send("POST", checkInPath, scanBody, winningToken)).Expect(HttpStatusCode.Forbidden);
        var scannerId = (await db.Set<VubesClub.Api.Persistence.BookingRow>().AsNoTracking().SingleAsync(b => b.Id == pass.BookingId)).MemberId;
        var staffToken = tokens[1 - winner];
        var staffId = (await server.Send("GET", Api + "/me", bearer: staffToken)).Expect(HttpStatusCode.OK).Read<MyProfile>().MemberId;
        Checks.Require(staffId != scannerId, "Test members must differ.");
        await db.Set<Member>().Where(m => m.Id == staffId).ExecuteUpdateAsync(s => s.SetProperty(m => m.FieldStaff, true));
        (await server.Send("POST", checkInPath, new CheckInRequest("garbage"), staffToken)).Expect((HttpStatusCode)422);
        var scanned = (await server.Send("POST", checkInPath, scanBody, staffToken)).Expect(HttpStatusCode.OK).Read<CheckInResult>();
        Checks.Require(scanned.Outcome == "checkedIn" && scanned.BookingId == pass.BookingId, "QR check-in did not match the member.");
        var scannedAgain = (await server.Send("POST", checkInPath, scanBody, staffToken)).Expect(HttpStatusCode.OK).Read<CheckInResult>();
        Checks.Require(scannedAgain.Outcome == "alreadyCheckedIn", "Second scan must not check in twice.");
        Checks.Require((await db.Set<VubesClub.Api.Persistence.BookingRow>().AsNoTracking().SingleAsync(b => b.Id == pass.BookingId)).Attendance == "checkedIn", "Attendance was not recorded.");
        Checks.Passed("stand QR check-in matches the member once and is restricted to field staff");

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

    private static async Task VerifyProfile(InProcessServer server, ClubDb db, string token, Mailbox mailbox)
    {
        var me = Api + "/me";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var birth = today.AddYears(-30);

        (await server.Send("PATCH", me, new UpdateProfile(5, null, null, null), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("PATCH", me, new UpdateProfile(null, 500, null, null), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("PATCH", me, new UpdateProfile(null, null, "XXXL", null), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("PATCH", me, new UpdateProfile(null, null, null, today.AddYears(-17)), token)).Expect(HttpStatusCode.UnprocessableEntity);
        var updated = (await server.Send("PATCH", me, new UpdateProfile(181, 79, "L", birth), token)).Expect(HttpStatusCode.OK).Read<MyProfile>();
        Checks.Require(updated.HeightCm == 181 && updated.WeightKg == 79 && updated.ClothingSize == "L" && updated.BirthDate == birth,
            "Profile update was not stored.");
        (await server.Send("PATCH", me, new UpdateProfile(null, null, null, birth.AddDays(1)), token)).Expect(HttpStatusCode.Conflict);
        (await server.Send("PATCH", me, new UpdateProfile(null, 80, null, birth), token)).Expect(HttpStatusCode.OK);
        var partial = (await server.Send("GET", me, bearer: token)).Expect(HttpStatusCode.OK).Read<MyProfile>();
        Checks.Require(partial.WeightKg == 80 && partial.HeightCm == 181 && partial.ClothingSize == "L", "A partial update must keep other fields.");

        async Task OpenWindow() => await db.Set<ContactChange>().ExecuteUpdateAsync(s => s.SetProperty(c => c.ResendAt, DateTime.UtcNow.AddMinutes(-1)));
        var contact = me + "/contact-changes";

        // e-posta: kod yeni adrese gider
        (await server.Send("POST", contact, new ContactChangeRequest(null, null), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", contact, new ContactChangeRequest("a@example.com", "+905559990001"), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", contact, new ContactChangeRequest("bozuk", null), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", contact, new ContactChangeRequest("test@example.com", null), token)).Expect(HttpStatusCode.Conflict);
        (await server.Send("POST", contact, new ContactChangeRequest(null, "+905559990001"), token)).Expect(HttpStatusCode.UnprocessableEntity);
        var before = mailbox.Count;
        var challenge = (await server.Send("POST", contact, new ContactChangeRequest("Yeni.Adres@Example.com", null), token)).Expect(HttpStatusCode.Accepted).Read<Challenge>();
        Checks.Require(mailbox.Count == before + 1 && mailbox.LastTo == "yeni.adres@example.com" && mailbox.LastSubject!.Contains("e-posta doğrulama"),
            "The email-change code must be sent to the new address.");
        (await server.Send("POST", contact, new ContactChangeRequest("baska@example.com", null), token)).Expect(HttpStatusCode.TooManyRequests);
        var verify = contact + "/" + challenge.ChallengeId + "/verify";
        (await server.Send("POST", verify, new VerifyRequest("000000"), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", verify, new VerifyRequest(null!), token)).Expect(HttpStatusCode.UnprocessableEntity);
        var withEmail = (await server.Send("POST", verify, new VerifyRequest(mailbox.LastCode), token)).Expect(HttpStatusCode.OK).Read<MyProfile>();
        Checks.Require(withEmail.Email == "yeni.adres@example.com", "Verified email change was not applied.");
        (await server.Send("POST", verify, new VerifyRequest(mailbox.LastCode), token)).Expect(HttpStatusCode.Gone);
        (await server.Send("POST", contact + "/" + Guid.NewGuid() + "/verify", new VerifyRequest("123456"), token)).Expect(HttpStatusCode.NotFound);

        // telefon: kod mevcut e-postaya gider; başka üyenin numarası reddedilir
        await OpenWindow();
        (await server.Send("POST", contact, new ContactChangeRequest(null, ApplicantPhone), token)).Expect(HttpStatusCode.Conflict);
        (await server.Send("POST", contact, new ContactChangeRequest(null, "05559990001"), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", contact, new ContactChangeRequest(null, MemberPhone), token)).Expect(HttpStatusCode.UnprocessableEntity);
        var phoneChallenge = (await server.Send("POST", contact, new ContactChangeRequest(null, "+905559990001"), token)).Expect(HttpStatusCode.Accepted).Read<Challenge>();
        Checks.Require(mailbox.LastTo == "yeni.adres@example.com" && mailbox.LastSubject!.Contains("telefon"), "The phone-change code must go to the current email.");
        var phoneVerify = contact + "/" + phoneChallenge.ChallengeId;
        await OpenWindow();
        (await server.Send("POST", phoneVerify + "/resend", null, token)).Expect(HttpStatusCode.OK);
        var withPhone = (await server.Send("POST", phoneVerify + "/verify", new VerifyRequest(mailbox.LastCode), token)).Expect(HttpStatusCode.OK).Read<MyProfile>();
        Checks.Require(withPhone.Phone == "+905559990001" && withPhone.PhoneMasked.EndsWith("0001"), "Verified phone change was not applied.");

        // beş yanlış deneme kodu kilitler
        await OpenWindow();
        var locked = (await server.Send("POST", contact, new ContactChangeRequest("kilit@example.com", null), token)).Expect(HttpStatusCode.Accepted).Read<Challenge>();
        for (var i = 0; i < 5; i++)
            (await server.Send("POST", contact + "/" + locked.ChallengeId + "/verify", new VerifyRequest("000000"), token)).Expect(HttpStatusCode.UnprocessableEntity);
        (await server.Send("POST", contact + "/" + locked.ChallengeId + "/verify", new VerifyRequest(mailbox.LastCode), token)).Expect(HttpStatusCode.TooManyRequests);

        // numara eski hâline döner; sonraki testler seed numarasını kullanır
        await db.Set<Member>().Where(m => m.Phone == "+905559990001").ExecuteUpdateAsync(s => s.SetProperty(m => m.Phone, MemberPhone));
        await db.Set<Member>().Where(m => m.Phone == MemberPhone).ExecuteUpdateAsync(s => s.SetProperty(m => m.Email, (string?)null));
        Checks.Passed("profile update, birth-date lock, email/phone change with codes, conflicts, resend and lockout");
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
        async Task<ClubFeed> Feed() => (await server.Send("GET", Api + "/club/feed", bearer: token)).Expect(HttpStatusCode.OK).Read<ClubFeed>();
        var feed = await Feed();
        Checks.Require(feed.Trainers.Any(t => t.Trainer.Id == trainer && t.Source == "following"), "Feed misses the followed trainer.");
        Checks.Require(feed.ForYou.Count > 0 && feed.ForYou.All(e => e.Organizer.Id == trainer), "Feed shows events of other trainers.");
        db.Set<TrainerLink>().Add(new TrainerLink { MemberId = profile.MemberId, TrainerId = trainer });
        await db.SaveChangesAsync();
        Checks.Require((await Feed()).Trainers.Any(t => t.Trainer.Id == trainer && t.Source == "both"), "Feed ignores the Studio link.");
        (await server.Send("DELETE", Api + "/me/followed-trainers/" + trainer, bearer: token)).Expect(HttpStatusCode.NoContent);
        Checks.Require((await Feed()).Trainers.Any(t => t.Trainer.Id == trainer && t.Source == "linked"), "A Studio link alone must show the trainer.");
        await db.Set<TrainerLink>().Where(l => l.MemberId == profile.MemberId && l.TrainerId == trainer).ExecuteDeleteAsync();
        Checks.Require((await Feed()).Trainers.All(t => t.Trainer.Id != trainer), "Feed kept an unlinked, unfollowed trainer.");
        Checks.Require((await Feed()).ForYou.Count > 0, "A member without trainers must still see the Club events.");
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

    private static async Task<VerifiedSession> VerifyOtp(InProcessServer server, LoginOtp request)
    {
        var challenge = (await server.Send("POST", Api + "/auth/otp-challenges", request)).Expect(HttpStatusCode.Accepted).Read<Challenge>();
        Checks.Require(challenge.CodeLength == 6 && challenge.Channel == "test", "Expected six-digit OTP.");
        var path = Api + "/auth/otp-challenges/" + challenge.ChallengeId + "/verify";
        (await server.Send("POST", path, new VerifyRequest("000000"))).Expect(HttpStatusCode.UnprocessableEntity);
        return (await server.Send("POST", path, new VerifyRequest("111111"))).Expect(HttpStatusCode.OK).Read<VerifiedSession>();
    }

    private static async Task VerifyApplicationOtp(InProcessServer server, ApplicationOtp request)
    {
        var challenge = (await server.Send("POST", Api + "/auth/application-otp-challenges", request))
            .Expect(HttpStatusCode.Accepted).Read<Challenge>();
        Checks.Require(challenge.CodeLength == 6, "Expected six-digit invitation OTP.");
        var path = Api + "/auth/otp-challenges/" + challenge.ChallengeId + "/verify";
        (await server.Send("POST", path, new VerifyRequest("000000"))).Expect(HttpStatusCode.UnprocessableEntity);
        var result = (await server.Send("POST", path, new VerifyRequest("111111")))
            .Expect(HttpStatusCode.OK).Read<ApplicationReceived>();
        Checks.Require(result.NextAction == "applicationReceived", "Application verification did not finish in review state.");
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
