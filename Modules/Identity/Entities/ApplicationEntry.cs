namespace VubesClub.Api.Persistence;

public sealed class ApplicationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid? InvitationId { get; set; }
    public DateTime AdultDeclaredAt { get; set; }
    public required string University { get; set; }
    public required string JobTitle { get; set; }
    public string? Workplace { get; set; }
    public string? Instagram { get; set; }
    public string? LinkedIn { get; set; }
    public DateTime SubmittedAt { get; set; }
}
