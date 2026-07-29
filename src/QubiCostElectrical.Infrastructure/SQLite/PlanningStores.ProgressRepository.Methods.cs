using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteProgressIntervalRepository : IProgressIntervalRepository
{
    private readonly string _databasePath;

    public SqliteProgressIntervalRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<List<ProgressInterval>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default)
    {
        var list = new List<ProgressInterval>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_intervals WHERE progress_update_id = $id;";
        cmd.Parameters.AddWithValue("$id", progressUpdateId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProgressInterval>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task<List<ProgressInterval>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
    {
        var list = new List<ProgressInterval>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_intervals WHERE schedule_id = $schedId AND activity_id = $actId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", activityId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProgressInterval>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(ProgressInterval interval, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_progress_intervals (id, progress_update_id, schedule_id, activity_id, json_data)
            VALUES ($id, $upd, $schedId, $actId, $json);";
        cmd.Parameters.AddWithValue("$id", interval.IntervalId);
        cmd.Parameters.AddWithValue("$upd", interval.ProgressUpdateId);
        cmd.Parameters.AddWithValue("$schedId", interval.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", interval.ActivityId.Value);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(interval, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string intervalId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_progress_intervals WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", intervalId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteDelayReasonRepository : IDelayReasonRepository
{
    private readonly string _databasePath;

    public SqliteDelayReasonRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<List<DelayReasonEntry>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
    {
        var list = new List<DelayReasonEntry>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_delay_reasons WHERE schedule_id = $schedId AND activity_id = $actId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", activityId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<DelayReasonEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task<List<DelayReasonEntry>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var list = new List<DelayReasonEntry>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_delay_reasons WHERE schedule_id = $schedId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<DelayReasonEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(DelayReasonEntry entry, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_delay_reasons (id, schedule_id, activity_id, progress_update_id, code, json_data)
            VALUES ($id, $schedId, $actId, $upd, $code, $json);";
        cmd.Parameters.AddWithValue("$id", entry.EntryId);
        cmd.Parameters.AddWithValue("$schedId", entry.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", entry.ActivityId.Value);
        cmd.Parameters.AddWithValue("$upd", entry.ProgressUpdateId);
        cmd.Parameters.AddWithValue("$code", entry.Code.ToString());
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entry, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string entryId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_delay_reasons WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", entryId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteProgressEvidenceRepository : IProgressEvidenceRepository
{
    private readonly string _databasePath;

    public SqliteProgressEvidenceRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<List<ProgressEvidence>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default)
    {
        var list = new List<ProgressEvidence>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_progress_evidences WHERE progress_update_id = $id;";
        cmd.Parameters.AddWithValue("$id", progressUpdateId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProgressEvidence>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(ProgressEvidence evidence, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_progress_evidences (id, schedule_id, activity_id, progress_update_id, type, reference, json_data)
            VALUES ($id, $schedId, $actId, $upd, $type, $ref, $json);";
        cmd.Parameters.AddWithValue("$id", evidence.EvidenceId);
        cmd.Parameters.AddWithValue("$schedId", evidence.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", (object?)evidence.ActivityId?.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$upd", evidence.ProgressUpdateId);
        cmd.Parameters.AddWithValue("$type", evidence.Type.ToString());
        cmd.Parameters.AddWithValue("$ref", evidence.Reference);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evidence, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string evidenceId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_progress_evidences WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", evidenceId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
