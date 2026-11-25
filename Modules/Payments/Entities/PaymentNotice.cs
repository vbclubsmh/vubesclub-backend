namespace VubesClub.Api.Persistence;

public sealed class PaymentNotice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public Guid PaymentId { get; set; }
    public DateTime Received { get; set; }
}
