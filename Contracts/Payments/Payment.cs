namespace VubesClub.Api.Contracts;

public sealed record Payment(
    Guid PaymentId,
    Guid HoldId,
    string Status,
    PaymentAction? NextAction);
