namespace VubesClub.Api.Persistence;

public sealed class PaymentRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HoldId { get; set; }
    public required string Provider { get; set; }
    public string? Reference { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string? Redirect { get; set; }
    public DateTime? PaidAt { get; set; }
}
