using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteScheduleBaselineRepository : IScheduleBaselineRepository
{
    private readonly string _databasePath;

    public SqliteScheduleBaselineRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ScheduleBaseline?> GetByIdAsync(string baselineId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_baselines WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", baselineId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ScheduleBaseline>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ScheduleBaseline>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var list = new List<ScheduleBaseline>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_baselines WHERE schedule_id = $schedId ORDER BY version ASC;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var json = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ScheduleBaseline>(json, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(ScheduleBaseline baseline, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_baselines (id, schedule_id, version, is_official, json_data)
            VALUES ($id, $schedId, $version, $isOfficial, $json);";
        cmd.Parameters.AddWithValue("$id", baseline.BaselineId);
        cmd.Parameters.AddWithValue("$schedId", baseline.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$version", baseline.Version);
        cmd.Parameters.AddWithValue("$isOfficial", baseline.IsOfficial ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(baseline, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string baselineId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_baselines WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", baselineId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ExistsAsync(string baselineId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM planning_baselines WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", baselineId);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return count > 0;
    }

    public async Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(version), -1) FROM planning_baselines WHERE schedule_id = $schedId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        var maxVer = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return maxVer + 1;
    }
}

public sealed class SqliteScheduleBaselineSnapshotRepository : IScheduleBaselineSnapshotRepository
{
    private readonly string _databasePath;

    public SqliteScheduleBaselineSnapshotRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ScheduleBaselineSnapshot?> GetByBaselineIdAsync(string baselineId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT snapshot_json FROM planning_baseline_snapshots WHERE baseline_id = $id;";
        cmd.Parameters.AddWithValue("$id", baselineId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ScheduleBaselineSnapshot>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task SaveAsync(string baselineId, ScheduleBaselineSnapshot snapshot, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_baseline_snapshots (baseline_id, snapshot_json)
            VALUES ($id, $json);";
        cmd.Parameters.AddWithValue("$id", baselineId);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string baselineId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_baseline_snapshots WHERE baseline_id = $id;";
        cmd.Parameters.AddWithValue("$id", baselineId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
