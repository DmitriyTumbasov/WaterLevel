using Microsoft.Extensions.Logging.Abstractions;
using WaterLevel.Web.Services;
using Xunit;

namespace WaterLevel.Tests;

/// <summary>
/// Поведение при неотвечающем источнике. По умолчанию HttpClient ждёт
/// 100 секунд; приложение задаёт свой таймаут из CollectorOptions.
/// </summary>
public sealed class RequestTimeoutTests
{
    private const string Url = "https://example.invalid/water";

    [Fact]
    public async Task Прекращает_ожидание_по_истечении_таймаута()
    {
        var client = new HttpClient(new NeverRespondingHandler())
        {
            Timeout = TimeSpan.FromMilliseconds(200)
        };

        var apiClient = new WaterLevelApiClient(client, NullLogger<WaterLevelApiClient>.Instance);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => apiClient.GetStationRecordAsync(Url, "76556", CancellationToken.None));
    }

    [Fact]
    public async Task Уважает_отмену_снаружи()
    {
        var client = new HttpClient(new NeverRespondingHandler());
        var apiClient = new WaterLevelApiClient(client, NullLogger<WaterLevelApiClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => apiClient.GetStationRecordAsync(Url, "76556", cts.Token));
    }

    /// <summary>
    /// Источник, который принимает соединение и не отвечает, —
    /// именно так выглядела недоступность pogoda43.ru с нового сервера.
    /// </summary>
    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Сюда выполнение не доходит.");
        }
    }
}
