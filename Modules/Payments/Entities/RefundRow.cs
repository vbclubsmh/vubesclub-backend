namespace VubesClub.Api.Persistence;

public sealed class RefundRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }
}
