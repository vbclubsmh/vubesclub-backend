namespace VubesClub.Api.Contracts;

public sealed record MyProfile(
    Guid MemberId,
    string FullName,
    Media? Avatar,
    Media? Cover,
    string PhoneMasked,
    string Phone,
    string? Email,
    int? HeightCm,
    int? WeightKg,
    string? ClothingSize,
    DateOnly? BirthDate,
    string MembershipState,
    string Tier,
    bool TrainerRole);
