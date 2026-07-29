using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteContractDashboardTable
{
    internal const string Snapshots = "contract_dashboard_snapshots";
    internal const string Alerts = "contract_dashboard_alerts";
    internal const string AlertAudit = "contract_dashboard_alert_audit";
}

public sealed class SqliteContractDashboardRepository : IContractDashboardRepository
{
    private readonly string _db;
    public SqliteContractDashboardRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task<List<ContractDashboardSnapshot>> ListSnapshotsAsync(ContractId contractId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractDashboardTable.Snapshots} WHERE contract_id=$cid ORDER BY version DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadSnapshotListAsync(cmd, ct);
    }

    public async Task<ContractExecutiveDashboard?> GetSnapshotAsync(string snapshotId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractDashboardTable.Snapshots} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractExecutiveDashboard>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task SaveSnapshotAsync(ContractDashboardSnapshot snapshot, string jsonData, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractDashboardTable.Snapshots} (
                id, contract_id, version, status_date, status_label,
                sha256, author, comment, created_utc, json_data)
            VALUES ($id, $cid, $ver, $sd, $sl, $sha, $auth, $com, $cu, $json)
            ON CONFLICT(id) DO UPDATE SET
                version=excluded.version, status_date=excluded.status_date,
                status_label=excluded.status_label, sha256=excluded.sha256,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", snapshot.SnapshotId);
        cmd.Parameters.AddWithValue("$cid", snapshot.ContractId.Value);
        cmd.Parameters.AddWithValue("$ver", snapshot.Version);
        cmd.Parameters.AddWithValue("$sd", snapshot.StatusDate.ToString("O"));
        cmd.Parameters.AddWithValue("$sl", snapshot.StatusLabel);
        cmd.Parameters.AddWithValue("$sha", snapshot.Sha256);
        cmd.Parameters.AddWithValue("$auth", snapshot.Author);
        cmd.Parameters.AddWithValue("$com", snapshot.Comment);
        cmd.Parameters.AddWithValue("$cu", snapshot.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", jsonData);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractAlert?> GetAlertAsync(string alertId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractDashboardTable.Alerts} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", alertId);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractAlert>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task UpdateAlertStatusAsync(string alertId, ContractAlertStatus newStatus, string actor, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            UPDATE {SqliteContractDashboardTable.Alerts}
            SET status=$st, resolved_utc=$ru
            WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", alertId);
        cmd.Parameters.AddWithValue("$st", (int)newStatus);
        cmd.Parameters.AddWithValue("$ru", newStatus is ContractAlertStatus.Resolved or ContractAlertStatus.Dismissed ? DateTime.UtcNow.ToString("O") : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);

        // Record audit
        using var auditCmd = c.CreateCommand();
        auditCmd.CommandText = $@"
            INSERT INTO {SqliteContractDashboardTable.AlertAudit} (id, alert_id, old_status, new_status, actor, created_utc)
            VALUES ($id, $aid, $old, $new, $actor, $cu);";
        auditCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        auditCmd.Parameters.AddWithValue("$aid", alertId);
        auditCmd.Parameters.AddWithValue("$old", (int)(newStatus == ContractAlertStatus.Acknowledged ? ContractAlertStatus.New : ContractAlertStatus.Acknowledged));
        auditCmd.Parameters.AddWithValue("$new", (int)newStatus);
        auditCmd.Parameters.AddWithValue("$actor", actor);
        auditCmd.Parameters.AddWithValue("$cu", DateTime.UtcNow.ToString("O"));
        await auditCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractAlert>> GetAlertsByContractAsync(ContractId contractId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractDashboardTable.Alerts} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadAlertListAsync(cmd, ct);
    }

    private static async Task<List<ContractDashboardSnapshot>> ReadSnapshotListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractDashboardSnapshot>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var dashJson = reader.GetString(0);
            var dash = JsonSerializer.Deserialize<ContractExecutiveDashboard>(dashJson, ProfessionalDbHelpers.JsonOptions);
            // Fallback: try to build snapshot from dashboard
            if (dash != null)
            {
                list.Add(new ContractDashboardSnapshot
                {
                    SnapshotId = Guid.NewGuid().ToString("N"),
                    ContractId = dash.ContractId,
                    Version = list.Count + 1,
                    StatusDate = dash.StatusDate,
                    StatusLabel = $"v{list.Count + 1}",
                    Sha256 = "",
                    Author = "System",
                    Comment = "Snapshot from dashboard",
                    CreatedUtc = dash.GeneratedUtc
                });
            }
        }
        return list;
    }

    private static async Task<List<ContractAlert>> ReadAlertListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractAlert>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractAlert>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
