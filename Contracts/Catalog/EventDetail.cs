namespace VubesClub.Api.Contracts;

public sealed record EventDetail(
    EventCard Event,
    string ProgramVersion,
    IReadOnlyList<Sport> AdditionalSports,
    TrainerCard Organizer,
    IReadOnlyList<ProgramDay> Days,
    string? Preparation,
    MeetingPoint MeetingPoint,
    string? TransportNotes,
    string? Accommodation,
    IReadOnlyList<Media> Gallery,
    Viewer Viewer);
