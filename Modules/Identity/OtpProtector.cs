using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Modules.Identity;

public sealed class OtpProtector
{
    private readonly byte[] key;

    public OtpProtector(IConfiguration configuration)
    {
        var configured = configuration["Security:OtpKey"];
        if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32)
            key = Encoding.UTF8.GetBytes(configured);
        else
            throw new InvalidOperationException("Security:OtpKey must contain at least 32 UTF-8 bytes.");
    }

    public string Digest(Guid id, string code)
    {
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id:N}:{code}")));
    }
}
