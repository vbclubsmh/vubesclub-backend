namespace VubesClub.Api.Persistence;

public sealed class SavedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid EventId { get; set; }
}
