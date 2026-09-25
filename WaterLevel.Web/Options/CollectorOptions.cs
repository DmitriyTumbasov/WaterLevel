namespace WaterLevel.Web.Options;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public string SourceUrl { get; set; } = string.Empty;
    public string StationId { get; set; } = "76556";
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Сколько ждать ответ источника. По умолчанию HttpClient ждёт 100 секунд —
    /// для часового опроса это бессмысленно долго: недоступность источника
    /// должна выясняться быстро.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 20;
}
