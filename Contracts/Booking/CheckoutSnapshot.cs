namespace VubesClub.Api.Contracts;

public sealed record CheckoutSnapshot(
    Guid EventId,
    string Title,
    string PlaceLabel,
    Media? Cover,
    DateTime StartsAt,
    DateTime EndsAt,
    string TimeZone,
    string ProgramVersion,
    int ProgramDays,
    int ParticipantCount,
    bool AccommodationIncluded,
    Money Total,
    Terms Terms);
