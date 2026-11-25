namespace VubesClub.Api.Persistence;

public sealed class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Phone { get; set; }
    public Guid? InviteId { get; set; }
    public string? FullName { get; set; }
    public required string Digest { get; set; }
    public DateTime Expires { get; set; }
    public DateTime ResendAt { get; set; }
    public DateTime Created { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
}
