namespace VubesClub.Api.Modules.Identity;

public sealed class FixedOtpSmsCodes : ISmsCodes
{
    public const string Code = "1111";

    public Task<OtpDispatch> SendAsync(OtpTarget target, CancellationToken ct) =>
        Task.FromResult(new OtpDispatch(Code, "test", Code.Length));
}
