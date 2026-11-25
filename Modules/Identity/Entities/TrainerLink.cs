namespace VubesClub.Api.Persistence;

public sealed class TrainerLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid TrainerId { get; set; }
}
