using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

public sealed class UnconfiguredSmsCodes : ISmsCodes
{
    public Task<string> SendAsync(string phone, CancellationToken ct) =>
        throw new ApiFault(503, "SMS_UNAVAILABLE", "SMS sağlayıcısı yapılandırılmadı.");
}
