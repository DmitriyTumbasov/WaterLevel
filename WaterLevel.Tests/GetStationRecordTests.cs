using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WaterLevel.Web.Services;
using Xunit;

namespace WaterLevel.Tests;

/// <summary>
/// Выбор нужной станции из ответа источника.
/// Сеть не используется: HTTP-ответ подставляется вручную.
/// </summary>
public sealed class GetStationRecordTests
{
    private const string Url = "https://example.invalid/water";

    private static WaterLevelApiClient ClientReturning(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return new WaterLevelApiClient(new HttpClient(handler), NullLogger<WaterLevelApiClient>.Instance);
    }

    private static string Json(params (string Station, string Name, string Value)[] records)
    {
        var items = records.Select(r =>
            $$"""{"dt":"17.09.2026","tm":"08:00","id_station":"{{r.Station}}","nm":"{{r.Name}}","v":"{{r.Value}}","x":"58.6","y":"49.66"}""");

        return "[" + string.Join(",", items) + "]";
    }

    [Fact]
    public async Task Находит_нужную_станцию_среди_нескольких()
    {
        var client = ClientReturning(
            HttpStatusCode.OK,
            Json(("76550", "Котельнич", "100"), ("76556", "Киров", "123.45"), ("76560", "Вятские Поляны", "90")));

        var record = await client.GetStationRecordAsync(Url, "76556", CancellationToken.None);

        Assert.NotNull(record);
        Assert.Equal("76556", record.StationId);
        Assert.Equal("Киров", record.StationName);
        Assert.Equal("123.45", record.Value);
    }

    [Fact]
    public async Task Возвращает_null_если_нужной_станции_нет_в_ответе()
    {
        var client = ClientReturning(HttpStatusCode.OK, Json(("76550", "Котельнич", "100")));

        var record = await client.GetStationRecordAsync(Url, "76556", CancellationToken.None);

        Assert.Null(record);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Возвращает_null_на_пустом_ответе(string body)
    {
        var client = ClientReturning(HttpStatusCode.OK, body);

        var record = await client.GetStationRecordAsync(Url, "76556", CancellationToken.None);

        Assert.Null(record);
    }

    /// <summary>
    /// Ошибка источника не должна выглядеть как «данных нет»:
    /// иначе сборщик молча пропустит сбой вместо записи в лог.
    /// </summary>
    [Fact]
    public async Task Пробрасывает_ошибку_если_источник_ответил_кодом_ошибки()
    {
        var client = ClientReturning(HttpStatusCode.InternalServerError, "server is down");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStationRecordAsync(Url, "76556", CancellationToken.None));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
