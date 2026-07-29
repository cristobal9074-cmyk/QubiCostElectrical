using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteProjectReceptionStore : IProjectReceptionRepository
{
    private readonly string _db;
    public SqliteProjectReceptionStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(ProjectReception r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_receptions(id,project_id,closeout_id,reception_type,number,title,scheduled_date,actual_date,client,contractor,inspector,responsible_team,scope,observations,document_refs,status,approved_by,approval_date,created_utc,created_by,modified_utc,modified_by,row_version)
            VALUES($id,$pi,$co,$rt,$nu,$ti,$sd,$ad,$cl,$ct,$in,$rt2,$sc,$ob,$dr,$st,$ab,$apd,$cu,$cb,$mu,$mb,$rv)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,approved_by=excluded.approved_by,approval_date=excluded.approval_date,observations=excluded.observations,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindReception(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProjectReception?> GetByIdAsync(ProjectReceptionId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_receptions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapReception(r) : null;
    }

    public async Task<List<ProjectReception>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_receptions WHERE closeout_id=$co ORDER BY number;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapReception, ct);
    }

    public async Task SaveInspectionAsync(ReceptionInspection i, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_reception_inspections(id,reception_id,inspection_date,location,scope,attendees,inspector,result,inspected_items,observations,photo_refs,document_refs,punch_item_ids,non_conformance_ids,decision,approval_signature,created_utc,created_by)
            VALUES($id,$ri,$id2,$lo,$sc,$at,$in,$re,$ii,$ob,$pr,$dr,$pi,$nc,$de,$as,$cu,$cb)
            ON CONFLICT(id) DO UPDATE SET result=excluded.result,observations=excluded.observations,decision=excluded.decision;";
        BindInspection(cmd, i);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ReceptionInspection>> ListInspectionsAsync(ProjectReceptionId receptionId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_reception_inspections WHERE reception_id=$ri;";
        cmd.Parameters.AddWithValue("$ri", receptionId.Value);
        return await ReadListAsync(cmd, MapInspection, ct);
    }

    public async Task SaveObservationAsync(ReceptionObservation o, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_reception_observations(id,reception_id,number,description,category,severity,responsible,due_date,status,punch_item_id,created_utc,created_by,row_version)
            VALUES($id,$ri,$nu,$de,$ca,$se,$re,$dd,$st,$pi,$cu,$cb,$rv)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,description=excluded.description,row_version=excluded.row_version;";
        BindObservation(cmd, o);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ReceptionObservation>> ListObservationsAsync(ProjectReceptionId receptionId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_reception_observations WHERE reception_id=$ri;";
        cmd.Parameters.AddWithValue("$ri", receptionId.Value);
        return await ReadListAsync(cmd, MapObservation, ct);
    }

    private static void BindReception(SqliteCommand cmd, ProjectReception r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$pi", r.ProjectId);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$rt", (int)r.ReceptionType);
        cmd.Parameters.AddWithValue("$nu", r.Number);
        cmd.Parameters.AddWithValue("$ti", r.Title);
        cmd.Parameters.AddWithValue("$sd", r.ScheduledDate);
        cmd.Parameters.AddWithValue("$ad", r.ActualDate);
        cmd.Parameters.AddWithValue("$cl", r.Client);
        cmd.Parameters.AddWithValue("$ct", r.Contractor);
        cmd.Parameters.AddWithValue("$in", r.Inspector);
        cmd.Parameters.AddWithValue("$rt2", r.ResponsibleTeam);
        cmd.Parameters.AddWithValue("$sc", r.Scope);
        cmd.Parameters.AddWithValue("$ob", r.Observations);
        cmd.Parameters.AddWithValue("$dr", r.DocumentRefs);
        cmd.Parameters.AddWithValue("$st", (int)r.Status);
        cmd.Parameters.AddWithValue("$ab", r.ApprovedBy);
        cmd.Parameters.AddWithValue("$apd", r.ApprovalDate);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", r.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$rv", r.RowVersion);
    }

    private static void BindInspection(SqliteCommand cmd, ReceptionInspection i)
    {
        cmd.Parameters.AddWithValue("$id", i.Id.Value);
        cmd.Parameters.AddWithValue("$ri", i.ReceptionId.Value);
        cmd.Parameters.AddWithValue("$id2", i.InspectionDate);
        cmd.Parameters.AddWithValue("$lo", i.Location);
        cmd.Parameters.AddWithValue("$sc", i.Scope);
        cmd.Parameters.AddWithValue("$at", i.Attendees);
        cmd.Parameters.AddWithValue("$in", i.Inspector);
        cmd.Parameters.AddWithValue("$re", i.Result);
        cmd.Parameters.AddWithValue("$ii", i.InspectedItems);
        cmd.Parameters.AddWithValue("$ob", i.Observations);
        cmd.Parameters.AddWithValue("$pr", i.PhotoRefs);
        cmd.Parameters.AddWithValue("$dr", i.DocumentRefs);
        cmd.Parameters.AddWithValue("$pi", i.PunchItemIds);
        cmd.Parameters.AddWithValue("$nc", i.NonConformanceIds);
        cmd.Parameters.AddWithValue("$de", i.Decision);
        cmd.Parameters.AddWithValue("$as", i.ApprovalSignature);
        cmd.Parameters.AddWithValue("$cu", i.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", i.CreatedBy);
    }

    private static void BindObservation(SqliteCommand cmd, ReceptionObservation o)
    {
        cmd.Parameters.AddWithValue("$id", o.Id.Value);
        cmd.Parameters.AddWithValue("$ri", o.ReceptionId.Value);
        cmd.Parameters.AddWithValue("$nu", o.Number);
        cmd.Parameters.AddWithValue("$de", o.Description);
        cmd.Parameters.AddWithValue("$ca", o.Category);
        cmd.Parameters.AddWithValue("$se", o.Severity);
        cmd.Parameters.AddWithValue("$re", o.Responsible);
        cmd.Parameters.AddWithValue("$dd", o.DueDate);
        cmd.Parameters.AddWithValue("$st", o.Status);
        cmd.Parameters.AddWithValue("$pi", o.PunchItemId);
        cmd.Parameters.AddWithValue("$cu", o.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", o.CreatedBy);
        cmd.Parameters.AddWithValue("$rv", o.RowVersion);
    }

    private static ProjectReception MapReception(SqliteDataReader r) => new()
    {
        Id = ProjectReceptionId.From(r.GetString(r.GetOrdinal("id"))), ProjectId = r.GetString(r.GetOrdinal("project_id")),
        CloseoutId = r.GetString(r.GetOrdinal("closeout_id")), ReceptionType = (ReceptionType)r.GetInt32(r.GetOrdinal("reception_type")),
        Number = r.GetInt32(r.GetOrdinal("number")), Title = r.GetString(r.GetOrdinal("title")),
        Status = (ReceptionStatus)r.GetInt32(r.GetOrdinal("status")),
        CreatedBy = r.GetString(r.GetOrdinal("created_by")), CreatedUtc = r.GetString(r.GetOrdinal("created_utc")),
        RowVersion = r.GetString(r.GetOrdinal("row_version"))
    };

    private static ReceptionInspection MapInspection(SqliteDataReader r) => new()
    {
        Id = ReceptionInspectionId.From(r.GetString(r.GetOrdinal("id"))),
        ReceptionId = ProjectReceptionId.From(r.GetString(r.GetOrdinal("reception_id"))),
        InspectionDate = r.GetString(r.GetOrdinal("inspection_date")), Inspector = r.GetString(r.GetOrdinal("inspector")),
        Result = r.GetString(r.GetOrdinal("result")), Decision = r.GetString(r.GetOrdinal("decision"))
    };

    private static ReceptionObservation MapObservation(SqliteDataReader r) => new()
    {
        Id = ReceptionObservationId.From(r.GetString(r.GetOrdinal("id"))),
        ReceptionId = ProjectReceptionId.From(r.GetString(r.GetOrdinal("reception_id"))),
        Number = r.GetInt32(r.GetOrdinal("number")), Description = r.GetString(r.GetOrdinal("description")),
        Status = r.GetString(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version"))
    };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}

public sealed class SqliteReceptionRecordStore : IReceptionRecordRepository
{
    private readonly string _db;
    public SqliteReceptionRecordStore(string databasePath) => _db = databasePath;

    public async Task SaveRecordAsync(ReceptionRecord r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_reception_records(id,reception_id,project_id,contract_id,reception_type,record_date,parties,delivered_scope,observations,reservations,accepted_pending_items,guarantee_refs,documentation_refs,approvers,signatures,version,hash_sha256,status,is_immutable,created_utc,created_by)
            VALUES($id,$ri,$pi,$ci,$rt,$rd,$pa,$ds,$ob,$rs,$api,$gr,$dr,$ap,$si,$ve,$hs,$st,$im,$cu,$cb)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,is_immutable=excluded.is_immutable;";
        BindRecord(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ReceptionRecord?> GetRecordByIdAsync(ReceptionRecordId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_reception_records WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapRecord(r) : null;
    }

    public async Task<List<ReceptionRecord>> ListRecordsAsync(ProjectReceptionId receptionId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_reception_records WHERE reception_id=$ri;";
        cmd.Parameters.AddWithValue("$ri", receptionId.Value);
        return await ReadListAsync(cmd, MapRecord, ct);
    }

    public async Task SaveHandoverAsync(HandoverRecord r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_handover_records(id,project_id,closeout_id,handover_date,client,contractor,delivered_scope,pending_items,documentation_refs,certificate_refs,training_refs,warranty_refs,approvers,signatures,version,hash_sha256,status,created_utc,created_by)
            VALUES($id,$pi,$co,$hd,$cl,$ct,$ds,$pi2,$dr,$cr,$tr,$wr,$ap,$si,$ve,$hs,$st,$cu,$cb)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status;";
        BindHandover(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<HandoverRecord>> ListHandoversAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_handover_records WHERE closeout_id=$co;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapHandover, ct);
    }

    public async Task SaveCertificateAsync(AcceptanceCertificate c, CancellationToken ct)
    {
        using var conn = ProfessionalDbHelpers.Open(_db);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_acceptance_certificates(id,project_id,contract_id,reception_type,certificate_date,issued_by,accepted_by,scope,observations,conditions,is_final,hash_sha256,created_utc,created_by)
            VALUES($id,$pi,$ci,$rt,$cd,$ib,$ab,$sc,$ob,$co,$if,$hs,$cu,$cb)
            ON CONFLICT(id) DO UPDATE SET is_final=excluded.is_final;";
        BindCertificate(cmd, c);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<AcceptanceCertificate>> ListCertificatesAsync(string projectId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_acceptance_certificates WHERE project_id=$pi;";
        cmd.Parameters.AddWithValue("$pi", projectId);
        return await ReadListAsync(cmd, MapCert, ct);
    }

    private static void BindRecord(SqliteCommand cmd, ReceptionRecord r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$ri", r.ReceptionId.Value);
        cmd.Parameters.AddWithValue("$pi", r.ProjectId);
        cmd.Parameters.AddWithValue("$ci", r.ContractId);
        cmd.Parameters.AddWithValue("$rt", (int)r.ReceptionType);
        cmd.Parameters.AddWithValue("$rd", r.RecordDate);
        cmd.Parameters.AddWithValue("$pa", r.Parties);
        cmd.Parameters.AddWithValue("$ds", r.DeliveredScope);
        cmd.Parameters.AddWithValue("$ob", r.Observations);
        cmd.Parameters.AddWithValue("$rs", r.Reservations);
        cmd.Parameters.AddWithValue("$api", r.AcceptedPendingItems);
        cmd.Parameters.AddWithValue("$gr", r.GuaranteeRefs);
        cmd.Parameters.AddWithValue("$dr", r.DocumentationRefs);
        cmd.Parameters.AddWithValue("$ap", r.Approvers);
        cmd.Parameters.AddWithValue("$si", r.Signatures);
        cmd.Parameters.AddWithValue("$ve", r.Version);
        cmd.Parameters.AddWithValue("$hs", r.HashSha256);
        cmd.Parameters.AddWithValue("$st", r.Status);
        cmd.Parameters.AddWithValue("$im", r.IsImmutable ? 1 : 0);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
    }

    private static void BindHandover(SqliteCommand cmd, HandoverRecord r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$pi", r.ProjectId);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$hd", r.HandoverDate);
        cmd.Parameters.AddWithValue("$cl", r.Client);
        cmd.Parameters.AddWithValue("$ct", r.Contractor);
        cmd.Parameters.AddWithValue("$ds", r.DeliveredScope);
        cmd.Parameters.AddWithValue("$pi2", r.PendingItems);
        cmd.Parameters.AddWithValue("$dr", r.DocumentationRefs);
        cmd.Parameters.AddWithValue("$cr", r.CertificateRefs);
        cmd.Parameters.AddWithValue("$tr", r.TrainingRefs);
        cmd.Parameters.AddWithValue("$wr", r.WarrantyRefs);
        cmd.Parameters.AddWithValue("$ap", r.Approvers);
        cmd.Parameters.AddWithValue("$si", r.Signatures);
        cmd.Parameters.AddWithValue("$ve", r.Version);
        cmd.Parameters.AddWithValue("$hs", r.HashSha256);
        cmd.Parameters.AddWithValue("$st", r.Status);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
    }

    private static void BindCertificate(SqliteCommand cmd, AcceptanceCertificate c)
    {
        cmd.Parameters.AddWithValue("$id", c.Id.Value);
        cmd.Parameters.AddWithValue("$pi", c.ProjectId);
        cmd.Parameters.AddWithValue("$ci", c.ContractId);
        cmd.Parameters.AddWithValue("$rt", (int)c.ReceptionType);
        cmd.Parameters.AddWithValue("$cd", c.CertificateDate);
        cmd.Parameters.AddWithValue("$ib", c.IssuedBy);
        cmd.Parameters.AddWithValue("$ab", c.AcceptedBy);
        cmd.Parameters.AddWithValue("$sc", c.Scope);
        cmd.Parameters.AddWithValue("$ob", c.Observations);
        cmd.Parameters.AddWithValue("$co", c.Conditions);
        cmd.Parameters.AddWithValue("$if", c.IsFinal ? 1 : 0);
        cmd.Parameters.AddWithValue("$hs", c.HashSha256);
        cmd.Parameters.AddWithValue("$cu", c.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", c.CreatedBy);
    }

    private static ReceptionRecord MapRecord(SqliteDataReader r) => new()
    { Id = ReceptionRecordId.From(r.GetString(r.GetOrdinal("id"))), Status = r.GetString(r.GetOrdinal("status")), IsImmutable = r.GetInt32(r.GetOrdinal("is_immutable")) != 0, CreatedBy = r.GetString(r.GetOrdinal("created_by")) };

    private static HandoverRecord MapHandover(SqliteDataReader r) => new()
    { Id = HandoverRecordId.From(r.GetString(r.GetOrdinal("id"))), Status = r.GetString(r.GetOrdinal("status")), CreatedBy = r.GetString(r.GetOrdinal("created_by")) };

    private static AcceptanceCertificate MapCert(SqliteDataReader r) => new()
    { Id = AcceptanceCertificateId.From(r.GetString(r.GetOrdinal("id"))), IsFinal = r.GetInt32(r.GetOrdinal("is_final")) != 0, CreatedBy = r.GetString(r.GetOrdinal("created_by")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}
