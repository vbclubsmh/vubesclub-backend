using System.Security.Cryptography;
using System.Text;

namespace VubesClub.Api.Infrastructure;

public sealed class ApiFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static ApiFault Invalid(string message) => new(422, "INVALID_INPUT", message);
    public static ApiFault Missing() => new(404, "NOT_FOUND", "Kayıt bulunamadı.");
    public static ApiFault Forbidden() => new(403, "FORBIDDEN", "Bu işlem için erişim yok.");
    public static ApiFault Unauthorized() => new(401, "SESSION_INVALID", "Oturum geçersiz.");
    public static ApiFault Conflict(string code, string message) => new(409, code, message);
}
