namespace VubesClub.Api.Persistence;

public sealed class ProgramSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DayId { get; set; }
    public int Ordinal { get; set; }
    public DateTime Starts { get; set; }
    public DateTime? Ends { get; set; }
    public required string Title { get; set; }
    public required string Details { get; set; }
}
