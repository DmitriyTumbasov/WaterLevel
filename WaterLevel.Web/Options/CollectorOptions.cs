namespace WaterLevel.Web.Options;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public string SourceUrl { get; set; } = string.Empty;
    public string StationId { get; set; } = "76556";
    public int IntervalMinutes { get; set; } = 60;
}
