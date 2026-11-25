namespace VubesClub.Api.Contracts;

public sealed record ProgramDay(
    Guid Id,
    DateOnly Date,
    IReadOnlyList<ProgramSession> Sessions);
