namespace VubesClub.Api.Contracts;

public sealed record Sport(
    string Code,
    string Label,
    Media? FallbackCover);
