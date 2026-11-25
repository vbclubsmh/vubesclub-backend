namespace VubesClub.Api.Persistence;

public sealed class ApplicationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid InvitationId { get; set; }
    public DateTime AdultDeclaredAt { get; set; }
}
