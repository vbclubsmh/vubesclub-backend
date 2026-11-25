namespace VubesClub.Api.Persistence;

public sealed class AdditionalSport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VersionId { get; set; }
    public Guid SportId { get; set; }
}
