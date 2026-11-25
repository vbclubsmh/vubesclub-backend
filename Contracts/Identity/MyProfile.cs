namespace VubesClub.Api.Contracts;

public sealed record MyProfile(
    Guid MemberId,
    string FullName,
    Media? Avatar,
    Media? Cover,
    string PhoneMasked,
    string MembershipState,
    string Tier,
    bool TrainerRole);
