namespace VubesClub.Api.Contracts;

public sealed record InviteOtp(
    string Purpose,
    string Phone,
    string InvitationCode,
    string FullName,
    bool AdultDeclared);
