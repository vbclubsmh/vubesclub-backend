namespace VubesClub.Api.Contracts;

public sealed record EventPage(
    IReadOnlyList<EventCard> Items,
    string? NextCursor,
    int Total);
