namespace VubesClub.Api.Persistence;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Phone { get; set; }
    public string? Email { get; set; }
    public required string FullName { get; set; }
    public required string Status { get; set; }
    public string? Avatar { get; set; }
    public string? Cover { get; set; }
    public required string Visibility { get; set; }
    public int? HeightCm { get; set; }
    public int? WeightKg { get; set; }
    public string? ClothingSize { get; set; }
    public DateOnly? BirthDate { get; set; }
}
