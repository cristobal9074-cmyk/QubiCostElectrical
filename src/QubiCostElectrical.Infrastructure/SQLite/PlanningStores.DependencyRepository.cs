using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteActivityDependencyRepository : IActivityDependencyRepository
{
    private readonly string _databasePath;

    public SqliteActivityDependencyRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ActivityDependency?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_dependencies WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ActivityDependency>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ActivityDependency>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var rows = new List<ActivityDependency>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_dependencies WHERE schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ActivityDependency>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<List<ActivityDependency>> GetByPredecessorAsync(ActivityId activityId, CancellationToken ct = default)
    {
        var rows = new List<ActivityDependency>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_dependencies WHERE predecessor_id = $pid;";
        cmd.Parameters.AddWithValue("$pid", activityId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ActivityDependency>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<List<ActivityDependency>> GetBySuccessorAsync(ActivityId activityId, CancellationToken ct = default)
    {
        var rows = new List<ActivityDependency>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_dependencies WHERE successor_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", activityId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ActivityDependency>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task SaveAsync(ActivityDependency dependency, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(dependency, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_dependencies (id, schedule_id, predecessor_id, successor_id, type, lag_json, is_lead, json)
                VALUES ($id, $sid, $pred, $succ, $type, $lag, $lead, $json)
                ON CONFLICT(id) DO UPDATE SET
                    schedule_id=excluded.schedule_id, predecessor_id=excluded.predecessor_id,
                    successor_id=excluded.successor_id, type=excluded.type,
                    lag_json=excluded.lag_json, is_lead=excluded.is_lead, json=excluded.json;
                """;
            cmd.Parameters.AddWithValue("$id", dependency.Id);
            cmd.Parameters.AddWithValue("$sid", dependency.ScheduleId.Value);
            cmd.Parameters.AddWithValue("$pred", dependency.PredecessorActivityId.Value);
            cmd.Parameters.AddWithValue("$succ", dependency.SuccessorActivityId.Value);
            cmd.Parameters.AddWithValue("$type", dependency.Type.ToString());
            cmd.Parameters.AddWithValue("$lag", JsonSerializer.Serialize(dependency.Lag, ProfessionalDbHelpers.JsonOptions));
            cmd.Parameters.AddWithValue("$lead", dependency.IsLead ? 1 : 0);
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM planning_dependencies WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM planning_dependencies WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }
}
