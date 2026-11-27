namespace VubesClub.Api.Contracts;

public sealed record CheckInRequest(string Payload);

public sealed record CheckInResult(
    Guid BookingId,
    string MemberName,
    Media? MemberAvatar,
    Guid EventId,
    string EventTitle,
    string Outcome,
    DateTime CheckedInAt);
