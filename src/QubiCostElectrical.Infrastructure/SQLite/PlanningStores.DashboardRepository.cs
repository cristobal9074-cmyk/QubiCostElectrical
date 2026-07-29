using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteDashboardSnapshotRepository : IDashboardSnapshotRepository
{
    private readonly string _databasePath;
    public SqliteDashboardSnapshotRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(DashboardSnapshot snapshot, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO planning_dashboard_snapshots
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

    public async Task<DashboardSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_dashboard_snapshots WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<DashboardSnapshot>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<List<DashboardSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_dashboard_snapshots WHERE schedule_id = $sid ORDER BY version ASC;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var list = new List<DashboardSnapshot>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<DashboardSnapshot>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    public async Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(version), -1) FROM planning_dashboard_snapshots WHERE schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) + 1;
    }
}

public sealed class SqliteDashboardAlertRepository : IDashboardAlertRepository
{
    private readonly string _databasePath;
    public SqliteDashboardAlertRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task ReplaceOpenAlertsAsync(ScheduleId scheduleId, IReadOnlyList<DashboardAlert> alerts, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var tx = connection.BeginTransaction();
        using (var del = connection.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM planning_dashboard_alerts WHERE schedule_id = $sid AND lifecycle = $open;";
            del.Parameters.AddWithValue("$sid", scheduleId.Value);
            del.Parameters.AddWithValue("$open", (int)DashboardAlertLifecycle.Open);
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var alert in alerts)
            await InsertAsync(connection, tx, alert, ct);
        tx.Commit();
    }

    public async Task SaveAsync(DashboardAlert alert, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var tx = connection.BeginTransaction();
        using (var del = connection.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM planning_dashboard_alerts WHERE id = $id;";
            del.Parameters.AddWithValue("$id", alert.AlertId);
            await del.ExecuteNonQueryAsync(ct);
        }
        await InsertAsync(connection, tx, alert, ct);
        tx.Commit();
    }

    public async Task<DashboardAlert?> GetByIdAsync(string alertId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_dashboard_alerts WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", alertId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<DashboardAlert>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }

    public async Task<List<DashboardAlert>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_dashboard_alerts WHERE schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var list = new List<DashboardAlert>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<DashboardAlert>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return DashboardAlertBuilder.Prioritize(list);
    }

    private static async Task InsertAsync(SqliteConnection connection, SqliteTransaction tx, DashboardAlert alert, CancellationToken ct)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            INSERT INTO planning_dashboard_alerts
                (id, schedule_id, code, severity, lifecycle, raised_utc, json_data)
            VALUES ($id, $sid, $code, $sev, $life, $utc, $json);";
        cmd.Parameters.AddWithValue("$id", alert.AlertId);
        cmd.Parameters.AddWithValue("$sid", alert.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$code", alert.Code);
        cmd.Parameters.AddWithValue("$sev", (int)alert.Severity);
        cmd.Parameters.AddWithValue("$life", (int)alert.Lifecycle);
        cmd.Parameters.AddWithValue("$utc", alert.RaisedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(alert, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteDashboardPreferenceRepository : IDashboardPreferenceRepository
{
    private readonly string _databasePath;
    public SqliteDashboardPreferenceRepository(string databasePath) =>
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(DashboardUserPreferences prefs, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO planning_dashboard_preferences (user_key, schedule_id, updated_utc, json_data)
            VALUES ($uk, $sid, $utc, $json)
            ON CONFLICT(user_key, schedule_id) DO UPDATE SET
                updated_utc = excluded.updated_utc,
                json_data = excluded.json_data;";
        cmd.Parameters.AddWithValue("$uk", prefs.UserKey);
        cmd.Parameters.AddWithValue("$sid", prefs.ScheduleId!.Value);
        cmd.Parameters.AddWithValue("$utc", prefs.UpdatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(prefs, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<DashboardUserPreferences?> GetAsync(string userKey, ScheduleId scheduleId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM planning_dashboard_preferences WHERE user_key = $uk AND schedule_id = $sid;";
        cmd.Parameters.AddWithValue("$uk", userKey);
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json
            ? JsonSerializer.Deserialize<DashboardUserPreferences>(json, ProfessionalDbHelpers.JsonOptions)
            : null;
    }
}
