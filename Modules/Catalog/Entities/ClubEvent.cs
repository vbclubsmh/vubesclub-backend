namespace VubesClub.Api.Persistence;

public sealed class ClubEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrainerId { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public bool Published { get; set; }
    public bool Cancelled { get; set; }
    public int Capacity { get; set; }
    public int MinimumConfirmed { get; set; }
    public DateTime Opens { get; set; }
    public DateTime Closes { get; set; }
    public DateTime? PremiumOpens { get; set; }
}
