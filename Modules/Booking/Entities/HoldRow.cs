namespace VubesClub.Api.Persistence;

public sealed class HoldRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid EventId { get; set; }
    public Guid VersionId { get; set; }
    public required string State { get; set; }
    public DateTime Expires { get; set; }
    public DateTime? VerifyUntil { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public required string TermsVersion { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
