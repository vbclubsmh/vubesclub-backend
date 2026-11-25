namespace VubesClub.Api.Contracts;

public sealed record Pin(
    Guid EventAliasId,
    string DisplayName,
    Media? Avatar,
    Point Position,
    DateTime RecordedAt);
