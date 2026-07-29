using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteLessonLearnedStore : ILessonLearnedRepository
{
    private readonly string _db;
    public SqliteLessonLearnedStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(LessonLearned l, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_lessons_learned(id,project_id,closeout_id,title,description,category,event_description,root_cause,impact,outcome,recommendation,applicability,wbs_code,activity_id,contract_id,supplier,resource_code,author,reviewer,lesson_date,evidence_ids,is_confidential,status,version,created_utc,created_by,modified_utc,modified_by,row_version)
            VALUES($id,$pi,$co,$ti,$de,$ca,$ed,$rc,$im,$ou,$re,$ap,$wc,$ac,$ci,$su,$rc2,$au,$rv,$ld,$ei,$ic,$st,$ve,$cu,$cb,$mu,$mb,$rw)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,reviewer=excluded.reviewer,recommendation=excluded.recommendation,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindLesson(cmd, l);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<LessonLearned?> GetByIdAsync(LessonLearnedId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_lessons_learned WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapLesson(r) : null;
    }

    public async Task<List<LessonLearned>> ListAsync(string? closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(closeoutId)
            ? "SELECT rowid,* FROM closeout_lessons_learned ORDER BY created_utc DESC;"
            : "SELECT rowid,* FROM closeout_lessons_learned WHERE closeout_id=$co ORDER BY created_utc DESC;";
        if (!string.IsNullOrEmpty(closeoutId))
            cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapLesson, ct);
    }

    public async Task SaveEvidenceAsync(LessonEvidence e, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_lesson_evidence(id,lesson_id,file_path,file_name,description,uploaded_by,uploaded_utc,hash)
            VALUES($id,$li,$fp,$fn,$de,$ub,$uu,$ha)
            ON CONFLICT(id) DO UPDATE SET file_name=excluded.file_name;";
        cmd.Parameters.AddWithValue("$id", e.Id.Value);
        cmd.Parameters.AddWithValue("$li", e.LessonId.Value);
        cmd.Parameters.AddWithValue("$fp", e.FilePath);
        cmd.Parameters.AddWithValue("$fn", e.FileName);
        cmd.Parameters.AddWithValue("$de", e.Description);
        cmd.Parameters.AddWithValue("$ub", e.UploadedBy);
        cmd.Parameters.AddWithValue("$uu", e.UploadedUtc);
        cmd.Parameters.AddWithValue("$ha", e.Hash);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<LessonEvidence>> ListEvidenceAsync(LessonLearnedId lessonId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_lesson_evidence WHERE lesson_id=$li;";
        cmd.Parameters.AddWithValue("$li", lessonId.Value);
        return await ReadListAsync(cmd, MapEv, ct);
    }

    private static void BindLesson(SqliteCommand cmd, LessonLearned l)
    {
        cmd.Parameters.AddWithValue("$id", l.Id.Value);
        cmd.Parameters.AddWithValue("$pi", l.ProjectId);
        cmd.Parameters.AddWithValue("$co", l.CloseoutId);
        cmd.Parameters.AddWithValue("$ti", l.Title);
        cmd.Parameters.AddWithValue("$de", l.Description);
        cmd.Parameters.AddWithValue("$ca", (int)l.Category);
        cmd.Parameters.AddWithValue("$ed", l.EventDescription);
        cmd.Parameters.AddWithValue("$rc", l.RootCause);
        cmd.Parameters.AddWithValue("$im", l.Impact);
        cmd.Parameters.AddWithValue("$ou", l.Outcome);
        cmd.Parameters.AddWithValue("$re", l.Recommendation);
        cmd.Parameters.AddWithValue("$ap", l.Applicability);
        cmd.Parameters.AddWithValue("$wc", l.WbsCode);
        cmd.Parameters.AddWithValue("$ac", l.ActivityId);
        cmd.Parameters.AddWithValue("$ci", l.ContractId);
        cmd.Parameters.AddWithValue("$su", l.Supplier);
        cmd.Parameters.AddWithValue("$rc2", l.ResourceCode);
        cmd.Parameters.AddWithValue("$au", l.Author);
        cmd.Parameters.AddWithValue("$rv", l.Reviewer);
        cmd.Parameters.AddWithValue("$ld", l.LessonDate);
        cmd.Parameters.AddWithValue("$ei", l.EvidenceIds);
        cmd.Parameters.AddWithValue("$ic", l.IsConfidential ? 1 : 0);
        cmd.Parameters.AddWithValue("$st", (int)l.Status);
        cmd.Parameters.AddWithValue("$ve", l.Version);
        cmd.Parameters.AddWithValue("$cu", l.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", l.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", l.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", l.ModifiedBy);
        cmd.Parameters.AddWithValue("$rw", l.RowVersion);
    }

    private static LessonLearned MapLesson(SqliteDataReader r) => new()
    { Id = LessonLearnedId.From(r.GetString(r.GetOrdinal("id"))), Title = r.GetString(r.GetOrdinal("title")), Category = (LessonCategory)r.GetInt32(r.GetOrdinal("category")), Status = (LessonStatus)r.GetInt32(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static LessonEvidence MapEv(SqliteDataReader r) => new()
    { Id = LessonEvidenceId.From(r.GetString(r.GetOrdinal("id"))), FileName = r.GetString(r.GetOrdinal("file_name")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}

public sealed class SqliteFinalCloseoutStore : IFinalCloseoutSnapshotRepository, IProjectArchiveManifestRepository
{
    private readonly string _db;
    public SqliteFinalCloseoutStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(FinalCloseoutSnapshot s, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_final_snapshots(id,project_id,closeout_id,close_date,project_data,final_reconciliation,kpi_data,receptions,contracts_data,procurement_data,evm_data,documents,guarantees,retentions,lessons,alerts,source_hashes,engine_versions,author,comment,created_utc,hash_sha256,is_official,closeout_version,row_version)
            VALUES($id,$pi,$co,$cd,$pd,$fr,$kd,$re,$cd2,$pd2,$ed,$do,$gu,$rt,$le,$al,$sh,$ev,$au,$cm,$cu,$hs,$io,$cv,$rw)
            ON CONFLICT(id) DO UPDATE SET is_official=excluded.is_official,row_version=excluded.row_version;";
        BindSnapshot(cmd, s);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FinalCloseoutSnapshot?> GetOfficialAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_final_snapshots WHERE closeout_id=$co AND is_official=1;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapSnap(r) : null;
    }

    public async Task<List<FinalCloseoutSnapshot>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_final_snapshots WHERE closeout_id=$co ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapSnap, ct);
    }

    public async Task SaveAsync(ProjectArchiveManifest m, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_archive_manifests(id,project_id,closeout_version,archive_date,responsible,file_location,documents_index,snapshot_ref,source_hashes,sql_schema_version,app_version,observations,retention_policy,status,created_utc,created_by,modified_utc,modified_by)
            VALUES($id,$pi,$cv,$ad,$re,$fl,$di,$sr,$sh,$ss,$av,$ob,$rp,$st,$cu,$cb,$mu,$mb)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by;";
        BindManifest(cmd, m);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProjectArchiveManifest?> GetByProjectAsync(string projectId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_archive_manifests WHERE project_id=$pi;";
        cmd.Parameters.AddWithValue("$pi", projectId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapMan(r) : null;
    }

    public async Task<List<ProjectArchiveManifest>> ListAsync(CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_archive_manifests ORDER BY created_utc DESC;";
        return await ReadListAsync(cmd, MapMan, ct);
    }

    private static void BindSnapshot(SqliteCommand cmd, FinalCloseoutSnapshot s)
    {
        cmd.Parameters.AddWithValue("$id", s.Id.Value);
        cmd.Parameters.AddWithValue("$pi", s.ProjectId);
        cmd.Parameters.AddWithValue("$co", s.CloseoutId);
        cmd.Parameters.AddWithValue("$cd", s.CloseDate);
        cmd.Parameters.AddWithValue("$pd", s.ProjectData);
        cmd.Parameters.AddWithValue("$fr", s.FinalReconciliation);
        cmd.Parameters.AddWithValue("$kd", s.KpiData);
        cmd.Parameters.AddWithValue("$re", s.Receptions);
        cmd.Parameters.AddWithValue("$cd2", s.ContractsData);
        cmd.Parameters.AddWithValue("$pd2", s.ProcurementData);
        cmd.Parameters.AddWithValue("$ed", s.EvmData);
        cmd.Parameters.AddWithValue("$do", s.Documents);
        cmd.Parameters.AddWithValue("$gu", s.Guarantees);
        cmd.Parameters.AddWithValue("$rt", s.Retentions);
        cmd.Parameters.AddWithValue("$le", s.Lessons);
        cmd.Parameters.AddWithValue("$al", s.Alerts);
        cmd.Parameters.AddWithValue("$sh", s.SourceHashes);
        cmd.Parameters.AddWithValue("$ev", s.EngineVersions);
        cmd.Parameters.AddWithValue("$au", s.Author);
        cmd.Parameters.AddWithValue("$cm", s.Comment);
        cmd.Parameters.AddWithValue("$cu", s.CreatedUtc);
        cmd.Parameters.AddWithValue("$hs", s.HashSha256);
        cmd.Parameters.AddWithValue("$io", s.IsOfficial ? 1 : 0);
        cmd.Parameters.AddWithValue("$cv", s.CloseoutVersion);
        cmd.Parameters.AddWithValue("$rw", s.RowVersion);
    }

    private static void BindManifest(SqliteCommand cmd, ProjectArchiveManifest m)
    {
        cmd.Parameters.AddWithValue("$id", m.Id.Value);
        cmd.Parameters.AddWithValue("$pi", m.ProjectId);
        cmd.Parameters.AddWithValue("$cv", m.CloseoutVersion);
        cmd.Parameters.AddWithValue("$ad", m.ArchiveDate);
        cmd.Parameters.AddWithValue("$re", m.Responsible);
        cmd.Parameters.AddWithValue("$fl", m.FileLocation);
        cmd.Parameters.AddWithValue("$di", m.DocumentsIndex);
        cmd.Parameters.AddWithValue("$sr", m.SnapshotRef);
        cmd.Parameters.AddWithValue("$sh", m.SourceHashes);
        cmd.Parameters.AddWithValue("$ss", m.SqlSchemaVersion);
        cmd.Parameters.AddWithValue("$av", m.AppVersion);
        cmd.Parameters.AddWithValue("$ob", m.Observations);
        cmd.Parameters.AddWithValue("$rp", m.RetentionPolicy);
        cmd.Parameters.AddWithValue("$st", m.Status);
        cmd.Parameters.AddWithValue("$cu", m.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", m.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", m.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", m.ModifiedBy);
    }

    private static FinalCloseoutSnapshot MapSnap(SqliteDataReader r) => new()
    { Id = FinalSnapshotId.From(r.GetString(r.GetOrdinal("id"))), IsOfficial = r.GetInt32(r.GetOrdinal("is_official")) != 0, CloseoutVersion = r.GetInt32(r.GetOrdinal("closeout_version")), HashSha256 = r.GetString(r.GetOrdinal("hash_sha256")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static ProjectArchiveManifest MapMan(SqliteDataReader r) => new()
    { Id = ProjectArchiveManifestId.From(r.GetString(r.GetOrdinal("id"))), Status = r.GetString(r.GetOrdinal("status")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}
