namespace VubesClub.Api.Persistence;

public sealed class MobileSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public required string AccessDigest { get; set; }
    public required string RefreshDigest { get; set; }
    public DateTime AccessExpires { get; set; }
    public DateTime Expires { get; set; }
    public bool Revoked { get; set; }
}
