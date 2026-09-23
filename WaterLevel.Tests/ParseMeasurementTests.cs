using WaterLevel.Web.Models;
using WaterLevel.Web.Services;
using Xunit;

namespace WaterLevel.Tests;

/// <summary>
/// Разбор записи источника в измерение: дата, время, уровень и координаты.
/// </summary>
public sealed class ParseMeasurementTests
{
    private static SourceWaterLevelRecord Record(
        string date = "17.09.2026",
        string timeLabel = "08:00",
        string stationId = "76556",
        string stationName = "Киров",
        string value = "123.45",
        string latitude = "58.603595",
        string longitude = "49.668005")
    {
        return new SourceWaterLevelRecord
        {
            Date = date,
            TimeLabel = timeLabel,
            StationId = stationId,
            StationName = stationName,
            Value = value,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    [Fact]
    public void Собирает_дату_и_время_в_один_момент_наблюдения()
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(Record(date: "17.09.2026", timeLabel: "08:00"));

        Assert.Equal(new DateTime(2026, 9, 17, 8, 0, 0), measurement.ObservedAt);
    }

    [Fact]
    public void Переносит_идентификатор_и_название_станции_без_изменений()
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(Record(stationId: "76556", stationName: "Киров"));

        Assert.Equal("76556", measurement.StationId);
        Assert.Equal("Киров", measurement.StationName);
    }

    [Theory]
    [InlineData("123.45", 123.45)]
    [InlineData("0", 0)]
    [InlineData("-15.5", -15.5)]
    public void Читает_уровень_с_точкой_как_разделителем(string raw, decimal expected)
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(Record(value: raw));

        Assert.Equal(expected, measurement.Level);
    }

    [Fact]
    public void Читает_координаты_когда_они_заданы()
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(
            Record(latitude: "58.603595", longitude: "49.668005"));

        Assert.Equal(58.603595m, measurement.Latitude);
        Assert.Equal(49.668005m, measurement.Longitude);
    }

    [Theory]
    [InlineData("")]
    [InlineData("н/д")]
    public void Оставляет_координаты_пустыми_если_источник_их_не_прислал(string raw)
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(Record(latitude: raw, longitude: raw));

        Assert.Null(measurement.Latitude);
        Assert.Null(measurement.Longitude);
    }

    /// <summary>
    /// Источник присылает в поле tm перечисление сроков наблюдения.
    /// Берётся последний — он самый свежий.
    /// </summary>
    [Theory]
    [InlineData("08:00, 20:00", 20)]
    [InlineData("08:00,20:00", 20)]
    [InlineData("02:00,  08:00 , 14:00", 14)]
    public void Из_перечня_сроков_берёт_последний(string timeLabel, int expectedHour)
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(Record(date: "17.09.2026", timeLabel: timeLabel));

        Assert.Equal(new DateTime(2026, 9, 17, expectedHour, 0, 0), measurement.ObservedAt);
    }

    [Fact]
    public void Игнорирует_пустые_элементы_в_перечне_сроков()
    {
        var measurement = WaterLevelApiClient.ParseMeasurement(
            Record(date: "17.09.2026", timeLabel: "08:00, 20:00, "));

        Assert.Equal(new DateTime(2026, 9, 17, 20, 0, 0), measurement.ObservedAt);
    }

    /// <summary>
    /// Разбор намеренно падает на мусоре, а не подставляет значение по умолчанию:
    /// сборщик поймает исключение, запишет ошибку в лог и оставит прошлую запись.
    /// </summary>
    [Theory]
    [InlineData("2026-09-17", "08:00")]
    [InlineData("17/09/2026", "08:00")]
    [InlineData("17.09.2026", "8:00")]
    public void Падает_на_некорректных_дате_или_времени(string date, string timeLabel)
    {
        Assert.ThrowsAny<FormatException>(
            () => WaterLevelApiClient.ParseMeasurement(Record(date: date, timeLabel: timeLabel)));
    }

    /// <summary>
    /// Пустое tm даёт не ошибку формата, а пустую последовательность сроков.
    /// Тест фиксирует это, чтобы поведение не изменилось незаметно.
    /// </summary>
    [Fact]
    public void Падает_если_перечень_сроков_пуст()
    {
        Assert.Throws<InvalidOperationException>(
            () => WaterLevelApiClient.ParseMeasurement(Record(timeLabel: "")));
    }

    [Fact]
    public void Падает_если_уровень_не_число()
    {
        Assert.Throws<FormatException>(
            () => WaterLevelApiClient.ParseMeasurement(Record(value: "нет данных")));
    }
}
