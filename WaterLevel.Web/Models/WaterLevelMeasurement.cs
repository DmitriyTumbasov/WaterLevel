namespace WaterLevel.Web.Models;

public sealed class WaterLevelMeasurement
{
    public long Id { get; init; }
    public string StationId { get; init; } = string.Empty;
    public string StationName { get; init; } = string.Empty;
    public decimal Level { get; init; }
    public DateTime ObservedAt { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public DateTime CreatedAt { get; init; }
}
