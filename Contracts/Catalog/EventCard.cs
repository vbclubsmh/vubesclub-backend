namespace VubesClub.Api.Contracts;

public sealed record EventCard(
    Guid Id,
    string Title,
    string PlaceLabel,
    DateTime StartsAt,
    DateTime EndsAt,
    string TimeZone,
    Sport PrimarySport,
    string Level,
    Media? Cover,
    int? DistanceMeters,
    Money Price,
    string Phase,
    Point? Position,
    bool IsSaved,
    RatingSummary? Rating,
    bool Featured);
