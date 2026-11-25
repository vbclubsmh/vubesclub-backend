namespace VubesClub.Api.Contracts;

public sealed record VerifiedSession(
    Session Session,
    Tokens Tokens);
