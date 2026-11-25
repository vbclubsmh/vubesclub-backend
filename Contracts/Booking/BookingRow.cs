namespace VubesClub.Api.Contracts;

public sealed record BookingRow(
    Guid Id,
    EventCard Event,
    string Status,
    string Attendance,
    bool CanShowPass,
    IReadOnlyList<Person> VisibleParticipantPreview);
