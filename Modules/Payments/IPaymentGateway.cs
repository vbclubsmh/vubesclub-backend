namespace VubesClub.Api.Modules.Payments;

public interface IPaymentGateway
{
    string Name { get; }
    bool IsConfigured { get; }

    // PaymentId is the merchant order and provider idempotency key across retries.
    Task<CheckoutSession> StartAsync(EventCharge charge, CancellationToken ct);
    Task<VerifiedSettlement?> FindAsync(Guid paymentId, CancellationToken ct);
    Task<VerifiedSettlement> VerifyNotificationAsync(HttpRequest request, CancellationToken ct);

    // The same refundId must produce at most one full refund. True means the refund is confirmed.
    Task<bool> RefundAsync(Guid refundId, EventCharge charge, string reference, CancellationToken ct);
}
