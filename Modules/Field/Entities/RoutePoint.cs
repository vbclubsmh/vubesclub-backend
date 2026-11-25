namespace VubesClub.Api.Persistence;

public sealed class RoutePoint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public int Ordinal { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
