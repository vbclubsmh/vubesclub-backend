namespace VubesClub.Api.Persistence;

public sealed class LastLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShareId { get; set; }
    public int Sequence { get; set; }
    public DateTime Recorded { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
