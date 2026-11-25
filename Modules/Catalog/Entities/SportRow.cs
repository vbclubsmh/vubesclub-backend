namespace VubesClub.Api.Persistence;

public sealed class SportRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Code { get; set; }
    public required string Label { get; set; }
    public string? FallbackCover { get; set; }
}
