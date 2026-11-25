namespace VubesClub.Api.Modules.Payments;

public sealed record EventCharge(
    Guid PaymentId,
    Guid EventId,
    long AmountMinor,
    string Currency,
    DateTime ExpiresAt);
