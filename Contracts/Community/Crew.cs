namespace VubesClub.Api.Contracts;

public sealed record Crew(
    string Name,
    IReadOnlyList<Person> Members);
