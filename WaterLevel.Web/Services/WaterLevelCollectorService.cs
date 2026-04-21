using Microsoft.Extensions.Options;
using WaterLevel.Web.Options;

namespace WaterLevel.Web.Services;

public sealed class WaterLevelCollectorService(
    WaterLevelApiClient apiClient,
    WaterLevelRepository repository,
    IOptions<CollectorOptions> options,
    ILogger<WaterLevelCollectorService> logger) : BackgroundService
{
    private readonly CollectorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CollectSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CollectSafelyAsync(stoppingToken);
        }
    }

    public async Task<bool> CollectOnceAsync(CancellationToken cancellationToken)
    {
        var record = await apiClient.GetStationRecordAsync(
            _options.SourceUrl,
            _options.StationId,
            cancellationToken);

        if (record is null)
        {
            logger.LogWarning("Station {StationId} was not found in source response.", _options.StationId);
            return false;
        }

        var measurement = WaterLevelApiClient.ParseMeasurement(record);
        var inserted = await repository.InsertIfNewerAsync(measurement, cancellationToken);

        logger.LogInformation(
            inserted
                ? "Stored water level {Level} for {ObservedAt}."
                : "Skipped water level {Level} for {ObservedAt}; record is not newer.",
            measurement.Level,
            measurement.ObservedAt);

        return inserted;
    }

    private async Task CollectSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CollectOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Water level collection failed.");
        }
    }
}
