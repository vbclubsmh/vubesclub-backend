namespace VubesClub.Api.Persistence;

/// <summary>Bir üyenin e-posta veya telefon değişikliği isteği; kodla doğrulanana kadar uygulanmaz.</summary>
public sealed class ContactChange
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public string? NewEmail { get; set; }
    public string? NewPhone { get; set; }
    public required string Digest { get; set; }
    public DateTime Expires { get; set; }
    public DateTime ResendAt { get; set; }
    public DateTime Created { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
}
