using Npgsql;
using WaterLevel.Web.Models;
using WaterLevel.Web.Options;
using WaterLevel.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CollectorOptions>(
    builder.Configuration.GetSection(CollectorOptions.SectionName));

builder.Services.AddHttpClient<WaterLevelApiClient>();
builder.Services.AddSingleton<WaterLevelRepository>();
builder.Services.AddSingleton<WaterLevelCollectorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WaterLevelCollectorService>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

await app.Services.GetRequiredService<WaterLevelRepository>().EnsureDatabaseAsync();

// Живость сервиса для smoke-теста при выкате.
// Проверяет и себя, и доступность базы: без базы приложение бесполезно,
// поэтому «поднялся, но не видит базу» — это не успешный выкат.
app.MapGet("/health", async (
    WaterLevelRepository repository,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    try
    {
        await repository.CheckConnectionAsync(cancellationToken);
        return Results.Ok(new { status = "ok" });
    }
    catch (NpgsqlException ex)
    {
        loggerFactory.CreateLogger("Health").LogError(ex, "Health check failed: database is unavailable.");
        return Results.Json(new { status = "database unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/water-level/latest", async (WaterLevelRepository repository, CancellationToken cancellationToken) =>
{
    var measurement = await repository.GetLatestAsync(cancellationToken);
    return measurement is null ? Results.NotFound() : Results.Ok(measurement);
});

app.MapGet("/api/water-level/history", async (
    DateTime? from,
    DateTime? to,
    WaterLevelRepository repository,
    CancellationToken cancellationToken) =>
{
    var history = await repository.GetHistoryAsync(from, to, cancellationToken);
    return Results.Ok(history);
});

app.MapPost("/api/water-level/collect", async (
    WaterLevelCollectorService collector,
    CancellationToken cancellationToken) =>
{
    var inserted = await collector.CollectOnceAsync(cancellationToken);
    return Results.Ok(new { inserted });
});

app.Run();
