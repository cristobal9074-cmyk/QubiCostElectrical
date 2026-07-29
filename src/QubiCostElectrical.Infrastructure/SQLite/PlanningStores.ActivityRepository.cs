using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteActivityRepository : IActivityRepository
{
    private readonly string _databasePath;

    public SqliteActivityRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_activities WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<Activity>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var rows = new List<Activity>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_activities WHERE schedule_id = $sid AND is_deleted = 0 ORDER BY code;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<Activity>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default)
    {
        var rows = new List<Activity>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_activities WHERE wbs_node_id = $wid AND is_deleted = 0 ORDER BY code;";
        cmd.Parameters.AddWithValue("$wid", wbsNodeId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<Activity>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);

        var conditions = new List<string> { "is_deleted = 0" };
        var parameters = new Dictionary<string, object>();

        if (request.ScheduleId != null)
        { conditions.Add("schedule_id = $sid"); parameters["$sid"] = request.ScheduleId.Value; }
        if (request.Status.HasValue)
        { conditions.Add("status = $status"); parameters["$status"] = request.Status.Value.ToString(); }
        if (request.Type.HasValue)
        { conditions.Add("type = $type"); parameters["$type"] = request.Type.Value.ToString(); }
        if (!string.IsNullOrEmpty(request.ResponsibleId))
        { conditions.Add("responsible_id = $rid"); parameters["$rid"] = request.ResponsibleId; }
        if (!string.IsNullOrEmpty(request.Query))
        {
            conditions.Add("(name LIKE $q OR code LIKE $q)");
            parameters["$q"] = $"%{request.Query}%";
        }

        var where = "WHERE " + string.Join(" AND ", conditions);

        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = $"SELECT COUNT(*) FROM planning_activities {where};";
            foreach (var (k, v) in parameters) countCmd.Parameters.AddWithValue(k, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT json FROM planning_activities {where} ORDER BY code LIMIT {request.MaxResults} OFFSET {request.Offset};";
            foreach (var (k, v) in parameters) cmd.Parameters.AddWithValue(k, v);

            var activities = new List<Activity>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var activity = JsonSerializer.Deserialize<Activity>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
                if (activity != null) activities.Add(activity);
            }

            using var allCmd = connection.CreateCommand();
            allCmd.CommandText = "SELECT COUNT(*) FROM planning_activities WHERE is_deleted = 0;";
            var totalAll = Convert.ToInt32(await allCmd.ExecuteScalarAsync(ct));

            return new ActivitySearchResult
            {
                Activities = activities.AsReadOnly(),
                TotalCount = totalAll,
                FilteredCount = total
            };
        }
    }

    public async Task SaveAsync(Activity activity, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(activity, ProfessionalDbHelpers.JsonOptions);
            var durationJson = JsonSerializer.Serialize(activity.PlannedDuration, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_activities (id, schedule_id, wbs_node_id, code, name, description,
                    type, planned_duration_json, calendar_id, planned_start, planned_finish,
                    min_date, max_date, priority, status, responsible_id, planned_progress_pct,
                    planned_quantity, quantity_unit, tags, remarks, author, last_editor,
                    created_at, modified_at, row_version, is_deleted, json)
                VALUES ($id, $sid, $wid, $code, $name, $desc, $type, $dur, $cal, $pstart, $pfinish,
                    $mind, $maxd, $pri, $status, $rid, $progress, $qty, $qunit, $tags, $rem,
                    $author, $leditor, $cat, $mat, $rver, $del, $json)
                ON CONFLICT(id) DO UPDATE SET
                    code=excluded.code, name=excluded.name, description=excluded.description,
                    type=excluded.type, planned_duration_json=excluded.planned_duration_json,
                    calendar_id=excluded.calendar_id, planned_start=excluded.planned_start,
                    planned_finish=excluded.planned_finish, min_date=excluded.min_date,
                    max_date=excluded.max_date, priority=excluded.priority, status=excluded.status,
                    responsible_id=excluded.responsible_id,
                    planned_progress_pct=excluded.planned_progress_pct,
                    planned_quantity=excluded.planned_quantity, quantity_unit=excluded.quantity_unit,
                    tags=excluded.tags, remarks=excluded.remarks, last_editor=excluded.last_editor,
                    modified_at=excluded.modified_at, row_version=excluded.row_version,
                    is_deleted=excluded.is_deleted, json=excluded.json;
                """;
            AddActivityParameters(cmd, activity, json, durationJson);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default)
    {
        if (activities.Count == 0) return;
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_activities (id, schedule_id, wbs_node_id, code, name, description,
                    type, planned_duration_json, calendar_id, planned_start, planned_finish,
                    min_date, max_date, priority, status, responsible_id, planned_progress_pct,
                    planned_quantity, quantity_unit, tags, remarks, author, last_editor,
                    created_at, modified_at, row_version, is_deleted, json)
                VALUES ($id, $sid, $wid, $code, $name, $desc, $type, $dur, $cal, $pstart, $pfinish,
                    $mind, $maxd, $pri, $status, $rid, $progress, $qty, $qunit, $tags, $rem,
                    $author, $leditor, $cat, $mat, $rver, $del, $json)
                ON CONFLICT(id) DO UPDATE SET
                    code=excluded.code, name=excluded.name, description=excluded.description,
                    type=excluded.type, planned_duration_json=excluded.planned_duration_json,
                    calendar_id=excluded.calendar_id, planned_start=excluded.planned_start,
                    planned_finish=excluded.planned_finish, min_date=excluded.min_date,
                    max_date=excluded.max_date, priority=excluded.priority, status=excluded.status,
                    responsible_id=excluded.responsible_id,
                    planned_progress_pct=excluded.planned_progress_pct,
                    planned_quantity=excluded.planned_quantity, quantity_unit=excluded.quantity_unit,
                    tags=excluded.tags, remarks=excluded.remarks, last_editor=excluded.last_editor,
                    modified_at=excluded.modified_at, row_version=excluded.row_version,
                    is_deleted=excluded.is_deleted, json=excluded.json;
                """;
            AddActivityParameters(cmd, activities[0], "", "");
            cmd.Prepare();
            foreach (var activity in activities)
            {
                var json = JsonSerializer.Serialize(activity, ProfessionalDbHelpers.JsonOptions);
                var durationJson = JsonSerializer.Serialize(activity.PlannedDuration, ProfessionalDbHelpers.JsonOptions);
                cmd.Parameters["$id"].Value = activity.Id.Value;
                cmd.Parameters["$sid"].Value = activity.ScheduleId.Value;
                cmd.Parameters["$wid"].Value = activity.WbsNodeId.Value;
                cmd.Parameters["$code"].Value = activity.Code;
                cmd.Parameters["$name"].Value = activity.Name;
                cmd.Parameters["$desc"].Value = activity.Description;
                cmd.Parameters["$type"].Value = activity.Type.ToString();
                cmd.Parameters["$dur"].Value = durationJson;
                cmd.Parameters["$cal"].Value = activity.CalendarId?.Value ?? "";
                cmd.Parameters["$pstart"].Value = activity.PlannedStart?.ToString("O") ?? "";
                cmd.Parameters["$pfinish"].Value = activity.PlannedFinish?.ToString("O") ?? "";
                cmd.Parameters["$mind"].Value = activity.MinDate?.ToString("O") ?? "";
                cmd.Parameters["$maxd"].Value = activity.MaxDate?.ToString("O") ?? "";
                cmd.Parameters["$pri"].Value = activity.Priority;
                cmd.Parameters["$status"].Value = activity.Status.ToString();
                cmd.Parameters["$rid"].Value = activity.ResponsibleId ?? "";
                cmd.Parameters["$progress"].Value = activity.PlannedProgressPct;
                cmd.Parameters["$qty"].Value = (object?)activity.PlannedQuantity ?? DBNull.Value;
                cmd.Parameters["$qunit"].Value = activity.QuantityUnit;
                cmd.Parameters["$tags"].Value = activity.Tags;
                cmd.Parameters["$rem"].Value = activity.Remarks;
                cmd.Parameters["$author"].Value = activity.Author;
                cmd.Parameters["$leditor"].Value = activity.LastEditor;
                cmd.Parameters["$cat"].Value = activity.CreatedAt.ToString("O");
                cmd.Parameters["$mat"].Value = activity.ModifiedAt.ToString("O");
                cmd.Parameters["$rver"].Value = activity.RowVersion;
                cmd.Parameters["$del"].Value = activity.IsDeleted ? 1 : 0;
                cmd.Parameters["$json"].Value = json;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            if (softDelete)
            {
                cmd.CommandText = "UPDATE planning_activities SET is_deleted = 1 WHERE id = $id;";
            }
            else
            {
                cmd.CommandText = "DELETE FROM planning_activities WHERE id = $id;";
            }
            cmd.Parameters.AddWithValue("$id", id.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT code FROM planning_activities WHERE schedule_id = $sid AND is_deleted = 0 ORDER BY length(code) DESC, code DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is string lastCode && !string.IsNullOrEmpty(lastCode))
        {
            if (int.TryParse(lastCode, out var num))
                return (num + 1).ToString("D4");
            return lastCode + "-1";
        }
        return "0001";
    }

    private static void AddActivityParameters(SqliteCommand cmd, Activity a, string json, string durationJson)
    {
        cmd.Parameters.AddWithValue("$id", a.Id.Value);
        cmd.Parameters.AddWithValue("$sid", a.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$wid", a.WbsNodeId.Value);
        cmd.Parameters.AddWithValue("$code", a.Code);
        cmd.Parameters.AddWithValue("$name", a.Name);
        cmd.Parameters.AddWithValue("$desc", a.Description);
        cmd.Parameters.AddWithValue("$type", a.Type.ToString());
        cmd.Parameters.AddWithValue("$dur", durationJson);
        cmd.Parameters.AddWithValue("$cal", a.CalendarId?.Value ?? "");
        cmd.Parameters.AddWithValue("$pstart", a.PlannedStart?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$pfinish", a.PlannedFinish?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$mind", a.MinDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$maxd", a.MaxDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$pri", a.Priority);
        cmd.Parameters.AddWithValue("$status", a.Status.ToString());
        cmd.Parameters.AddWithValue("$rid", a.ResponsibleId ?? "");
        cmd.Parameters.AddWithValue("$progress", a.PlannedProgressPct);
        cmd.Parameters.AddWithValue("$qty", (object?)a.PlannedQuantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$qunit", a.QuantityUnit);
        cmd.Parameters.AddWithValue("$tags", a.Tags);
        cmd.Parameters.AddWithValue("$rem", a.Remarks);
        cmd.Parameters.AddWithValue("$author", a.Author);
        cmd.Parameters.AddWithValue("$leditor", a.LastEditor);
        cmd.Parameters.AddWithValue("$cat", a.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$mat", a.ModifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$rver", a.RowVersion);
        cmd.Parameters.AddWithValue("$del", a.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", json);
    }
}
