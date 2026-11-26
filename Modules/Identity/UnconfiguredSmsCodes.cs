using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

public sealed class UnconfiguredSmsCodes : ISmsCodes
{
    public Task<OtpDispatch> SendAsync(OtpTarget target, CancellationToken ct) =>
        throw new ApiFault(503, "SMS_UNAVAILABLE", "Kod gönderim kanalı yapılandırılmadı.");
}
