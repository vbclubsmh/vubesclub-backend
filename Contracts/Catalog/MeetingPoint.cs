namespace VubesClub.Api.Contracts;

public sealed record MeetingPoint(
    string Label,
    string? Directions,
    Point? Position);
