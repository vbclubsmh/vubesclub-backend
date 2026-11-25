namespace VubesClub.Api.Persistence;

public sealed class CrewRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrainerId { get; set; }
    public required string Name { get; set; }
}
