namespace VubesClub.Api.Contracts;

public sealed record BookingPage(
    IReadOnlyList<BookingRow> Items,
    string? NextCursor);
