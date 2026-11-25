using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Identity;

public sealed class DevelopmentSmsCodes(IConfiguration configuration, IHostEnvironment environment) : ISmsCodes
{
    public Task<string> SendAsync(string phone, CancellationToken ct)
    {
        var allowed = configuration.GetSection("DevelopmentSms:Phones").Get<string[]>() ?? [];
        var code = configuration["DevelopmentSms:Code"];

        if (!environment.IsDevelopment() || !allowed.Contains(phone) ||
            !OtpFormat.IsValid(code))
            throw new ApiFault(503, "SMS_UNAVAILABLE", "SMS sağlayıcısı bu numara için etkin değil.");

        return Task.FromResult(code!);
    }
}
