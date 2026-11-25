namespace VubesClub.Api.Contracts;

public sealed record Challenge(
    Guid ChallengeId,
    int CodeLength,
    DateTime ExpiresAt,
    DateTime ResendAt);
