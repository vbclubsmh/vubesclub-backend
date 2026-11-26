using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VubesClub.Api.Contracts;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Persistence;

namespace VubesClub.Api.Modules.Identity;

/// <summary>Üyenin kendi profilini güncellemesi; e-posta ve telefon yalnızca kodla doğrulanarak değişir.</summary>
public sealed class ProfileService(
    ClubDb db, OtpProtector protector, IMailSender mail, TimeProvider clock, IConfiguration configuration)
{
    public static readonly string[] ClothingSizes = ["XS", "S", "M", "L", "XL", "XXL"];

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now);

    public static int AgeOn(DateOnly birth, DateOnly today)
    {
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        return age;
    }

    /// <summary>18 yaş ve üzeri, 100 yaşından küçük.</summary>
    public static bool ValidBirthDate(DateOnly birth, DateOnly today) => AgeOn(birth, today) is >= 18 and <= 100;

    public static bool ValidHeight(int? value) => value is null or (>= 100 and <= 250);
    public static bool ValidWeight(int? value) => value is null or (>= 30 and <= 300);

    public async Task<Member> Update(Guid memberId, UpdateProfile request, CancellationToken ct)
    {
        if (!ValidHeight(request.HeightCm)) throw ApiFault.Invalid("Boy 100 ile 250 cm arasında olmalı.");
        if (!ValidWeight(request.WeightKg)) throw ApiFault.Invalid("Kilo 30 ile 300 kg arasında olmalı.");
        if (request.ClothingSize is not null && !ClothingSizes.Contains(request.ClothingSize))
            throw ApiFault.Invalid("Beden geçerli değil.");

        var member = await db.Set<Member>().SingleAsync(m => m.Id == memberId, ct);
        if (request.BirthDate is { } birth)
        {
            if (member.BirthDate is not null && member.BirthDate != birth)
                throw ApiFault.Conflict("BIRTH_DATE_LOCKED", "Doğum tarihi sonradan değiştirilemez.");
            if (!ValidBirthDate(birth, Today)) throw ApiFault.Invalid("Doğum tarihi geçerli değil. Üyelik için 18 yaşını doldurmuş olmalısın.");
            member.BirthDate = birth;
        }

        if (request.HeightCm is { } height) member.HeightCm = height;
        if (request.WeightKg is { } weight) member.WeightKg = weight;
        if (request.ClothingSize is { } size) member.ClothingSize = size;
        await db.SaveChangesAsync(ct);
        return member;
    }

    public async Task<Challenge> RequestContactChange(Guid memberId, ContactChangeRequest request, CancellationToken ct)
    {
        var wantsEmail = !string.IsNullOrWhiteSpace(request.Email);
        var wantsPhone = !string.IsNullOrWhiteSpace(request.Phone);
        if (wantsEmail == wantsPhone) throw ApiFault.Invalid("Aynı anda yalnızca e-posta ya da telefon değiştirebilirsin.");

        var member = await db.Set<Member>().SingleAsync(m => m.Id == memberId, ct);
        if (OtpCodes.IsDemo(configuration, member.Phone))
            throw new ApiFault(403, "DEMO_ACCOUNT", "Demo hesabının iletişim bilgileri değiştirilemez.");

        string? newEmail = null, newPhone = null, destination;
        OtpPurpose purpose;
        if (wantsEmail)
        {
            newEmail = request.Email!.Trim().ToLowerInvariant();
            if (!IsEmail(newEmail)) throw ApiFault.Invalid("E-posta adresi geçerli değil.");
            if (newEmail == member.Email) throw ApiFault.Invalid("Bu zaten kayıtlı e-posta adresin.");
            if (await db.Set<Member>().AnyAsync(m => m.Email == newEmail && m.Id != memberId, ct))
                throw ApiFault.Conflict("EMAIL_TAKEN", "Bu e-posta adresi başka bir üyelikte kullanılıyor.");
            destination = newEmail;
            purpose = OtpPurpose.EmailChange;
        }
        else
        {
            newPhone = request.Phone!.Trim();
            if (!Regex.IsMatch(newPhone, @"^\+[1-9][0-9]{7,14}$")) throw ApiFault.Invalid("Geçerli bir GSM numarası girin.");
            if (newPhone == member.Phone) throw ApiFault.Invalid("Bu zaten kayıtlı telefon numaran.");
            if (await db.Set<Member>().AnyAsync(m => m.Phone == newPhone && m.Id != memberId, ct))
                throw ApiFault.Conflict("PHONE_TAKEN", "Bu telefon numarası başka bir üyelikte kullanılıyor.");
            destination = member.Email ??
                throw ApiFault.Invalid("Telefon numaranı değiştirmek için önce e-posta adresini eklemelisin.");
            purpose = OtpPurpose.PhoneChange;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("contact:" + memberId, ct);
        var now = Now;
        var recent = db.Set<ContactChange>().Where(c => c.MemberId == memberId && c.Created > now.AddMinutes(-OtpCodes.LifetimeMinutes));
        if (await recent.AnyAsync(c => c.ResendAt > now, ct) || await recent.CountAsync(ct) >= 5 ||
            await recent.SumAsync(c => (int?)c.Attempts, ct) >= 5)
            throw Limit();

        var change = new ContactChange { MemberId = memberId, NewEmail = newEmail, NewPhone = newPhone, Digest = "", Created = now };
        db.Add(change);
        var dispatch = await Deliver(change, member, destination, purpose, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new Challenge(change.Id, dispatch.Length, change.Expires, change.ResendAt, dispatch.Channel);
    }

    public async Task<Challenge> Resend(Guid memberId, Guid changeId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("contact:" + memberId, ct);
        var change = await db.Set<ContactChange>().SingleOrDefaultAsync(c => c.Id == changeId && c.MemberId == memberId, ct)
            ?? throw ApiFault.Missing();
        var now = Now;
        if (change.ResendAt > now || change.Attempts >= 5) throw Limit();
        if (change.Consumed || change.Expires <= now) throw new ApiFault(410, "OTP_EXPIRED", "Kodun süresi doldu.");

        var member = await db.Set<Member>().SingleAsync(m => m.Id == memberId, ct);
        var destination = change.NewEmail ?? member.Email!;
        var purpose = change.NewEmail is not null ? OtpPurpose.EmailChange : OtpPurpose.PhoneChange;
        var dispatch = await Deliver(change, member, destination, purpose, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new Challenge(change.Id, dispatch.Length, change.Expires, change.ResendAt, dispatch.Channel);
    }

    public async Task<Member> Confirm(Guid memberId, Guid changeId, string? code, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("contact:" + memberId, ct);
        var change = await db.Set<ContactChange>().SingleOrDefaultAsync(c => c.Id == changeId && c.MemberId == memberId, ct)
            ?? throw ApiFault.Missing();
        var now = Now;
        if (change.Attempts >= 5) throw Limit();
        if (change.Consumed || change.Expires <= now)
            throw new ApiFault(410, "OTP_EXPIRED", "Kod kullanılmış veya süresi dolmuş.");

        var member = await db.Set<Member>().SingleAsync(m => m.Id == memberId, ct);
        var fixedCode = OtpCodes.FixedFor(configuration, member.Phone);
        var matches = fixedCode
            ? string.Equals(code, FixedOtpSmsCodes.Code, StringComparison.Ordinal)
            : code?.Length == OtpCodes.CodeLength && code.All(char.IsAsciiDigit) && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(change.Digest), Convert.FromHexString(protector.Digest(change.Id, code)));
        if (!matches)
        {
            change.Attempts++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new ApiFault(422, "OTP_INVALID", "Doğrulama kodu geçersiz.");
        }

        if (change.NewEmail is not null)
        {
            if (await db.Set<Member>().AnyAsync(m => m.Email == change.NewEmail && m.Id != memberId, ct))
                throw ApiFault.Conflict("EMAIL_TAKEN", "Bu e-posta adresi başka bir üyelikte kullanılıyor.");
            member.Email = change.NewEmail;
        }
        else
        {
            if (await db.Set<Member>().AnyAsync(m => m.Phone == change.NewPhone && m.Id != memberId, ct))
                throw ApiFault.Conflict("PHONE_TAKEN", "Bu telefon numarası başka bir üyelikte kullanılıyor.");
            member.Phone = change.NewPhone!;
        }

        change.Consumed = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return member;
    }

    private async Task<OtpDispatch> Deliver(
        ContactChange change, Member member, string destination, OtpPurpose purpose, DateTime now, CancellationToken ct)
    {
        OtpDispatch dispatch;
        if (OtpCodes.FixedFor(configuration, member.Phone))
        {
            dispatch = new OtpDispatch(FixedOtpSmsCodes.Code, "test", FixedOtpSmsCodes.Code.Length);
        }
        else
        {
            if (!mail.IsConfigured) throw new ApiFault(503, "EMAIL_UNAVAILABLE", "E-posta gönderimi yapılandırılmadı.");
            var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OtpCodes.CodeLength)).ToString($"D{OtpCodes.CodeLength}");
            var logo = configuration["Mail:LogoUrl"] ?? "https://api.vubesclub.com/media/Wordmark.png";
            await mail.SendAsync(destination, member.FullName, OtpEmail.Subject(code, purpose),
                OtpEmail.Html(member.FullName, code, OtpCodes.LifetimeMinutes, logo, purpose),
                OtpEmail.Text(member.FullName, code, OtpCodes.LifetimeMinutes, purpose), ct);
            dispatch = new OtpDispatch(code, "email", OtpCodes.CodeLength);
        }

        change.Digest = protector.Digest(change.Id, dispatch.Code);
        change.Expires = now.AddMinutes(OtpCodes.LifetimeMinutes);
        change.ResendAt = now.AddMinutes(1);
        return dispatch;
    }

    private static bool IsEmail(string email) =>
        email.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) &&
        string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);

    private static ApiFault Limit() => new(429, "OTP_LIMIT", "Kod isteği veya deneme sınırına ulaşıldı.");
}
