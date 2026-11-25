namespace VubesClub.Api.Contracts;

public sealed record Viewer(
    string Primary,
    string? ReasonCode,
    Guid? BookingId,
    Guid? HoldId,
    bool CanViewActivityMap,
    bool CanRate,
    MyRating? MyRating);
