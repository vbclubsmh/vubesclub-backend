namespace VubesClub.Api.Persistence;

public sealed class IntroductionRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid TrainerId { get; set; }
    public required string State { get; set; }
    public DateTime RequestedAt { get; set; }
}
