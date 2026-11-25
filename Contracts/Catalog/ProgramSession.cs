namespace VubesClub.Api.Contracts;

public sealed record ProgramSession(
    Guid Id,
    DateTime StartsAt,
    DateTime? EndsAt,
    string Title,
    string Details);
