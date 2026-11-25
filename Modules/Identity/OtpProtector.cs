using System.Security.Cryptography;
using System.Text;
using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

public sealed class OtpProtector
{
    private readonly byte[]? key;

    public OtpProtector(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Security:OtpKey"];
        if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32)
            key = Encoding.UTF8.GetBytes(configured);
        else if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(configured))
            key = RandomNumberGenerator.GetBytes(32);
    }

    public string Digest(Guid id, string code)
    {
        if (key is null)
            throw new ApiFault(503, "OTP_UNAVAILABLE", "SMS doğrulama anahtarı yapılandırılmadı.");

        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id:N}:{code}")));
    }
}
