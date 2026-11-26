namespace VubesClub.Api.Contracts;

public sealed record ApplicationOtp(
    string Phone,
    string FullName,
    string Email,
    string University,
    string JobTitle,
    string? Workplace,
    string? Instagram,
    string? LinkedIn,
    string? InvitationCode,
    bool AdultDeclared);
