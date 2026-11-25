namespace VubesClub.Api.Modules.Payments;

public sealed record CheckoutSession(
    string Reference,
    Uri? Redirect,
    VerifiedSettlement? Settlement = null);
