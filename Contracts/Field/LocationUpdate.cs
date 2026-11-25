namespace VubesClub.Api.Contracts;

public sealed record LocationUpdate(
    Guid SharingId,
    Point Position,
    DateTime RecordedAt,
    int Sequence);
