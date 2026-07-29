using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteAsBuiltStore : IAsBuiltRepository
{
    private readonly string _db;
    public SqliteAsBuiltStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(AsBuiltDeliverable d, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_asbuilt_deliverables(id,closeout_id,doc_type,title,required_version,delivered_version,author,responsible,delivery_date,approval_date,approved_by,integrity_hash,file_path,comment,status,created_utc,created_by,modified_utc,modified_by,row_version)
            VALUES($id,$co,$dt,$ti,$rv,$dv,$au,$re,$dd,$ad,$ab,$ih,$fp,$co2,$st,$cu,$cb,$mu,$mb,$rw)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,delivered_version=excluded.delivered_version,approved_by=excluded.approved_by,approval_date=excluded.approval_date,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindDeliverable(cmd, d);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AsBuiltDeliverable?> GetByIdAsync(AsBuiltDeliverableId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_asbuilt_deliverables WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapDeliverable(r) : null;
    }

    public async Task<List<AsBuiltDeliverable>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_asbuilt_deliverables WHERE closeout_id=$co ORDER BY doc_type;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapDeliverable, ct);
    }

    public async Task SaveTrainingAsync(TrainingRecord r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_training_records(id,closeout_id,topic,instructor,attendees,training_date,duration_minutes,documentation_refs,evaluation,evidence_refs,conformity,pending_items,created_utc,created_by)
            VALUES($id,$co,$to,$in,$at,$td,$dm,$dr,$ev,$er,$cn,$pi,$cu,$cb)
            ON CONFLICT(id) DO UPDATE SET topic=excluded.topic;";
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$to", r.Topic);
        cmd.Parameters.AddWithValue("$in", r.Instructor);
        cmd.Parameters.AddWithValue("$at", r.Attendees);
        cmd.Parameters.AddWithValue("$td", r.TrainingDate);
        cmd.Parameters.AddWithValue("$dm", r.DurationMinutes);
        cmd.Parameters.AddWithValue("$dr", r.DocumentationRefs);
        cmd.Parameters.AddWithValue("$ev", r.Evaluation);
        cmd.Parameters.AddWithValue("$er", r.EvidenceRefs);
        cmd.Parameters.AddWithValue("$cn", r.Conformity);
        cmd.Parameters.AddWithValue("$pi", r.PendingItems);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<TrainingRecord>> ListTrainingAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_training_records WHERE closeout_id=$co;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapTraining, ct);
    }

    private static void BindDeliverable(SqliteCommand cmd, AsBuiltDeliverable d)
    {
        cmd.Parameters.AddWithValue("$id", d.Id.Value);
        cmd.Parameters.AddWithValue("$co", d.CloseoutId);
        cmd.Parameters.AddWithValue("$dt", d.DocType);
        cmd.Parameters.AddWithValue("$ti", d.Title);
        cmd.Parameters.AddWithValue("$rv", d.RequiredVersion);
        cmd.Parameters.AddWithValue("$dv", d.DeliveredVersion);
        cmd.Parameters.AddWithValue("$au", d.Author);
        cmd.Parameters.AddWithValue("$re", d.Responsible);
        cmd.Parameters.AddWithValue("$dd", d.DeliveryDate);
        cmd.Parameters.AddWithValue("$ad", d.ApprovalDate);
        cmd.Parameters.AddWithValue("$ab", d.ApprovedBy);
        cmd.Parameters.AddWithValue("$ih", d.IntegrityHash);
        cmd.Parameters.AddWithValue("$fp", d.FilePath);
        cmd.Parameters.AddWithValue("$co2", d.Comment);
        cmd.Parameters.AddWithValue("$st", (int)d.Status);
        cmd.Parameters.AddWithValue("$cu", d.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", d.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", d.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", d.ModifiedBy);
        cmd.Parameters.AddWithValue("$rw", d.RowVersion);
    }

    private static AsBuiltDeliverable MapDeliverable(SqliteDataReader r) => new()
    { Id = AsBuiltDeliverableId.From(r.GetString(r.GetOrdinal("id"))), Title = r.GetString(r.GetOrdinal("title")), Status = (AsBuiltStatus)r.GetInt32(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static TrainingRecord MapTraining(SqliteDataReader r) => new()
    { Id = TrainingRecordId.From(r.GetString(r.GetOrdinal("id"))), Topic = r.GetString(r.GetOrdinal("topic")), Instructor = r.GetString(r.GetOrdinal("instructor")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}

public sealed class SqliteGuaranteeReleaseStore : IGuaranteeReleaseRepository
{
    private readonly string _db;
    public SqliteGuaranteeReleaseStore(string databasePath) => _db = databasePath;

    public async Task SaveRequestAsync(GuaranteeReleaseRequest r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_guarantee_releases(id,closeout_id,contract_id,guarantee_ref,guarantee_type,start_date,expiry_date,responsible,coverage,release_condition,document_ref,renewal_ref,status,requested_by,requested_utc,released_amount,currency_code,release_reason,alert,modified_utc,modified_by,row_version)
            VALUES($id,$co,$ci,$gr,$gt,$sd,$ed,$re,$cv,$rc,$dr,$rr,$st,$rb,$ru,$ra,$cc,$rr2,$al,$mu,$mb,$rw)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,released_amount=excluded.released_amount,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindGuaranteeRequest(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<GuaranteeReleaseRequest?> GetRequestByIdAsync(GuaranteeReleaseId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_guarantee_releases WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapRequest(r) : null;
    }

    public async Task<List<GuaranteeReleaseRequest>> ListRequestsAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_guarantee_releases WHERE closeout_id=$co;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapRequest, ct);
    }

    public async Task SaveApprovalAsync(GuaranteeReleaseApproval a, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_guarantee_release_approvals(id,release_id,is_approved,approved_by,approval_date,comment)
            VALUES($id,$ri,$ia,$ab,$ad,$co)
            ON CONFLICT(id) DO UPDATE SET is_approved=excluded.is_approved;";
        cmd.Parameters.AddWithValue("$id", a.Id);
        cmd.Parameters.AddWithValue("$ri", a.ReleaseId.Value);
        cmd.Parameters.AddWithValue("$ia", a.IsApproved ? 1 : 0);
        cmd.Parameters.AddWithValue("$ab", a.ApprovedBy);
        cmd.Parameters.AddWithValue("$ad", a.ApprovalDate);
        cmd.Parameters.AddWithValue("$co", a.Comment);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindGuaranteeRequest(SqliteCommand cmd, GuaranteeReleaseRequest r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$ci", r.ContractId);
        cmd.Parameters.AddWithValue("$gr", r.GuaranteeRef);
        cmd.Parameters.AddWithValue("$gt", r.GuaranteeType);
        cmd.Parameters.AddWithValue("$sd", r.StartDate);
        cmd.Parameters.AddWithValue("$ed", r.ExpiryDate);
        cmd.Parameters.AddWithValue("$re", r.Responsible);
        cmd.Parameters.AddWithValue("$cv", r.Coverage);
        cmd.Parameters.AddWithValue("$rc", r.ReleaseCondition);
        cmd.Parameters.AddWithValue("$dr", r.DocumentRef);
        cmd.Parameters.AddWithValue("$rr", r.RenewalRef);
        cmd.Parameters.AddWithValue("$st", (int)r.Status);
        cmd.Parameters.AddWithValue("$rb", r.RequestedBy);
        cmd.Parameters.AddWithValue("$ru", r.RequestedUtc);
        cmd.Parameters.AddWithValue("$ra", r.ReleasedAmount);
        cmd.Parameters.AddWithValue("$cc", r.CurrencyCode);
        cmd.Parameters.AddWithValue("$rr2", r.ReleaseReason);
        cmd.Parameters.AddWithValue("$al", r.Alert);
        cmd.Parameters.AddWithValue("$mu", r.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$rw", r.RowVersion);
    }

    private static GuaranteeReleaseRequest MapRequest(SqliteDataReader r) => new()
    { Id = GuaranteeReleaseId.From(r.GetString(r.GetOrdinal("id"))), Status = (GuaranteeReleaseStatus)r.GetInt32(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}

public sealed class SqliteRetentionReleaseStore : ICloseoutRetentionReleaseRepository
{
    private readonly string _db;
    public SqliteRetentionReleaseStore(string databasePath) => _db = databasePath;

    public async Task SaveAsync(RetentionReleaseRequest r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_retention_releases(id,closeout_id,contract_id,payment_certificate_ref,retention_amount,released_amount,pending_amount,currency_code,release_condition,status,requested_by,requested_utc,is_partial,approval_date,approved_by,comment,modified_utc,modified_by,row_version)
            VALUES($id,$co,$ci,$pc,$ra,$ra2,$pa,$cc,$rc,$st,$rb,$ru,$ip,$ad,$ab,$cm,$mu,$mb,$rw)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,released_amount=excluded.released_amount,approved_by=excluded.approved_by,approval_date=excluded.approval_date,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindRetention(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<RetentionReleaseRequest?> GetByIdAsync(RetentionReleaseId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_retention_releases WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapRetention(r) : null;
    }

    public async Task<List<RetentionReleaseRequest>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_retention_releases WHERE closeout_id=$co;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapRetention, ct);
    }

    private static void BindRetention(SqliteCommand cmd, RetentionReleaseRequest r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$ci", r.ContractId);
        cmd.Parameters.AddWithValue("$pc", r.PaymentCertificateRef);
        cmd.Parameters.AddWithValue("$ra", r.RetentionAmount);
        cmd.Parameters.AddWithValue("$ra2", r.ReleasedAmount);
        cmd.Parameters.AddWithValue("$pa", r.PendingAmount);
        cmd.Parameters.AddWithValue("$cc", r.CurrencyCode);
        cmd.Parameters.AddWithValue("$rc", r.ReleaseCondition);
        cmd.Parameters.AddWithValue("$st", (int)r.Status);
        cmd.Parameters.AddWithValue("$rb", r.RequestedBy);
        cmd.Parameters.AddWithValue("$ru", r.RequestedUtc);
        cmd.Parameters.AddWithValue("$ip", r.IsPartial ? 1 : 0);
        cmd.Parameters.AddWithValue("$ad", r.ApprovalDate);
        cmd.Parameters.AddWithValue("$ab", r.ApprovedBy);
        cmd.Parameters.AddWithValue("$cm", r.Comment);
        cmd.Parameters.AddWithValue("$mu", r.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$rw", r.RowVersion);
    }

    private static RetentionReleaseRequest MapRetention(SqliteDataReader r) => new()
    { Id = RetentionReleaseId.From(r.GetString(r.GetOrdinal("id"))), Status = (RetentionReleaseStatus)r.GetInt32(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}

public sealed class SqliteInventoryCloseoutStore : IInventoryCloseoutRepository
{
    private readonly string _db;
    public SqliteInventoryCloseoutStore(string databasePath) => _db = databasePath;

    public async Task SaveReconciliationAsync(InventoryReconciliation r, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_inventory_reconciliation(id,closeout_id,project_id,reconciliation_date,total_purchased,total_received,total_consumed,total_returned,total_surplus,total_differences,currency_code,differences_detail,comment,status,created_utc,created_by,modified_utc,modified_by,row_version)
            VALUES($id,$co,$pi,$rd,$tp,$tr,$tc,$tr2,$ts,$td,$cc,$dd,$cm,$st,$cu,$cb,$mu,$mb,$rw)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status,total_differences=excluded.total_differences,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,row_version=excluded.row_version;";
        BindReconciliation(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<InventoryReconciliation?> GetByIdAsync(InventoryCloseoutId id, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_inventory_reconciliation WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapRec(r) : null;
    }

    public async Task<List<InventoryReconciliation>> ListAsync(string closeoutId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_inventory_reconciliation WHERE closeout_id=$co;";
        cmd.Parameters.AddWithValue("$co", closeoutId);
        return await ReadListAsync(cmd, MapRec, ct);
    }

    public async Task SaveDisposalAsync(SurplusDisposal d, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO closeout_surplus_disposals(id,inventory_id,resource_code,resource_name,quantity,unit,location,destination,disposal_type,cost,responsible,authorization,evidence_ref,created_utc,created_by,row_version)
            VALUES($id,$ii,$rc,$rn,$qu,$un,$lo,$de,$dt,$co,$re,$au,$er,$cu,$cb,$rw)
            ON CONFLICT(id) DO UPDATE SET disposal_type=excluded.disposal_type;";
        BindDisposal(cmd, d);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<SurplusDisposal>> ListDisposalsAsync(InventoryCloseoutId inventoryId, CancellationToken ct)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT rowid,* FROM closeout_surplus_disposals WHERE inventory_id=$ii;";
        cmd.Parameters.AddWithValue("$ii", inventoryId.Value);
        return await ReadListAsync(cmd, MapDisp, ct);
    }

    private static void BindReconciliation(SqliteCommand cmd, InventoryReconciliation r)
    {
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$co", r.CloseoutId);
        cmd.Parameters.AddWithValue("$pi", r.ProjectId);
        cmd.Parameters.AddWithValue("$rd", r.ReconciliationDate);
        cmd.Parameters.AddWithValue("$tp", r.TotalPurchased);
        cmd.Parameters.AddWithValue("$tr", r.TotalReceived);
        cmd.Parameters.AddWithValue("$tc", r.TotalConsumed);
        cmd.Parameters.AddWithValue("$tr2", r.TotalReturned);
        cmd.Parameters.AddWithValue("$ts", r.TotalSurplus);
        cmd.Parameters.AddWithValue("$td", r.TotalDifferences);
        cmd.Parameters.AddWithValue("$cc", r.CurrencyCode);
        cmd.Parameters.AddWithValue("$dd", r.DifferencesDetail);
        cmd.Parameters.AddWithValue("$cm", r.Comment);
        cmd.Parameters.AddWithValue("$st", (int)r.Status);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", r.ModifiedUtc);
        cmd.Parameters.AddWithValue("$mb", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$rw", r.RowVersion);
    }

    private static void BindDisposal(SqliteCommand cmd, SurplusDisposal d)
    {
        cmd.Parameters.AddWithValue("$id", d.Id.Value);
        cmd.Parameters.AddWithValue("$ii", d.InventoryId.Value);
        cmd.Parameters.AddWithValue("$rc", d.ResourceCode);
        cmd.Parameters.AddWithValue("$rn", d.ResourceName);
        cmd.Parameters.AddWithValue("$qu", d.Quantity);
        cmd.Parameters.AddWithValue("$un", d.Unit);
        cmd.Parameters.AddWithValue("$lo", d.Location);
        cmd.Parameters.AddWithValue("$de", d.Destination);
        cmd.Parameters.AddWithValue("$dt", (int)d.DisposalType);
        cmd.Parameters.AddWithValue("$co", d.Cost);
        cmd.Parameters.AddWithValue("$re", d.Responsible);
        cmd.Parameters.AddWithValue("$au", d.Authorization);
        cmd.Parameters.AddWithValue("$er", d.EvidenceRef);
        cmd.Parameters.AddWithValue("$cu", d.CreatedUtc);
        cmd.Parameters.AddWithValue("$cb", d.CreatedBy);
        cmd.Parameters.AddWithValue("$rw", d.RowVersion);
    }

    private static InventoryReconciliation MapRec(SqliteDataReader r) => new()
    { Id = InventoryCloseoutId.From(r.GetString(r.GetOrdinal("id"))), Status = (InventoryCloseoutStatus)r.GetInt32(r.GetOrdinal("status")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static SurplusDisposal MapDisp(SqliteDataReader r) => new()
    { Id = SurplusDisposalId.From(r.GetString(r.GetOrdinal("id"))), DisposalType = (SurplusDisposalType)r.GetInt32(r.GetOrdinal("disposal_type")), RowVersion = r.GetString(r.GetOrdinal("row_version")) };

    private static async Task<List<T>> ReadListAsync<T>(SqliteCommand cmd, System.Func<SqliteDataReader, T> mapper, CancellationToken ct)
    {
        var list = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(mapper(reader));
        return list;
    }
}
