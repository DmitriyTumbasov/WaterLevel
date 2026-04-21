using Npgsql;
using NpgsqlTypes;
using WaterLevel.Web.Models;

namespace WaterLevel.Web.Services;

public sealed class WaterLevelRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

    public async Task EnsureDatabaseAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            create table if not exists water_level_measurements (
                id bigserial primary key,
                station_id varchar(16) not null,
                station_name text not null,
                level numeric(10, 2) not null,
                observed_at timestamp without time zone not null,
                latitude numeric(9, 6),
                longitude numeric(9, 6),
                created_at timestamp without time zone not null default now()
            );

            create unique index if not exists ux_water_level_station_time
                on water_level_measurements(station_id, observed_at);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> InsertIfNewerAsync(WaterLevelMeasurement measurement, CancellationToken cancellationToken)
    {
        var latestObservedAt = await GetLatestObservedAtAsync(measurement.StationId, cancellationToken);
        if (latestObservedAt.HasValue && measurement.ObservedAt <= latestObservedAt.Value)
        {
            return false;
        }

        const string sql = """
            insert into water_level_measurements
                (station_id, station_name, level, observed_at, latitude, longitude)
            values
                (@station_id, @station_name, @level, @observed_at, @latitude, @longitude)
            on conflict (station_id, observed_at) do nothing;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("station_id", measurement.StationId);
        command.Parameters.AddWithValue("station_name", measurement.StationName);
        command.Parameters.AddWithValue("level", measurement.Level);
        command.Parameters.AddWithValue("observed_at", measurement.ObservedAt);
        command.Parameters.AddWithValue("latitude", (object?)measurement.Latitude ?? DBNull.Value);
        command.Parameters.AddWithValue("longitude", (object?)measurement.Longitude ?? DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<WaterLevelMeasurement?> GetLatestAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select id, station_id, station_name, level, observed_at, latitude, longitude, created_at
            from water_level_measurements
            order by observed_at desc
            limit 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? MapMeasurement(reader)
            : null;
    }

    public async Task<IReadOnlyList<WaterLevelHistoryPoint>> GetHistoryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select observed_at, level
            from water_level_measurements
            where (@from is null or observed_at >= @from)
              and (@to is null or observed_at <= @to)
            order by observed_at;
            """;

        var result = new List<WaterLevelHistoryPoint>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var fromParameter = command.Parameters.Add("from", NpgsqlDbType.Timestamp);
        fromParameter.Value = from ?? (object)DBNull.Value;

        var toParameter = command.Parameters.Add("to", NpgsqlDbType.Timestamp);
        toParameter.Value = to ?? (object)DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WaterLevelHistoryPoint
            {
                ObservedAt = reader.GetDateTime(0),
                Level = reader.GetDecimal(1)
            });
        }

        return result;
    }

    private async Task<DateTime?> GetLatestObservedAtAsync(string stationId, CancellationToken cancellationToken)
    {
        const string sql = """
            select observed_at
            from water_level_measurements
            where station_id = @station_id
            order by observed_at desc
            limit 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("station_id", stationId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is DateTime value ? value : null;
    }

    private static WaterLevelMeasurement MapMeasurement(NpgsqlDataReader reader)
    {
        return new WaterLevelMeasurement
        {
            Id = reader.GetInt64(0),
            StationId = reader.GetString(1),
            StationName = reader.GetString(2),
            Level = reader.GetDecimal(3),
            ObservedAt = reader.GetDateTime(4),
            Latitude = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            Longitude = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            CreatedAt = reader.GetDateTime(7)
        };
    }
}
