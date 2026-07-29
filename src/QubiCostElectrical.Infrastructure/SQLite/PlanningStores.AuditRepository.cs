using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqlitePlanningAuditRepository : IPlanningAuditRepository
{
    private readonly string _databasePath;

    public SqlitePlanningAuditRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(auditEvent, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_audit (id, actor, utc_timestamp, entity_type, entity_id, action,
                    previous_state, new_state, relevant_values, comment, result, json)
                VALUES ($id, $actor, $ts, $etype, $eid, $action, $prev, $new, $rv, $comment, $result, $json);
                """;
            cmd.Parameters.AddWithValue("$id", auditEvent.Id);
            cmd.Parameters.AddWithValue("$actor", auditEvent.Actor);
            cmd.Parameters.AddWithValue("$ts", auditEvent.UtcTimestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$etype", auditEvent.EntityType);
            cmd.Parameters.AddWithValue("$eid", auditEvent.EntityId);
            cmd.Parameters.AddWithValue("$action", auditEvent.Action);
            cmd.Parameters.AddWithValue("$prev", (object?)auditEvent.PreviousState ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$new", (object?)auditEvent.NewState ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rv", (object?)auditEvent.RelevantValues ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$comment", auditEvent.Comment);
            cmd.Parameters.AddWithValue("$result", auditEvent.Result);
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
    {
        var rows = new List<PlanningAuditEvent>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_audit WHERE entity_type = 'Schedule' AND entity_id = $eid ORDER BY utc_timestamp DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$eid", scheduleId.Value);
        cmd.Parameters.AddWithValue("$lim", limit);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<PlanningAuditEvent>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default)
    {
        var rows = new List<PlanningAuditEvent>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_audit WHERE entity_type = $etype AND entity_id = $eid ORDER BY utc_timestamp DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$etype", entityType);
        cmd.Parameters.AddWithValue("$eid", entityId);
        cmd.Parameters.AddWithValue("$lim", limit);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<PlanningAuditEvent>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }
}
