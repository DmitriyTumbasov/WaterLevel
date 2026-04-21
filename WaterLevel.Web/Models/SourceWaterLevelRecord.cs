using System.Text.Json.Serialization;

namespace WaterLevel.Web.Models;

public sealed class SourceWaterLevelRecord
{
    [JsonPropertyName("dt")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("tm")]
    public string TimeLabel { get; set; } = string.Empty;

    [JsonPropertyName("id_station")]
    public string StationId { get; set; } = string.Empty;

    [JsonPropertyName("nm")]
    public string StationName { get; set; } = string.Empty;

    [JsonPropertyName("v")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public string Latitude { get; set; } = string.Empty;

    [JsonPropertyName("y")]
    public string Longitude { get; set; } = string.Empty;
}
