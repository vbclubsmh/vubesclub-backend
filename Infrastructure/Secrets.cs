using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Infrastructure;

public static class Secrets
{
    public static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
