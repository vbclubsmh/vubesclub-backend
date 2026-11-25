namespace VubesClub.Api.Persistence;

public sealed class RequestKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }
    public required string Operation { get; set; }
    public required string Key { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
}
