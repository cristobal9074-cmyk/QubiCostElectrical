using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteCloseoutSnapshotRepository : ICloseoutSnapshotRepository
{
    private readonly string _db;
    public SqliteCloseoutSnapshotRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(CloseoutSnapshot snapshot, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Snapshots} (id,closeout_id,status_date,checklist_data,punch_list_data,non_conformance_data,document_data,reconciliation_data,readiness_score,blocker_data,exception_data,alerts_data,source_hashes,service_version,created_by,comment,created_utc,hash,row_version,json_data) VALUES ($id,$coid,$sd,$cd,$pd,$nd,$dd,$rd,$rs,$bd,$ed,$ad,$sh,$sv,$cb,$com,$cu,$hash,$rv,$json) ON CONFLICT(id) DO UPDATE SET status_date=excluded.status_date,checklist_data=excluded.checklist_data,punch_list_data=excluded.punch_list_data,non_conformance_data=excluded.non_conformance_data,document_data=excluded.document_data,reconciliation_data=excluded.reconciliation_data,readiness_score=excluded.readiness_score,blocker_data=excluded.blocker_data,exception_data=excluded.exception_data,alerts_data=excluded.alerts_data,source_hashes=excluded.source_hashes,service_version=excluded.service_version,comment=excluded.comment,hash=excluded.hash,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", snapshot.Id.Value);
        cmd.Parameters.AddWithValue("$coid", snapshot.CloseoutId);
        cmd.Parameters.AddWithValue("$sd", snapshot.StatusDate);
        cmd.Parameters.AddWithValue("$cd", snapshot.ChecklistData);
        cmd.Parameters.AddWithValue("$pd", snapshot.PunchListData);
        cmd.Parameters.AddWithValue("$nd", snapshot.NonConformanceData);
        cmd.Parameters.AddWithValue("$dd", snapshot.DocumentData);
        cmd.Parameters.AddWithValue("$rd", snapshot.ReconciliationData);
        cmd.Parameters.AddWithValue("$rs", snapshot.ReadinessScore);
        cmd.Parameters.AddWithValue("$bd", snapshot.BlockerData);
        cmd.Parameters.AddWithValue("$ed", snapshot.ExceptionData);
        cmd.Parameters.AddWithValue("$ad", snapshot.AlertsData);
        cmd.Parameters.AddWithValue("$sh", snapshot.SourceHashes);
        cmd.Parameters.AddWithValue("$sv", snapshot.ServiceVersion);
        cmd.Parameters.AddWithValue("$cb", snapshot.CreatedBy);
        cmd.Parameters.AddWithValue("$com", snapshot.Comment);
        cmd.Parameters.AddWithValue("$cu", snapshot.CreatedUtc);
        cmd.Parameters.AddWithValue("$hash", snapshot.Hash);
        cmd.Parameters.AddWithValue("$rv", snapshot.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutSnapshot?> GetByIdAsync(CloseoutSnapshotId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Snapshots} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutSnapshot>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutSnapshot>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Snapshots} WHERE closeout_id=$coid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutSnapshot>(cmd, ct);
    }

    private static async Task<List<T>> ReadJsonListAsync<T>(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<T>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) list.Add(row);
        }
        return list;
    }
}

public sealed class SqliteCloseoutAlertRepository : ICloseoutAlertRepository
{
    private readonly string _db;
    public SqliteCloseoutAlertRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(CloseoutAlert alert, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Alerts} (id,closeout_id,code,severity,title,description,entity_type,entity_id,responsible,created_date,impact,suggested_action,status,row_version,json_data) VALUES ($id,$coid,$code,$sev,$t,$desc,$ent,$eid,$resp,$cd,$imp,$sa,$st,$rv,$json) ON CONFLICT(id) DO UPDATE SET code=excluded.code,severity=excluded.severity,title=excluded.title,description=excluded.description,entity_type=excluded.entity_type,entity_id=excluded.entity_id,responsible=excluded.responsible,impact=excluded.impact,suggested_action=excluded.suggested_action,status=excluded.status,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", alert.Id.Value);
        cmd.Parameters.AddWithValue("$coid", alert.CloseoutId);
        cmd.Parameters.AddWithValue("$code", alert.Code);
        cmd.Parameters.AddWithValue("$sev", (int)alert.Severity);
        cmd.Parameters.AddWithValue("$t", alert.Title);
        cmd.Parameters.AddWithValue("$desc", alert.Description);
        cmd.Parameters.AddWithValue("$ent", alert.EntityType);
        cmd.Parameters.AddWithValue("$eid", alert.EntityId);
        cmd.Parameters.AddWithValue("$resp", alert.Responsible);
        cmd.Parameters.AddWithValue("$cd", alert.CreatedDate);
        cmd.Parameters.AddWithValue("$imp", alert.Impact);
        cmd.Parameters.AddWithValue("$sa", alert.SuggestedAction);
        cmd.Parameters.AddWithValue("$st", alert.Status);
        cmd.Parameters.AddWithValue("$rv", alert.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(alert, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<CloseoutAlert>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Alerts} WHERE closeout_id=$coid ORDER BY created_date DESC;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutAlert>(cmd, ct);
    }

    private static async Task<List<T>> ReadJsonListAsync<T>(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<T>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) list.Add(row);
        }
        return list;
    }
}

public sealed class SqliteCloseoutAuditRepository : ICloseoutAuditRepository
{
    private readonly string _db;
    public SqliteCloseoutAuditRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(CloseoutAuditEvent evt, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Audit} (id,closeout_id,action,entity_type,entity_id,previous_value,new_value,performed_by,performed_utc,comment,json_data) VALUES ($id,$coid,$act,$ent,$eid,$pv,$nv,$pb,$pu,$com,$json);";
        cmd.Parameters.AddWithValue("$id", evt.Id);
        cmd.Parameters.AddWithValue("$coid", evt.CloseoutId);
        cmd.Parameters.AddWithValue("$act", evt.Action);
        cmd.Parameters.AddWithValue("$ent", evt.EntityType);
        cmd.Parameters.AddWithValue("$eid", evt.EntityId);
        cmd.Parameters.AddWithValue("$pv", evt.PreviousValue);
        cmd.Parameters.AddWithValue("$nv", evt.NewValue);
        cmd.Parameters.AddWithValue("$pb", evt.PerformedBy);
        cmd.Parameters.AddWithValue("$pu", evt.PerformedUtc);
        cmd.Parameters.AddWithValue("$com", evt.Comment);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evt, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<CloseoutAuditEvent>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Audit} WHERE closeout_id=$coid ORDER BY performed_utc DESC;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutAuditEvent>(cmd, ct);
    }

    private static async Task<List<T>> ReadJsonListAsync<T>(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<T>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) list.Add(row);
        }
        return list;
    }
}
