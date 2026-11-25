namespace VubesClub.Api.Contracts;

public sealed record TrainerCard(
    Guid Id,
    string DisplayName,
    string Specialty,
    string ExperienceText,
    string LocationLabel,
    string Bio,
    Media? Avatar,
    Media? Cover,
    bool IsFollowed,
    string IntroductionState);
