namespace VubesClub.Api.Persistence;

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public DateTime Starts { get; set; }
    public DateTime Ends { get; set; }
}
