using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using VubesClub.Api.Infrastructure;
using VubesClub.Api.Modules.Payments;

namespace VubesClub.Verification;

internal sealed class TestGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<Guid, EventCharge> orders = new();
    private readonly ConcurrentDictionary<string, VerifiedSettlement> verified = new();
    private readonly ConcurrentDictionary<Guid, Guid> refunds = new();

    public string Name => "verification-only";
    public bool IsConfigured { get; set; } = true;
    public int RefundCount => refunds.Count;
    public int OrderCount => orders.Count;

    public Task<CheckoutSession> StartAsync(EventCharge charge, CancellationToken ct)
    {
        var existing = orders.GetOrAdd(charge.PaymentId, charge);
        Checks.Require(existing == charge, "The same merchant order was reused with new charge details.");
        return Task.FromResult(new CheckoutSession(charge.PaymentId.ToString("N"), new Uri("https://example.invalid/pay")));
    }

    public string Paid(Guid paymentId, DateTime paidAt, long? changedAmount = null)
    {
        var charge = orders[paymentId];
        var eventId = Guid.NewGuid().ToString("N");
        verified[eventId] = new VerifiedSettlement(paymentId, paymentId.ToString("N"), eventId,
            changedAmount ?? charge.AmountMinor, charge.Currency, SettlementOutcome.Paid, paidAt);
        return eventId;
    }

    public Task<VerifiedSettlement?> FindAsync(Guid paymentId, CancellationToken ct) =>
        Task.FromResult<VerifiedSettlement?>(null);

    public async Task<VerifiedSettlement> VerifyNotificationAsync(HttpRequest request, CancellationToken ct)
    {
        var eventId = await request.ReadFromJsonAsync<string>(ct);
        return eventId != null && verified.TryGetValue(eventId, out var settlement)
            ? settlement : throw ApiFault.Forbidden();
    }

    public Task<bool> RefundAsync(Guid refundId, EventCharge charge, string reference, CancellationToken ct)
    {
        Checks.Require(orders[charge.PaymentId] == charge, "A full refund must match its original charge.");
        Checks.Require(refunds.GetOrAdd(refundId, charge.PaymentId) == charge.PaymentId, "Refund identity changed.");
        return Task.FromResult(true);
    }
}

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now = now.Add(duration);
}
