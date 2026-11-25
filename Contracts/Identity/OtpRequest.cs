namespace VubesClub.Api.Contracts;

public sealed record OtpRequest(
    string Purpose,
    string Phone,
    string? InvitationCode,
    string? FullName,
    bool? AdultDeclared);
