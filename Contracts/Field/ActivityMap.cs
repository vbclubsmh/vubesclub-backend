namespace VubesClub.Api.Contracts;

public sealed record ActivityMap(
    Guid EventId,
    DateTime AsOf,
    IReadOnlyList<Point> Route,
    IReadOnlyList<Pin> Participants,
    Sharing MySharing);
