using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteCloseoutChecklistRepository : ICloseoutChecklistRepository
{
    private readonly string _db;
    public SqliteCloseoutChecklistRepository(string databasePath) => _db = databasePath;

    public async Task SaveTemplateAsync(CloseoutChecklistTemplate template, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.ChecklistTemplates} (id,name,description,version,is_active,is_obsolete,project_type,created_utc,created_by,modified_utc,modified_by,json_data) VALUES ($id,$n,$desc,$v,$act,$obs,$pt,$cu,$cb,$mu,$mb,$json) ON CONFLICT(id) DO UPDATE SET name=excluded.name,description=excluded.description,version=excluded.version,is_active=excluded.is_active,is_obsolete=excluded.is_obsolete,project_type=excluded.project_type,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", template.Id.Value);
        cmd.Parameters.AddWithValue("$n", template.Name);
        cmd.Parameters.AddWithValue("$desc", template.Description);
        cmd.Parameters.AddWithValue("$v", template.Version);
        cmd.Parameters.AddWithValue("$act", template.IsActive);
        cmd.Parameters.AddWithValue("$obs", template.IsObsolete);
        cmd.Parameters.AddWithValue("$pt", template.ProjectType);
        cmd.Parameters.AddWithValue("$cu", template.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", template.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", template.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", template.ModifiedBy);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(template, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutChecklistTemplate?> GetTemplateByIdAsync(ChecklistTemplateId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ChecklistTemplates} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutChecklistTemplate>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutChecklistTemplate>> ListTemplatesAsync(CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ChecklistTemplates} WHERE is_obsolete=0 ORDER BY name;";
        return await ReadJsonListAsync<CloseoutChecklistTemplate>(cmd, ct);
    }

    public async Task SaveChecklistAsync(CloseoutChecklist checklist, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Checklists} (id,closeout_id,template_id,template_version,name,created_utc,created_by,modified_utc,modified_by,row_version,json_data) VALUES ($id,$coid,$tid,$tv,$n,$cu,$cb,$mu,$mb,$rv,$json) ON CONFLICT(id) DO UPDATE SET template_id=excluded.template_id,template_version=excluded.template_version,name=excluded.name,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", checklist.Id.Value);
        cmd.Parameters.AddWithValue("$coid", checklist.CloseoutId.Value);
        cmd.Parameters.AddWithValue("$tid", checklist.TemplateId);
        cmd.Parameters.AddWithValue("$tv", checklist.TemplateVersion);
        cmd.Parameters.AddWithValue("$n", checklist.Name);
        cmd.Parameters.AddWithValue("$cu", checklist.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", checklist.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", checklist.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", checklist.ModifiedBy);
        cmd.Parameters.AddWithValue("$rv", checklist.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(checklist, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutChecklist?> GetChecklistByIdAsync(ChecklistId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Checklists} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutChecklist>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutChecklist>> ListChecklistsAsync(CloseoutId closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Checklists} WHERE closeout_id=$coid ORDER BY created_utc;";
        cmd.Parameters.AddWithValue("$coid", closeoutId.Value);
        return await ReadJsonListAsync<CloseoutChecklist>(cmd, ct);
    }

    public async Task SaveItemAsync(CloseoutChecklistItem item, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.ChecklistItems} (id,checklist_id,code,title,description,category,is_mandatory,is_blocking,responsible,due_date,status,evidence_required,linked_evidence_ids,comment,verified_by,verification_date,row_version,json_data) VALUES ($id,$clid,$code,$t,$desc,$cat,$man,$blk,$resp,$dd,$st,$er,$lei,$com,$vb,$vd,$rv,$json) ON CONFLICT(id) DO UPDATE SET code=excluded.code,title=excluded.title,description=excluded.description,category=excluded.category,is_mandatory=excluded.is_mandatory,is_blocking=excluded.is_blocking,responsible=excluded.responsible,due_date=excluded.due_date,status=excluded.status,evidence_required=excluded.evidence_required,linked_evidence_ids=excluded.linked_evidence_ids,comment=excluded.comment,verified_by=excluded.verified_by,verification_date=excluded.verification_date,row_version=excluded.row_version,json_data=excluded.json_data;";
        BindItemParams(cmd, item);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutChecklistItem?> GetItemByIdAsync(ChecklistItemId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ChecklistItems} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutChecklistItem>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutChecklistItem>> ListItemsAsync(ChecklistId checklistId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.ChecklistItems} WHERE checklist_id=$clid ORDER BY code;";
        cmd.Parameters.AddWithValue("$clid", checklistId.Value);
        return await ReadJsonListAsync<CloseoutChecklistItem>(cmd, ct);
    }

    private static void BindItemParams(SqliteCommand cmd, CloseoutChecklistItem item)
    {
        cmd.Parameters.AddWithValue("$id", item.Id.Value);
        cmd.Parameters.AddWithValue("$clid", item.ChecklistId.Value);
        cmd.Parameters.AddWithValue("$code", item.Code);
        cmd.Parameters.AddWithValue("$t", item.Title);
        cmd.Parameters.AddWithValue("$desc", item.Description);
        cmd.Parameters.AddWithValue("$cat", item.Category);
        cmd.Parameters.AddWithValue("$man", item.IsMandatory);
        cmd.Parameters.AddWithValue("$blk", item.IsBlocking);
        cmd.Parameters.AddWithValue("$resp", item.Responsible);
        cmd.Parameters.AddWithValue("$dd", item.DueDate);
        cmd.Parameters.AddWithValue("$st", (int)item.Status);
        cmd.Parameters.AddWithValue("$er", item.EvidenceRequired);
        cmd.Parameters.AddWithValue("$lei", item.LinkedEvidenceIds);
        cmd.Parameters.AddWithValue("$com", item.Comment);
        cmd.Parameters.AddWithValue("$vb", item.VerifiedBy);
        cmd.Parameters.AddWithValue("$vd", item.VerificationDate);
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
