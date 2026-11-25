namespace VubesClub.Api.Contracts;

public sealed record Filters(
    IReadOnlyList<Sport> Sports,
    IReadOnlyList<LevelOption> Levels);
