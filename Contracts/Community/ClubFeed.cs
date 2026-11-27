namespace VubesClub.Api.Contracts;

public sealed record TrainerRef(Guid Id, string DisplayName, Media? Avatar);

public sealed record FeedTrainer(TrainerCard Trainer, string Source);

public sealed record FeedEvent(EventCard Event, TrainerRef Organizer);

public sealed record ClubFeed(
    IReadOnlyList<FeedTrainer> Trainers,
    IReadOnlyList<FeedEvent> ForYou,
    IReadOnlyList<FeedEvent> RatedExperiences);
