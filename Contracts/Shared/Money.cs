namespace VubesClub.Api.Contracts;

public sealed record Money(
    long AmountMinor,
    string Currency);
