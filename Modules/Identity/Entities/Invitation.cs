namespace VubesClub.Api.Persistence;

public sealed class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Digest { get; set; }
    public Guid TrainerId { get; set; }
    public DateTime Expires { get; set; }
    public Guid? UsedBy { get; set; }
}
