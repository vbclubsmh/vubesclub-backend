namespace VubesClub.Api.Persistence;

public sealed class BookingRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public Guid EventId { get; set; }
    public Guid VersionId { get; set; }
    public Guid PaymentId { get; set; }
    public required string Status { get; set; }
    public required string Attendance { get; set; }
}
