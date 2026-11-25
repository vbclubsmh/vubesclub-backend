namespace VubesClub.Api.Contracts;

public sealed record TrainerPage(
    IReadOnlyList<TrainerCard> Items,
    string? NextCursor);
