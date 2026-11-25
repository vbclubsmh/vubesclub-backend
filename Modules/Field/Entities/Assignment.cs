namespace VubesClub.Api.Persistence;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid EventId { get; set; }
    public DateTime Starts { get; set; }
    public DateTime Ends { get; set; }
}
