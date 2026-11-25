namespace VubesClub.Api.Contracts;

public sealed record TrainerDetail(
    TrainerCard Trainer,
    Crew? Crew,
    IReadOnlyList<EventCard> Upcoming,
    IReadOnlyList<EventCard> RatedExperiences);
