using System.Security.Cryptography;

namespace VubesClub.Api.Modules.Identity;

/// <summary>
/// Kanal seçimi: test için sabit kod yalnızca izinli numaralarda (liste boşsa yalnızca yerel geliştirmede) geçerlidir;
/// diğer numaralara 6 haneli rastgele kod e-posta ile gider. SMS sağlayıcısı eklenince buradan seçilir.
/// </summary>
public sealed class OtpCodes(IConfiguration configuration, MailSender mail) : ISmsCodes
{
    public const int CodeLength = 6;
    public const int LifetimeMinutes = 15;

    public static bool FixedFor(IConfiguration configuration, string phone) =>
        configuration.GetValue<bool>("Identity:AllowFixedOtp") &&
        (configuration.GetSection("Identity:FixedOtpPhones").Get<string[]>() is not { Length: > 0 } allowed ||
         allowed.Contains(phone, StringComparer.Ordinal));

    public async Task<OtpDispatch> SendAsync(OtpTarget target, CancellationToken ct)
    {
        if (FixedFor(configuration, target.Phone))
            return await new FixedOtpSmsCodes().SendAsync(target, ct);

        if (!mail.IsConfigured)
            throw new VubesClub.Api.Infrastructure.ApiFault(503, "SMS_UNAVAILABLE", "Kod gönderim kanalı yapılandırılmadı.");

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
        // Bilinmeyen numara: kod üretilir ama gönderilmez; yanıt biçimi değişmez (numara sorgulanamaz).
        if (!string.IsNullOrWhiteSpace(target.Email))
        {
            var logo = configuration["Mail:LogoUrl"] ?? "https://api.vubesclub.com/media/Wordmark.png";
            await mail.SendAsync(target.Email, target.Name, OtpEmail.Subject(code),
                OtpEmail.Html(target.Name, code, LifetimeMinutes, logo),
                OtpEmail.Text(target.Name, code, LifetimeMinutes), ct);
        }

        return new OtpDispatch(code, "email", CodeLength);
    }
}
