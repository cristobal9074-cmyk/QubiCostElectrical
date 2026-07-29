using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class CloseoutTables
{
    internal const string ProjectCloseouts = "project_closeouts";
    internal const string ChecklistTemplates = "closeout_checklist_templates";
    internal const string Checklists = "closeout_checklists";
    internal const string ChecklistItems = "closeout_checklist_items";
    internal const string PunchItems = "closeout_punch_items";
    internal const string NonConformances = "closeout_non_conformances";
    internal const string DocRequirements = "closeout_document_requirements";
    internal const string Documents = "closeout_documents";
    internal const string Evidence = "closeout_evidence";
    internal const string Meetings = "closeout_meetings";
    internal const string Exceptions = "closeout_exceptions";
    internal const string Snapshots = "closeout_snapshots";
    internal const string Alerts = "closeout_alerts";
    internal const string Audit = "closeout_audit";
}

public sealed class SqliteProjectCloseoutRepository : IProjectCloseoutRepository
{
    private readonly string _db;
    public SqliteProjectCloseoutRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(ProjectCloseout closeout, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.ProjectCloseouts} (id,project_id,code,name,description,responsible,target_date,status_date,status,readiness_pct,risk_level,active_checklist_id,punch_item_count,non_conformance_count,pending_documents,blockers,comment,version,hash,created_utc,created_by,modified_utc,modified_by,row_version) VALUES ($id,$pid,$code,$name,$desc,$resp,$td,$sd,$st,$rp,$rl,$ac,$pic,$ncc,$pd,$blk,$com,$ver,$hash,$cu,$cb,$mu,$mb,$rv) ON CONFLICT(id) DO UPDATE SET project_id=excluded.project_id,code=excluded.code,name=excluded.name,description=excluded.description,responsible=excluded.responsible,target_date=excluded.target_date,status_date=excluded.status_date,status=excluded.status,readiness_pct=excluded.readiness_pct,risk_level=excluded.risk_level,active_checklist_id=excluded.active_checklist_id,punch_item_count=excluded.punch_item_count,non_conformance_count=excluded.non_conformance_count,pending_documents=excluded.pending_documents,blockers=excluded.blockers,comment=excluded.comment,version=excluded.version,hash=excluded.hash,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        cmd.Parameters.AddWithValue("$id", closeout.Id.Value);
        cmd.Parameters.AddWithValue("$pid", closeout.ProjectId);
        cmd.Parameters.AddWithValue("$code", closeout.Code);
        cmd.Parameters.AddWithValue("$name", closeout.Name);
        cmd.Parameters.AddWithValue("$desc", closeout.Description);
        cmd.Parameters.AddWithValue("$resp", closeout.Responsible);
        cmd.Parameters.AddWithValue("$td", closeout.TargetDate);
        cmd.Parameters.AddWithValue("$sd", closeout.StatusDate);
        cmd.Parameters.AddWithValue("$st", (int)closeout.Status);
        cmd.Parameters.AddWithValue("$rp", closeout.ReadinessPct);
        cmd.Parameters.AddWithValue("$rl", closeout.RiskLevel);
        cmd.Parameters.AddWithValue("$ac", closeout.ActiveChecklistId);
        cmd.Parameters.AddWithValue("$pic", closeout.PunchItemCount);
        cmd.Parameters.AddWithValue("$ncc", closeout.NonConformanceCount);
        cmd.Parameters.AddWithValue("$pd", closeout.PendingDocuments);
        cmd.Parameters.AddWithValue("$blk", closeout.Blockers);
        cmd.Parameters.AddWithValue("$com", closeout.Comment);
        cmd.Parameters.AddWithValue("$ver", closeout.Version);
        cmd.Parameters.AddWithValue("$hash", closeout.Hash);
        cmd.Parameters.AddWithValue("$cu", closeout.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", closeout.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", closeout.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", closeout.ModifiedBy);
        cmd.Parameters.AddWithValue("$rv", closeout.RowVersion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProjectCloseout?> GetByIdAsync(CloseoutId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ProjectCloseouts} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ProjectCloseout>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<ProjectCloseout?> GetByProjectAsync(string projectId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ProjectCloseouts} WHERE project_id=$pid LIMIT 1;";
        cmd.Parameters.AddWithValue("$pid", projectId);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ProjectCloseout>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ProjectCloseout>> ListAsync(string? projectId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(projectId)
            ? $"SELECT json_data FROM {CloseoutTables.ProjectCloseouts} ORDER BY created_utc DESC;"
            : $"SELECT json_data FROM {CloseoutTables.ProjectCloseouts} WHERE project_id=$pid ORDER BY created_utc DESC;";
        if (!string.IsNullOrEmpty(projectId))
            cmd.Parameters.AddWithValue("$pid", projectId);
        return await ReadJsonListAsync<ProjectCloseout>(cmd, ct);
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
