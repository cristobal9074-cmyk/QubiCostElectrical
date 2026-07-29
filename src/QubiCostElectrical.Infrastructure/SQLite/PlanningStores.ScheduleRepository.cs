using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteScheduleRepository : IScheduleRepository
{
    private readonly string _databasePath;

    public SqliteScheduleRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM project_schedules WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<Schedule>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default)
    {
        var rows = new List<Schedule>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM project_schedules ORDER BY name;";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<Schedule>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task SaveAsync(Schedule schedule, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(schedule, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO project_schedules (id, name, description, time_zone_id, default_calendar_id,
                    project_start_date, target_end_date, status, version, created_at, created_by,
                    modified_at, modified_by, row_version, remarks, compatibility_metadata, json)
                VALUES ($id, $name, $desc, $tz, $cal, $pstart, $tend, $status, $ver, $cat, $cby,
                    $mat, $mby, $rver, $rem, $cmeta, $json)
                ON CONFLICT(id) DO UPDATE SET
                    name=excluded.name, description=excluded.description, time_zone_id=excluded.time_zone_id,
                    default_calendar_id=excluded.default_calendar_id, status=excluded.status,
                    version=excluded.version, modified_at=excluded.modified_at,
                    modified_by=excluded.modified_by, row_version=excluded.row_version,
                    remarks=excluded.remarks, compatibility_metadata=excluded.compatibility_metadata,
                    json=excluded.json;
                """;
            AddScheduleParameters(cmd, schedule, json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(ScheduleId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM project_schedules WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM project_schedules WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    private static void AddScheduleParameters(SqliteCommand cmd, Schedule s, string json)
    {
        cmd.Parameters.AddWithValue("$id", s.Id.Value);
        cmd.Parameters.AddWithValue("$name", s.Name);
        cmd.Parameters.AddWithValue("$desc", s.Description);
        cmd.Parameters.AddWithValue("$tz", s.TimeZoneId);
        cmd.Parameters.AddWithValue("$cal", s.DefaultCalendarId?.Value ?? "");
        cmd.Parameters.AddWithValue("$pstart", s.ProjectStartDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$tend", s.TargetEndDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$status", s.Status.ToString());
        cmd.Parameters.AddWithValue("$ver", s.Version);
        cmd.Parameters.AddWithValue("$cat", s.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$cby", s.CreatedBy);
        cmd.Parameters.AddWithValue("$mat", s.ModifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$mby", s.ModifiedBy);
        cmd.Parameters.AddWithValue("$rver", s.RowVersion);
        cmd.Parameters.AddWithValue("$rem", s.Remarks);
        cmd.Parameters.AddWithValue("$cmeta", s.CompatibilityMetadata);
        cmd.Parameters.AddWithValue("$json", json);
    }
}
