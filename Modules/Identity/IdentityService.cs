using VubesClub.Api.Persistence;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace VubesClub.Api.Modules.Identity;

public sealed class IdentityService(
    ClubDb db,
    ISmsCodes delivery,
    OtpProtector protector,
    TimeProvider clock,
    IConfiguration configuration)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private bool UsesFixedOtp => configuration.GetValue<bool>("Identity:AllowFixedOtp");

    public Task<Challenge> CreateLoginChallenge(LoginOtp request, CancellationToken ct) =>
        CreateChallenge(request.Phone, null, null, null, ct);

    public async Task<Challenge> CreateApplicationChallenge(ApplicationOtp request, CancellationToken ct)
    {
        if (!request.AdultDeclared || string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 150 ||
            !IsPhone(request.Phone) ||
            !IsEmail(request.Email) || !RequiredText(request.University, 160) || !RequiredText(request.JobTitle, 120) ||
            !Fits(request.Workplace, 160) || !Fits(request.Instagram, 100) || !Fits(request.LinkedIn, 300))
            throw ApiFault.Invalid("Üyelik bilgilerini kontrol edin.");

        Guid? invitationId = null;
        var invitationCode = request.InvitationCode?.Trim();
        if (!string.IsNullOrEmpty(invitationCode))
        {
            var digest = Secrets.Digest(invitationCode.ToUpperInvariant());
            invitationId = await db.Set<Invitation>()
                .Where(i => i.Digest == digest && i.UsedBy == null && i.Expires > Now)
                .Select(i => (Guid?)i.Id)
                .SingleOrDefaultAsync(ct);

            if (invitationId is null) throw ApiFault.Invalid("Davet kodu geçersiz.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = request.Phone.Trim();
        if (await db.Set<Member>().AnyAsync(member =>
                member.Phone == normalizedPhone || member.Email == normalizedEmail, ct))
            throw ApiFault.Conflict("MEMBER_EXISTS", "Bu telefon veya e-posta ile bir üyelik kaydı zaten var.");

        return await CreateChallenge(normalizedPhone, invitationId, request.FullName, request, ct);
    }

    private async Task<Challenge> CreateChallenge(
        string phone, Guid? invitationId, string? fullName, ApplicationOtp? application, CancellationToken ct)
    {
        var normalizedPhone = phone?.Trim();
        if (string.IsNullOrEmpty(normalizedPhone) || !Regex.IsMatch(normalizedPhone, @"^\+[1-9][0-9]{7,14}$"))
            throw ApiFault.Invalid("Geçerli bir GSM numarası girin.");

        phone = normalizedPhone;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("otp:" + phone, ct);
        var now = Now;
        var recent = db.Set<OtpChallenge>().Where(c => c.Phone == phone && c.Created > now.AddMinutes(-5));
        if (await recent.AnyAsync(c => c.ResendAt > now, ct) || await recent.CountAsync(ct) >= 5 ||
            await recent.SumAsync(c => (int?)c.Attempts, ct) >= 5)
            throw Limit();

        await db.Set<OtpChallenge>().Where(c => c.Phone == phone && !c.Consumed)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Consumed, true), ct);
        var challenge = new OtpChallenge
        {
            Phone = phone,
            InviteId = invitationId,
            FullName = fullName?.Trim(),
            Email = application?.Email.Trim().ToLowerInvariant(),
            University = application?.University.Trim(),
            JobTitle = application?.JobTitle.Trim(),
            Workplace = EmptyToNull(application?.Workplace),
            Instagram = EmptyToNull(application?.Instagram),
            LinkedIn = EmptyToNull(application?.LinkedIn),
            Digest = "", Created = now
        };
        db.Add(challenge);
        await Deliver(challenge, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(challenge);
    }

    public async Task<Challenge> Resend(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var challenge = await LockedChallenge(id, ct);
        var now = Now;
        if (challenge.ResendAt > now || challenge.Attempts >= 5) throw Limit();
        if (challenge.Consumed || challenge.Expires <= now)
            throw new ApiFault(410, "OTP_EXPIRED", "Kodun süresi doldu.");
        await Deliver(challenge, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(challenge);
    }

    public async Task<IResult> Verify(Guid id, string code, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var challenge = await LockedChallenge(id, ct);
        var now = Now;
        if (challenge.Attempts >= 5) throw Limit();
        if (challenge.Consumed || challenge.Expires <= now)
            throw new ApiFault(410, "OTP_EXPIRED", "Kod kullanılmış veya süresi dolmuş.");

        var codeMatches = UsesFixedOtp
            ? string.Equals(code, "1111", StringComparison.Ordinal)
            : code?.Length == 4 && code.All(char.IsAsciiDigit) && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.Digest), Convert.FromHexString(protector.Digest(id, code)));
        if (!codeMatches)
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw ApiFault.Invalid("Doğrulama kodu geçersiz.");
        }

        var member = await db.Set<Member>().SingleOrDefaultAsync(m => m.Phone == challenge.Phone, ct);
        var applicationCreated = false;
        if (member is null && challenge.Email is not null)
        {
            Invitation? invite = null;
            if (challenge.InviteId is Guid inviteId)
            {
                await db.LockAsync("invitation:" + inviteId, ct);
                invite = await db.Set<Invitation>().SingleAsync(i => i.Id == inviteId, ct);
                if (invite.UsedBy != null || invite.Expires <= now)
                    throw ApiFault.Conflict("INVITATION_USED", "Davet kullanılamıyor.");
            }

            member = new Member
            {
                Phone = challenge.Phone,
                Email = challenge.Email,
                FullName = challenge.FullName!,
                Status = "pending",
                Visibility = "private"
            };
            db.Add(member);
            if (invite is not null) invite.UsedBy = member.Id;
            await db.SaveChangesAsync(ct);
            applicationCreated = true;

            db.Add(new ApplicationEntry
            {
                MemberId = member.Id,
                InvitationId = challenge.InviteId,
                AdultDeclaredAt = now,
                University = challenge.University!,
                JobTitle = challenge.JobTitle!,
                Workplace = challenge.Workplace,
                Instagram = challenge.Instagram,
                LinkedIn = challenge.LinkedIn,
                SubmittedAt = now
            });
        }

        if (member is not null && member.Status != "active" && !applicationCreated)
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw ApiFault.Forbidden();
        }

        IResult result;
        if (member is null)
        {
            result = Results.Ok(new InvitationRequired("invitationRequired"));
        }
        else if (applicationCreated)
        {
            result = Results.Ok(new ApplicationReceived("applicationReceived"));
        }
        else
        {
            await db.Set<MobileSession>().Where(s => s.MemberId == member.Id && !s.Revoked)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Revoked, true), ct);
            var session = new MobileSession
            {
                MemberId = member.Id, Expires = now.AddDays(30), AccessDigest = "", RefreshDigest = ""
            };
            var tokens = Rotate(session, now);
            db.Add(session);
            result = Results.Ok(new VerifiedSession(Describe(member.Status, session.Expires), tokens));
        }

        challenge.Consumed = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<Tokens> Refresh(string token, CancellationToken ct)
    {
        if (token is not { Length: 64 }) throw ApiFault.Unauthorized();
        var digest = Secrets.Digest(token);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("refresh:" + digest, ct);
        var session = await db.Set<MobileSession>().SingleOrDefaultAsync(s => s.RefreshDigest == digest, ct);
        if (session is null || session.Revoked || session.Expires <= Now) throw ApiFault.Unauthorized();

        var tokens = Rotate(session, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return tokens;
    }

    public static Session Describe(string status, DateTime expires) => new(status, status switch
    {
        "active" => "events",
        "pending" => "review",
        "suspended" => "restricted",
        _ => "applicationClosed"
    }, expires);

    private async Task<OtpChallenge> LockedChallenge(Guid id, CancellationToken ct)
    {
        var phone = await db.Set<OtpChallenge>().Where(c => c.Id == id).Select(c => c.Phone)
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.Missing();
        await db.LockAsync("otp:" + phone, ct);
        return await db.Set<OtpChallenge>().SingleAsync(c => c.Id == id, ct);
    }

    private async Task Deliver(OtpChallenge challenge, DateTime now, CancellationToken ct)
    {
        var code = await delivery.SendAsync(challenge.Phone, ct);
        challenge.Digest = protector.Digest(challenge.Id, code);
        challenge.Expires = now.AddMinutes(5);
        challenge.ResendAt = now.AddMinutes(1);
    }

    private static Tokens Rotate(MobileSession session, DateTime now)
    {
        var access = Secrets.RandomToken();
        var refresh = Secrets.RandomToken();
        session.AccessDigest = Secrets.Digest(access);
        session.RefreshDigest = Secrets.Digest(refresh);
        session.AccessExpires = new[] { now.AddMinutes(15), session.Expires }.Min();
        return new Tokens(access, session.AccessExpires, refresh, session.Expires);
    }

    private static Challenge View(OtpChallenge c) => new(c.Id, 4, c.Expires, c.ResendAt);

    private static bool IsEmail(string? email) =>
        email is { Length: <= 254 } && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) &&
        string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);

    private static bool IsPhone(string? phone) =>
        phone is not null && Regex.IsMatch(phone.Trim(), @"^\+[1-9][0-9]{7,14}$");

    private static bool RequiredText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static bool Fits(string? value, int maxLength) => value is null || value.Trim().Length <= maxLength;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApiFault Limit() => new(429, "OTP_LIMIT", "Kod isteği veya deneme sınırına ulaşıldı.");
}
