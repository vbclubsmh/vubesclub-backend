namespace VubesClub.Api.Contracts;

public sealed record Session(
    string MembershipState,
    string NextAction,
    DateTime SessionExpiresAt);
