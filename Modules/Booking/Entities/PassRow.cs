namespace VubesClub.Api.Persistence;

public sealed class PassRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }
    public required string Nonce { get; set; }
    public bool Revoked { get; set; }
}
