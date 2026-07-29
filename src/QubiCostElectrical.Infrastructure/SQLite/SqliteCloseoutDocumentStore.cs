using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteCloseoutDocumentRepository : ICloseoutDocumentRepository
{
    private readonly string _db;
    public SqliteCloseoutDocumentRepository(string databasePath) => _db = databasePath;

    public async Task SaveRequirementAsync(CloseoutDocumentRequirement req, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.DocRequirements} (id,closeout_id,category,document_type,is_required,is_blocking,min_version,responsible,due_date,format,reference,status,linked_document_id,integrity_hash,approved_by,approved_date,row_version,json_data) VALUES ($id,$coid,$cat,$dt,$ir,$ib,$mv,$resp,$dd,$fmt,$ref,$st,$ldi,$ih,$ab,$ad,$rv,$json) ON CONFLICT(id) DO UPDATE SET category=excluded.category,document_type=excluded.document_type,is_required=excluded.is_required,is_blocking=excluded.is_blocking,min_version=excluded.min_version,responsible=excluded.responsible,due_date=excluded.due_date,format=excluded.format,reference=excluded.reference,status=excluded.status,linked_document_id=excluded.linked_document_id,integrity_hash=excluded.integrity_hash,approved_by=excluded.approved_by,approved_date=excluded.approved_date,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", req.Id.Value);
        cmd.Parameters.AddWithValue("$coid", req.CloseoutId);
        cmd.Parameters.AddWithValue("$cat", req.Category);
        cmd.Parameters.AddWithValue("$dt", req.DocumentType);
        cmd.Parameters.AddWithValue("$ir", req.IsRequired);
        cmd.Parameters.AddWithValue("$ib", req.IsBlocking);
        cmd.Parameters.AddWithValue("$mv", req.MinVersion);
        cmd.Parameters.AddWithValue("$resp", req.Responsible);
        cmd.Parameters.AddWithValue("$dd", req.DueDate);
        cmd.Parameters.AddWithValue("$fmt", req.Format);
        cmd.Parameters.AddWithValue("$ref", req.Reference);
        cmd.Parameters.AddWithValue("$st", (int)req.Status);
        cmd.Parameters.AddWithValue("$ldi", req.LinkedDocumentId);
        cmd.Parameters.AddWithValue("$ih", req.IntegrityHash);
        cmd.Parameters.AddWithValue("$ab", req.ApprovedBy);
        cmd.Parameters.AddWithValue("$ad", req.ApprovedDate);
        cmd.Parameters.AddWithValue("$rv", req.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(req, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutDocumentRequirement?> GetRequirementByIdAsync(DocumentRequirementId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.DocRequirements} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutDocumentRequirement>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutDocumentRequirement>> ListRequirementsAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.DocRequirements} WHERE closeout_id=$coid ORDER BY category,document_type;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutDocumentRequirement>(cmd, ct);
    }

    public async Task SaveDocumentAsync(CloseoutDocument doc, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Documents} (id,closeout_id,requirement_id,document_type,title,file_path,file_name,version,submitted_by,submitted_utc,integrity_hash,status,approved_by,approved_date,comment,row_version,json_data) VALUES ($id,$coid,$reqid,$dt,$t,$fp,$fn,$v,$sb,$su,$ih,$st,$ab,$ad,$com,$rv,$json) ON CONFLICT(id) DO UPDATE SET document_type=excluded.document_type,title=excluded.title,file_path=excluded.file_path,file_name=excluded.file_name,version=excluded.version,submitted_by=excluded.submitted_by,submitted_utc=excluded.submitted_utc,integrity_hash=excluded.integrity_hash,status=excluded.status,approved_by=excluded.approved_by,approved_date=excluded.approved_date,comment=excluded.comment,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", doc.Id.Value);
        cmd.Parameters.AddWithValue("$coid", doc.CloseoutId);
        cmd.Parameters.AddWithValue("$reqid", doc.RequirementId);
        cmd.Parameters.AddWithValue("$dt", doc.DocumentType);
        cmd.Parameters.AddWithValue("$t", doc.Title);
        cmd.Parameters.AddWithValue("$fp", doc.FilePath);
        cmd.Parameters.AddWithValue("$fn", doc.FileName);
        cmd.Parameters.AddWithValue("$v", doc.Version);
        cmd.Parameters.AddWithValue("$sb", doc.SubmittedBy);
        cmd.Parameters.AddWithValue("$su", doc.SubmittedUtc);
        cmd.Parameters.AddWithValue("$ih", doc.IntegrityHash);
        cmd.Parameters.AddWithValue("$st", (int)doc.Status);
        cmd.Parameters.AddWithValue("$ab", doc.ApprovedBy);
        cmd.Parameters.AddWithValue("$ad", doc.ApprovedDate);
        cmd.Parameters.AddWithValue("$com", doc.Comment);
        cmd.Parameters.AddWithValue("$rv", doc.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(doc, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutDocument?> GetDocumentByIdAsync(CloseoutDocumentId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Documents} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutDocument>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutDocument>> ListDocumentsAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Documents} WHERE closeout_id=$coid ORDER BY document_type,version;";
        cmd.Parameters.AddWithValue("$coid", closeoutId);
        return await ReadJsonListAsync<CloseoutDocument>(cmd, ct);
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

public sealed class SqliteCloseoutEvidenceRepository : ICloseoutEvidenceRepository
{
    private readonly string _db;
    public SqliteCloseoutEvidenceRepository(string databasePath) => _db = databasePath;

    public async Task SaveAsync(CloseoutEvidence evidence, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {CloseoutTables.Evidence} (id,evidence_type,entity_type,entity_id,file_path,file_name,version,uploaded_utc,uploaded_by,comment,hash,status,metadata,row_version,json_data) VALUES ($id,$et,$ent,$eid,$fp,$fn,$v,$uu,$ub,$com,$hash,$st,$meta,$rv,$json) ON CONFLICT(id) DO UPDATE SET evidence_type=excluded.evidence_type,entity_type=excluded.entity_type,entity_id=excluded.entity_id,file_path=excluded.file_path,file_name=excluded.file_name,version=excluded.version,uploaded_utc=excluded.uploaded_utc,uploaded_by=excluded.uploaded_by,comment=excluded.comment,hash=excluded.hash,status=excluded.status,metadata=excluded.metadata,row_version=excluded.row_version,json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", evidence.Id.Value);
        cmd.Parameters.AddWithValue("$et", evidence.EvidenceType);
        cmd.Parameters.AddWithValue("$ent", evidence.EntityType);
        cmd.Parameters.AddWithValue("$eid", evidence.EntityId);
        cmd.Parameters.AddWithValue("$fp", evidence.FilePath);
        cmd.Parameters.AddWithValue("$fn", evidence.FileName);
        cmd.Parameters.AddWithValue("$v", evidence.Version);
        cmd.Parameters.AddWithValue("$uu", evidence.UploadedUtc);
        cmd.Parameters.AddWithValue("$ub", evidence.UploadedBy);
        cmd.Parameters.AddWithValue("$com", evidence.Comment);
        cmd.Parameters.AddWithValue("$hash", evidence.Hash);
        cmd.Parameters.AddWithValue("$st", (int)evidence.Status);
        cmd.Parameters.AddWithValue("$meta", evidence.Metadata);
        cmd.Parameters.AddWithValue("$rv", evidence.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evidence, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CloseoutEvidence?> GetByIdAsync(EvidenceId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Evidence} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CloseoutEvidence>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CloseoutEvidence>> ListAsync(string entityType, string entityId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {CloseoutTables.Evidence} WHERE entity_type=$ent AND entity_id=$eid ORDER BY uploaded_utc;";
        cmd.Parameters.AddWithValue("$ent", entityType);
        cmd.Parameters.AddWithValue("$eid", entityId);
        return await ReadJsonListAsync<CloseoutEvidence>(cmd, ct);
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
