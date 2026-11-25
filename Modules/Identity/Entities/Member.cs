namespace VubesClub.Api.Persistence;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Phone { get; set; }
    public required string FullName { get; set; }
    public required string Status { get; set; }
    public string? Avatar { get; set; }
    public string? Cover { get; set; }
    public required string Visibility { get; set; }
}
