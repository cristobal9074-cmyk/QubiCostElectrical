using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteCloseoutNonConformanceStore : ICloseoutNonConformanceRepository
{
    private readonly string _db;
    public SqliteCloseoutNonConformanceStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(CloseoutNonConformance nc, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.NonConformances} (id,closeout_id,project_id,origin,description,severity,category,responsible,created_date,due_date,root_cause,correction,linked_evidence_ids,associated_cost,schedule_impact,status,verified_by,created_by,created_utc,modified_by,modified_utc,row_version,json_data) VALUES ($id,$coid,$pid,$o,$desc,$sev,$cat,$resp,$cd,$dd,$rc,$corr,$lei,$ac,$si,$st,$vb,$cb,$cu,$mb,$mu,$rv,$json) ON CONFLICT(id) DO UPDATE SET origin=excluded.origin,description=excluded.description,severity=excluded.severity,category=excluded.category,responsible=excluded.responsible,due_date=excluded.due_date,root_cause=excluded.root_cause,correction=excluded.correction,linked_evidence_ids=excluded.linked_evidence_ids,associated_cost=excluded.associated_cost,schedule_impact=excluded.schedule_impact,status=excluded.status,verified_by=excluded.verified_by,modified_by=excluded.modified_by,modified_utc=excluded.modified_utc,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", nc.Id.Value);
        cmd.Parameters.AddWithValue("$coid", nc.CloseoutId);
        cmd.Parameters.AddWithValue("$pid", nc.ProjectId);
        cmd.Parameters.AddWithValue("$o", nc.Origin);
        cmd.Parameters.AddWithValue("$desc", nc.Description);
        cmd.Parameters.AddWithValue("$sev", nc.Severity);
        cmd.Parameters.AddWithValue("$cat", nc.Category);
        cmd.Parameters.AddWithValue("$resp", nc.Responsible);
        cmd.Parameters.AddWithValue("$cd", nc.CreatedDate);
        cmd.Parameters.AddWithValue("$dd", nc.DueDate);
        cmd.Parameters.AddWithValue("$rc", nc.RootCause);
        cmd.Parameters.AddWithValue("$corr", nc.Correction);
        cmd.Parameters.AddWithValue("$lei", nc.LinkedEvidenceIds);
        cmd.Parameters.AddWithValue("$ac", nc.AssociatedCost);
        cmd.Parameters.AddWithValue("$si", nc.ScheduleImpact);
        cmd.Parameters.AddWithValue("$st", (int)nc.Status);
        cmd.Parameters.AddWithValue("$vb", nc.VerifiedBy);
        cmd.Parameters.AddWithValue("$cb", nc.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", nc.CreatedUtc);
        cmd.Parameters.AddWithValue("$mb", nc.ModifiedBy);
        cmd.Parameters.AddWithValue("$mu", nc.ModifiedUtc);
        cmd.Parameters.AddWithValue("$rv", nc.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(nc, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutNonConformance?> GetByIdAsync(NonConformanceId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.NonConformances} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutNonConformance>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutNonConformance>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.NonConformances} WHERE closeout_id=$coid ORDER BY created_date DESC;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutNonConformance>(cmd, ct);
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

public sealed class SqlitePunchListRepository : IPunchListRepository
{
    private readonly string _db;
    public SqlitePunchListRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(PunchItem item, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.PunchItems} (id,closeout_id,project_id,number,title,description,category,priority,severity,wbs_code,activity_id,contract_id,responsible,detected_date,due_date,corrected_date,verified_date,status,estimated_cost_impact,estimated_schedule_impact,linked_evidence_ids,comment,row_version,json_data) VALUES ($id,$coid,$pid,$num,$t,$desc,$cat,$pri,$sev,$wbs,$act,$cont,$resp,$dd,$dued,$cd,$vd,$st,$eci,$esi,$lei,$com,$rv,$json) ON CONFLICT(id) DO UPDATE SET title=excluded.title,description=excluded.description,category=excluded.category,priority=excluded.priority,severity=excluded.severity,responsible=excluded.responsible,due_date=excluded.due_date,corrected_date=excluded.corrected_date,verified_date=excluded.verified_date,status=excluded.status,estimated_cost_impact=excluded.estimated_cost_impact,estimated_schedule_impact=excluded.estimated_schedule_impact,linked_evidence_ids=excluded.linked_evidence_ids,comment=excluded.comment,row_version=excluded.row_version,json_data=excluded.json_data;";
        BindParams(cmd, item);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PunchItem?> GetByIdAsync(PunchItemId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.PunchItems} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PunchItem>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<PunchItem>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.PunchItems} WHERE closeout_id=$coid ORDER BY number;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<PunchItem>(cmd, ct);
    }

    private static void BindParams(SqliteCommand cmd, PunchItem item)
    {
        cmd.Parameters.AddWithValue("$id", item.Id.Value);
        cmd.Parameters.AddWithValue("$coid", item.CloseoutId);
        cmd.Parameters.AddWithValue("$pid", item.ProjectId);
        cmd.Parameters.AddWithValue("$num", item.Number);
        cmd.Parameters.AddWithValue("$t", item.Title);
        cmd.Parameters.AddWithValue("$desc", item.Description);
        cmd.Parameters.AddWithValue("$cat", item.Category);
        cmd.Parameters.AddWithValue("$pri", item.Priority);
        cmd.Parameters.AddWithValue("$sev", item.Severity);
        cmd.Parameters.AddWithValue("$wbs", item.WbsCode);
        cmd.Parameters.AddWithValue("$act", item.ActivityId);
        cmd.Parameters.AddWithValue("$cont", item.ContractId);
        cmd.Parameters.AddWithValue("$resp", item.Responsible);
        cmd.Parameters.AddWithValue("$dd", item.DetectedDate);
        cmd.Parameters.AddWithValue("$dued", item.DueDate);
        cmd.Parameters.AddWithValue("$cd", item.CorrectedDate);
        cmd.Parameters.AddWithValue("$vd", item.VerifiedDate);
        cmd.Parameters.AddWithValue("$st", (int)item.Status);
        cmd.Parameters.AddWithValue("$eci", item.EstimatedCostImpact);
        cmd.Parameters.AddWithValue("$esi", item.EstimatedScheduleImpact);
        cmd.Parameters.AddWithValue("$lei", item.LinkedEvidenceIds);
        cmd.Parameters.AddWithValue("$com", item.Comment);
        cmd.Parameters.AddWithValue("$rv", item.RowVersion);
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
