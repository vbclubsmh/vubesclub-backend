namespace VubesClub.Api.Contracts;

public sealed record StartPayment(
    string AcceptedTermsVersion,
    bool TermsAccepted);
