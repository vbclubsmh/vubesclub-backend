namespace VubesClub.Api.Modules.Identity;

public interface ISmsCodes
{
    Task<string> SendAsync(string phone, CancellationToken ct);
}
