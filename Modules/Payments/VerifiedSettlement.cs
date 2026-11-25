namespace VubesClub.Api.Modules.Payments;

// Adapters produce this value only after authenticating a callback or querying the provider server-to-server.
public sealed record VerifiedSettlement(
    Guid PaymentId,
    string Reference,
    string ExternalEventId,
    long AmountMinor,
    string Currency,
    SettlementOutcome Outcome,
    DateTime? PaidAt);
