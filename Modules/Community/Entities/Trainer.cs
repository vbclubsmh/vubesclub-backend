namespace VubesClub.Api.Persistence;

public sealed class Trainer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public required string Specialty { get; set; }
    public required string Bio { get; set; }
    public required string Experience { get; set; }
    public required string Location { get; set; }
    public string? Cover { get; set; }
    public bool Active { get; set; } = true;
}
