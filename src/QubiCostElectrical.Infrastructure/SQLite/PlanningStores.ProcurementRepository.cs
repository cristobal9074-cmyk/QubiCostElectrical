using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteSupplierRepository : ISupplierRepository
{
    private readonly string _db;
    public SqliteSupplierRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(Supplier supplier, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_suppliers (id, code, status, updated_utc, json_data)
            VALUES ($id, $code, $st, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET code=excluded.code, status=excluded.status, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", supplier.Id.Value);
        cmd.Parameters.AddWithValue("$code", supplier.Code);
        cmd.Parameters.AddWithValue("$st", (int)supplier.Status);
        cmd.Parameters.AddWithValue("$utc", supplier.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(supplier, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Supplier?> GetByIdAsync(SupplierId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_suppliers WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<Supplier>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<Supplier?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_suppliers WHERE lower(code)=lower($code) LIMIT 1;";
        cmd.Parameters.AddWithValue("$code", code);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<Supplier>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<Supplier>> SearchAsync(string? text, SupplierStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT json_data FROM procurement_suppliers
            WHERE ($st IS NULL OR status=$st)
              AND ($q='' OR lower(code) LIKE '%'||lower($q)||'%' OR lower(json_data) LIKE '%'||lower($q)||'%')
            ORDER BY code LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$st", status.HasValue ? (object)(int)status.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$q", text ?? string.Empty);
        cmd.Parameters.AddWithValue("$take", take <= 0 ? 50 : take);
        cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
        var list = new List<Supplier>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = JsonSerializer.Deserialize<Supplier>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (s is not null) list.Add(s);
        }
        return list;
    }

    public async Task<List<string>> GetAllCodesAsync(CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT code FROM procurement_suppliers;";
        var list = new List<string>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(r.GetString(0));
        return list;
    }
}

public sealed class SqliteSupplierContactRepository : ISupplierContactRepository
{
    private readonly string _db;
    public SqliteSupplierContactRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(SupplierContact contact, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_supplier_contacts (id, supplier_id, is_active, json_data)
            VALUES ($id, $sid, $act, $json)
            ON CONFLICT(id) DO UPDATE SET supplier_id=excluded.supplier_id, is_active=excluded.is_active, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", contact.ContactId);
        cmd.Parameters.AddWithValue("$sid", contact.SupplierId.Value);
        cmd.Parameters.AddWithValue("$act", contact.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(contact, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<SupplierContact>> GetBySupplierAsync(SupplierId supplierId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_supplier_contacts WHERE supplier_id=$sid;";
        cmd.Parameters.AddWithValue("$sid", supplierId.Value);
        var list = new List<SupplierContact>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<SupplierContact>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    public async Task<SupplierContact?> GetByIdAsync(string contactId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_supplier_contacts WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", contactId);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<SupplierContact>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task DeleteAsync(string contactId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM procurement_supplier_contacts WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", contactId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqlitePurchaseRequisitionRepository : IPurchaseRequisitionRepository
{
    private readonly string _db;
    public SqlitePurchaseRequisitionRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PurchaseRequisition requisition, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_requisitions (id, project_path, number, status, required_date, updated_utc, json_data)
            VALUES ($id, $proj, $num, $st, $rd, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET number=excluded.number, status=excluded.status, required_date=excluded.required_date, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", requisition.Id.Value);
        cmd.Parameters.AddWithValue("$proj", requisition.ProjectPath);
        cmd.Parameters.AddWithValue("$num", requisition.Number);
        cmd.Parameters.AddWithValue("$st", (int)requisition.Status);
        cmd.Parameters.AddWithValue("$rd", requisition.RequiredDate.ToString("O"));
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(requisition, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PurchaseRequisition?> GetByIdAsync(PurchaseRequisitionId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_requisitions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PurchaseRequisition>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<PurchaseRequisition>> SearchAsync(string projectPath, PurchaseRequisitionStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT json_data FROM procurement_requisitions
            WHERE project_path=$proj AND ($st IS NULL OR status=$st)
            ORDER BY number DESC LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        cmd.Parameters.AddWithValue("$st", status.HasValue ? (object)(int)status.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$take", take <= 0 ? 50 : take);
        cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
        var list = new List<PurchaseRequisition>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<PurchaseRequisition>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM procurement_requisitions WHERE project_path=$proj;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) + 1;
    }
}

public sealed class SqliteRequestForQuotationRepository : IRequestForQuotationRepository
{
    private readonly string _db;
    public SqliteRequestForQuotationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(RequestForQuotation rfq, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_rfqs (id, project_path, number, status, deadline_utc, updated_utc, json_data)
            VALUES ($id, $proj, $num, $st, $dl, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET number=excluded.number, status=excluded.status, deadline_utc=excluded.deadline_utc, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", rfq.Id.Value);
        cmd.Parameters.AddWithValue("$proj", rfq.ProjectPath);
        cmd.Parameters.AddWithValue("$num", rfq.Number);
        cmd.Parameters.AddWithValue("$st", (int)rfq.Status);
        cmd.Parameters.AddWithValue("$dl", rfq.DeadlineUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(rfq, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<RequestForQuotation?> GetByIdAsync(RequestForQuotationId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_rfqs WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<RequestForQuotation>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<RequestForQuotation>> SearchAsync(string projectPath, RfqStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT json_data FROM procurement_rfqs
            WHERE project_path=$proj AND ($st IS NULL OR status=$st)
            ORDER BY number DESC LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        cmd.Parameters.AddWithValue("$st", status.HasValue ? (object)(int)status.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$take", take <= 0 ? 50 : take);
        cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
        var list = new List<RequestForQuotation>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<RequestForQuotation>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM procurement_rfqs WHERE project_path=$proj;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) + 1;
    }
}

public sealed class SqliteQuotationResponseRepository : IQuotationResponseRepository
{
    private readonly string _db;
    public SqliteQuotationResponseRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(QuotationResponse response, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_quotation_responses (id, rfq_id, supplier_id, received_utc, json_data)
            VALUES ($id, $rfq, $sid, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", response.ResponseId);
        cmd.Parameters.AddWithValue("$rfq", response.RfqId.Value);
        cmd.Parameters.AddWithValue("$sid", response.SupplierId.Value);
        cmd.Parameters.AddWithValue("$utc", response.ReceivedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(response, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<QuotationResponse>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_quotation_responses WHERE rfq_id=$rfq;";
        cmd.Parameters.AddWithValue("$rfq", rfqId.Value);
        var list = new List<QuotationResponse>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<QuotationResponse>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteProcurementDocumentRepository : IProcurementDocumentRepository
{
    private readonly string _db;
    public SqliteProcurementDocumentRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ProcurementDocumentRef doc, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_documents (id, entity_type, entity_id, kind, json_data)
            VALUES ($id, $et, $eid, $k, $json)
            ON CONFLICT(id) DO UPDATE SET json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", doc.DocumentId);
        cmd.Parameters.AddWithValue("$et", doc.EntityType);
        cmd.Parameters.AddWithValue("$eid", doc.EntityId);
        cmd.Parameters.AddWithValue("$k", (int)doc.Kind);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(doc, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ProcurementDocumentRef>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_documents WHERE entity_type=$et AND entity_id=$eid;";
        cmd.Parameters.AddWithValue("$et", entityType);
        cmd.Parameters.AddWithValue("$eid", entityId);
        var list = new List<ProcurementDocumentRef>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProcurementDocumentRef>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteProcurementAuditRepository : IProcurementAuditRepository
{
    private readonly string _db;
    public SqliteProcurementAuditRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task RecordAsync(ProcurementAuditEvent evt, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_audit (id, entity_type, entity_id, action, actor, utc_ts, json_data)
            VALUES ($id, $et, $eid, $act, $actor, $utc, $json);";
        cmd.Parameters.AddWithValue("$id", evt.Id);
        cmd.Parameters.AddWithValue("$et", evt.EntityType);
        cmd.Parameters.AddWithValue("$eid", evt.EntityId);
        cmd.Parameters.AddWithValue("$act", evt.Action);
        cmd.Parameters.AddWithValue("$actor", evt.Actor);
        cmd.Parameters.AddWithValue("$utc", evt.UtcTimestamp.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evt, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ProcurementAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 100, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_audit WHERE entity_type=$et AND entity_id=$eid ORDER BY utc_ts DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$et", entityType);
        cmd.Parameters.AddWithValue("$eid", entityId);
        cmd.Parameters.AddWithValue("$lim", limit);
        var list = new List<ProcurementAuditEvent>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProcurementAuditEvent>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}
