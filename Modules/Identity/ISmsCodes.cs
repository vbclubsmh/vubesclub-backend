namespace VubesClub.Api.Modules.Identity;

/// <summary>Kodun kime gideceği. E-posta yoksa (bilinmeyen numara) hiçbir şey gönderilmez.</summary>
public sealed record OtpTarget(string Phone, string? Email, string? Name);

/// <summary>Üretilen kod, kanal ("email", "sms" ya da "test") ve hane sayısı.</summary>
public sealed record OtpDispatch(string Code, string Channel, int Length);

/// <summary>Giriş kodunu üretir ve ilgili kanaldan iletir. SMS sağlayıcısı gelince yeni bir uygulama eklenir.</summary>
public interface ISmsCodes
{
    Task<OtpDispatch> SendAsync(OtpTarget target, CancellationToken ct);
}
