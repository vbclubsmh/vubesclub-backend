namespace VubesClub.Api.Contracts;

public sealed record Introduction(
    Guid TrainerId,
    string State,
    DateTime RequestedAt);
