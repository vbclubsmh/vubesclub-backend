namespace VubesClub.Api.Contracts;

public sealed record Pass(
    Guid BookingId,
    Guid EventId,
    string Title,
    DateTime StartsAt,
    string TimeZone,
    MeetingPoint MeetingPoint,
    string QrPayload,
    DateTime ValidUntil);
