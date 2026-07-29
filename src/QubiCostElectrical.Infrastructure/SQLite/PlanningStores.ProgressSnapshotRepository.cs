using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteProgressSnapshotRepository : IProgressSnapshotRepository
{
    private readonly string _databasePath;

    public SqliteProgressSnapshotRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<ProgressSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_snapshots WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<ProgressSnapshot>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<List<ProgressSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var list = new List<ProgressSnapshot>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_snapshots WHERE schedule_id = $schedId ORDER BY version ASC;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProgressSnapshot>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(ProgressSnapshot snapshot, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_progress_snapshots
                (id, schedule_id, version, status_date, created_utc, created_by, sha256, json_data)
            VALUES ($id, $schedId, $version, $statusDate, $createdUtc, $createdBy, $sha, $json);";
        cmd.Parameters.AddWithValue("$id", snapshot.SnapshotId);
        cmd.Parameters.AddWithValue("$schedId", snapshot.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$version", snapshot.Version);
        cmd.Parameters.AddWithValue("$statusDate", snapshot.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$createdUtc", snapshot.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$createdBy", snapshot.CreatedBy);
        cmd.Parameters.AddWithValue("$sha", snapshot.SHA256);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveItemsAsync(string snapshotId, IReadOnlyList<ProgressSnapshotItem> items, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var tx = connection.BeginTransaction();
        using (var del = connection.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM planning_progress_snapshot_items WHERE snapshot_id = $id;";
            del.Parameters.AddWithValue("$id", snapshotId);
            await del.ExecuteNonQueryAsync(ct);
        }

        foreach (var item in items)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO planning_progress_snapshot_items (snapshot_id, activity_id, json_data)
                VALUES ($sid, $aid, $json);";
            cmd.Parameters.AddWithValue("$sid", snapshotId);
            cmd.Parameters.AddWithValue("$aid", item.ActivityId.Value);
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, ProfessionalDbHelpers.JsonOptions));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
    }

    public async Task<List<ProgressSnapshotItem>> GetItemsAsync(string snapshotId, CancellationToken ct = default)
    {
        var list = new List<ProgressSnapshotItem>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_snapshot_items WHERE snapshot_id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProgressSnapshotItem>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(version), -1) FROM planning_progress_snapshots WHERE schedule_id = $schedId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        var maxVer = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return maxVer + 1;
    }
}
