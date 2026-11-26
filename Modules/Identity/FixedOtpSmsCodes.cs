namespace VubesClub.Api.Modules.Identity;

public sealed class FixedOtpSmsCodes : ISmsCodes
{
    public Task<string> SendAsync(string phone, CancellationToken ct) => Task.FromResult("1111");
}
