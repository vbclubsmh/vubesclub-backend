namespace VubesClub.Api.Persistence;

public sealed class EventVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public required string Revision { get; set; }
    public required string Title { get; set; }
    public required string Place { get; set; }
    public DateTime Starts { get; set; }
    public DateTime Ends { get; set; }
    public required string TimeZone { get; set; }
    public Guid SportId { get; set; }
    public required string Level { get; set; }
    public string? Cover { get; set; }
    public string[] Gallery { get; set; } = [];
    public int? DistanceMeters { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public required string Meeting { get; set; }
    public string? Directions { get; set; }
    public string? Preparation { get; set; }
    public string? Lodging { get; set; }
    public string? Transport { get; set; }
    public required string TermsVersion { get; set; }
    public required string TermsUrl { get; set; }
}
