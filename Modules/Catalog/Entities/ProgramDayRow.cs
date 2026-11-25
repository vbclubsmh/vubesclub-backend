namespace VubesClub.Api.Persistence;

public sealed class ProgramDayRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VersionId { get; set; }
    public DateOnly Day { get; set; }
    public int Ordinal { get; set; }
}
