using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteEarnedValueRepository : IEarnedValueRepository
{
    private readonly string _databasePath;

    public SqliteEarnedValueRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveLatestAsync(EarnedValueResult result, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO planning_earned_value (schedule_id, status_date, updated_utc, json_data)
            VALUES ($sid, $sd, $utc, $json)
            ON CONFLICT(schedule_id) DO UPDATE SET
                status_date = excluded.status_date,
                updated_utc = excluded.updated_utc,
                json_data = excluded.json_data;";
        cmd.Parameters.AddWithValue("$sid", result.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$sd", result.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$utc", result.CalculatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(result, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_earned_value WHERE schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<EarnedValueResult>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }
}

public sealed class SqliteEarnedValueSnapshotRepository : IEarnedValueSnapshotRepository
{
    private readonly string _databasePath;

    public SqliteEarnedValueSnapshotRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(EarnedValueSnapshot snapshot, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_earned_value_snapshots
                (id, schedule_id, version, status_date, created_utc, created_by, sha256, json_data)
            VALUES ($id, $sid, $ver, $sd, $utc, $by, $sha, $json);";
        cmd.Parameters.AddWithValue("$id", snapshot.SnapshotId);
        cmd.Parameters.AddWithValue("$sid", snapshot.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$ver", snapshot.Version);
        cmd.Parameters.AddWithValue("$sd", snapshot.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$utc", snapshot.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$by", snapshot.CreatedBy);
        cmd.Parameters.AddWithValue("$sha", snapshot.SHA256);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<EarnedValueSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_earned_value_snapshots WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<EarnedValueSnapshot>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<List<EarnedValueSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var list = new List<EarnedValueSnapshot>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_earned_value_snapshots WHERE schedule_id = $sid ORDER BY version ASC;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<EarnedValueSnapshot>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(version), -1) FROM planning_earned_value_snapshots WHERE schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) + 1;
    }
}

public sealed class SqliteEarnedValueHistoryRepository : IEarnedValueHistoryRepository
{
    private readonly string _databasePath;

    public SqliteEarnedValueHistoryRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task AppendAsync(EarnedValueHistoryEntry entry, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO planning_earned_value_history (id, schedule_id, status_date, recorded_utc, recorded_by, json_data)
            VALUES ($id, $sid, $sd, $utc, $by, $json);";
        cmd.Parameters.AddWithValue("$id", entry.EntryId);
        cmd.Parameters.AddWithValue("$sid", entry.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$sd", entry.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$utc", entry.RecordedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$by", entry.RecordedBy);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entry, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<EarnedValueHistoryEntry>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
    {
        var list = new List<EarnedValueHistoryEntry>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_earned_value_history WHERE schedule_id = $sid ORDER BY recorded_utc DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<EarnedValueHistoryEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
