namespace VubesClub.Api.Contracts;

public sealed record Sharing(
    bool Enabled,
    Guid? SharingId,
    DateTime? ExpiresAt);
