namespace VubesClub.Api.Persistence;

public sealed class RatingRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid EventId { get; set; }
    public int Stars { get; set; }
    public required string Moderation { get; set; }
}
