using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteStatusDateRepository : IStatusDateRepository
{
    private readonly string _databasePath;

    public SqliteStatusDateRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<ScheduleStatusDate?> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_status_dates WHERE schedule_id = $schedId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<ScheduleStatusDate>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task SaveAsync(ScheduleStatusDate statusDate, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO planning_status_dates (id, schedule_id, status_date, time_zone_id, updated_utc, updated_by, comment, row_version, json_data)
            VALUES ($id, $schedId, $statusDate, $tz, $updatedUtc, $updatedBy, $comment, $rv, $json)
            ON CONFLICT(schedule_id) DO UPDATE SET
                id = excluded.id,
                status_date = excluded.status_date,
                time_zone_id = excluded.time_zone_id,
                updated_utc = excluded.updated_utc,
                updated_by = excluded.updated_by,
                comment = excluded.comment,
                row_version = excluded.row_version,
                json_data = excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", statusDate.Id);
        cmd.Parameters.AddWithValue("$schedId", statusDate.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$statusDate", statusDate.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$tz", statusDate.TimeZoneId);
        cmd.Parameters.AddWithValue("$updatedUtc", statusDate.UpdatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedBy", statusDate.UpdatedBy);
        cmd.Parameters.AddWithValue("$comment", statusDate.Comment);
        cmd.Parameters.AddWithValue("$rv", statusDate.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(statusDate, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteActivityProgressRepository : IActivityProgressRepository
{
    private readonly string _databasePath;

    public SqliteActivityProgressRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<ActivityProgressUpdate?> GetByIdAsync(string progressUpdateId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_activity_progress WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", progressUpdateId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<ActivityProgressUpdate>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<ActivityProgressUpdate?> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_activity_progress WHERE schedule_id = $schedId AND activity_id = $actId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", activityId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<ActivityProgressUpdate>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<List<ActivityProgressUpdate>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var list = new List<ActivityProgressUpdate>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_activity_progress WHERE schedule_id = $schedId;";
        cmd.Parameters.AddWithValue("$schedId", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ActivityProgressUpdate>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveAsync(ActivityProgressUpdate update, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO planning_activity_progress (id, schedule_id, activity_id, status_date, row_version, json_data)
            VALUES ($id, $schedId, $actId, $statusDate, $rv, $json)
            ON CONFLICT(schedule_id, activity_id) DO UPDATE SET
                id = excluded.id,
                status_date = excluded.status_date,
                row_version = excluded.row_version,
                json_data = excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", update.ProgressUpdateId);
        cmd.Parameters.AddWithValue("$schedId", update.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$actId", update.ActivityId.Value);
        cmd.Parameters.AddWithValue("$statusDate", update.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$rv", update.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(update, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string progressUpdateId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM planning_activity_progress WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", progressUpdateId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
