using System.Globalization;
using System.Text.Json;
using WaterLevel.Web.Models;

namespace WaterLevel.Web.Services;

public sealed class WaterLevelApiClient(HttpClient httpClient, ILogger<WaterLevelApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SourceWaterLevelRecord?> GetStationRecordAsync(
        string url,
        string stationId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var records = await JsonSerializer.DeserializeAsync<List<SourceWaterLevelRecord>>(
            stream,
            JsonOptions,
            cancellationToken);

        if (records is null || records.Count == 0)
        {
            logger.LogWarning("Source returned no records.");
            return null;
        }

        return records.FirstOrDefault(x => x.StationId == stationId);
    }

    public static WaterLevelMeasurement ParseMeasurement(SourceWaterLevelRecord record)
    {
        var datePart = DateOnly.ParseExact(record.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        var timePart = record.TimeLabel.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Last();
        var timeOnly = TimeOnly.ParseExact(timePart, "HH:mm", CultureInfo.InvariantCulture);
        var observedAt = datePart.ToDateTime(timeOnly);

        return new WaterLevelMeasurement
        {
            StationId = record.StationId,
            StationName = record.StationName,
            Level = decimal.Parse(record.Value, CultureInfo.InvariantCulture),
            ObservedAt = observedAt,
            Latitude = ParseNullableDecimal(record.Latitude),
            Longitude = ParseNullableDecimal(record.Longitude)
        };
    }

    private static decimal? ParseNullableDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
