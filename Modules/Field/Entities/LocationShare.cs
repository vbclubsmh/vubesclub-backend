namespace VubesClub.Api.Persistence;

public sealed class LocationShare
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Guid MemberId { get; set; }
    public Guid Alias { get; set; }
    public DateTime Expires { get; set; }
    public bool Revoked { get; set; }
}
