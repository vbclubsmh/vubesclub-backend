using VubesClub.Api.Infrastructure;

namespace VubesClub.Api.Modules.Payments;

public sealed class UnconfiguredGateway : IPaymentGateway
{
    public string Name => "unconfigured";
    public bool IsConfigured => false;

    public static ApiFault Unavailable() => new(503, "PAYMENT_PROVIDER_UNAVAILABLE",
        "Etkinlik ödemeleri henüz açılmadı.");

    public Task<CheckoutSession> StartAsync(EventCharge charge, CancellationToken ct) => throw Unavailable();
    public Task<VerifiedSettlement?> FindAsync(Guid paymentId, CancellationToken ct) => throw Unavailable();
    public Task<VerifiedSettlement> VerifyNotificationAsync(HttpRequest request, CancellationToken ct) => throw Unavailable();
    public Task<bool> RefundAsync(Guid refundId, EventCharge charge, string reference, CancellationToken ct) => throw Unavailable();
}
