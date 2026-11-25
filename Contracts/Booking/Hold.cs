namespace VubesClub.Api.Contracts;

public sealed record Hold(
    Guid Id,
    string State,
    DateTime ExpiresAt,
    DateTime? VerificationEndsAt,
    Guid? BookingId,
    Guid? PaymentId,
    CheckoutSnapshot Checkout);
