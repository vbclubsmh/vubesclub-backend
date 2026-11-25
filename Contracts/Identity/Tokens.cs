namespace VubesClub.Api.Contracts;

public sealed record Tokens(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime SessionExpiresAt);
