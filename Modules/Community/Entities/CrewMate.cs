namespace VubesClub.Api.Persistence;

public sealed class CrewMate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewId { get; set; }
    public Guid MemberId { get; set; }
}
