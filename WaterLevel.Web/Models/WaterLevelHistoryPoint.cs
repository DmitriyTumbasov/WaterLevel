namespace WaterLevel.Web.Models;

public sealed class WaterLevelHistoryPoint
{
    public DateTime ObservedAt { get; init; }
    public decimal Level { get; init; }
}
