using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractRepository : IContractRepository
{
    private readonly string _db;
    public SqliteContractRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(Contract contract, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contracts (id, project_path, visible_number, status, updated_utc, json_data)
            VALUES ($id, $proj, $vis, $st, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET project_path=excluded.project_path, visible_number=excluded.visible_number, status=excluded.status, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", contract.Id.Value);
        cmd.Parameters.AddWithValue("$proj", contract.ProjectPath);
        cmd.Parameters.AddWithValue("$vis", contract.VisibleNumber);
        cmd.Parameters.AddWithValue("$st", (int)contract.Status);
        cmd.Parameters.AddWithValue("$utc", contract.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(contract, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contracts WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<Contract>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contracts WHERE lower(visible_number)=lower($vis) LIMIT 1;";
        cmd.Parameters.AddWithValue("$vis", visibleNumber);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<Contract>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT json_data FROM contracts
            WHERE project_path=$proj
              AND ($st IS NULL OR status=$st)
              AND ($q='' OR lower(visible_number) LIKE '%'||lower($q)||'%' OR lower(json_data) LIKE '%'||lower($q)||'%')
            ORDER BY visible_number LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        cmd.Parameters.AddWithValue("$st", status.HasValue ? (object)(int)status.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$q", text ?? string.Empty);
        cmd.Parameters.AddWithValue("$take", take <= 0 ? 50 : take);
        cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
        var list = new List<Contract>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var contract = JsonSerializer.Deserialize<Contract>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (contract is not null) list.Add(contract);
        }
        return list;
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM contracts WHERE project_path=$proj;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        var count = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(count ?? 0) + 1;
    }
}

public sealed class SqliteContractVersionRepository : IContractVersionRepository
{
    private readonly string _db;
    public SqliteContractVersionRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractVersion version, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_versions (id, contract_id, version_number, is_approved, hash, json_data)
            VALUES ($id, $cid, $vnum, $app, $hash, $json)
            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, version_number=excluded.version_number, is_approved=excluded.is_approved, hash=excluded.hash, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", version.Id.Value);
        cmd.Parameters.AddWithValue("$cid", version.ContractId.Value);
        cmd.Parameters.AddWithValue("$vnum", version.VersionNumber);
        cmd.Parameters.AddWithValue("$app", version.IsApproved ? 1 : 0);
        cmd.Parameters.AddWithValue("$hash", version.Hash);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(version, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractVersion?> GetByIdAsync(ContractVersionId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_versions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractVersion>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractVersion>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_versions WHERE contract_id=$cid ORDER BY version_number ASC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractVersion>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var v = JsonSerializer.Deserialize<ContractVersion>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (v is not null) list.Add(v);
        }
        return list;
    }

    public async Task<int> GetLatestVersionNumberAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT MAX(version_number) FROM contract_versions WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var val = await cmd.ExecuteScalarAsync(ct);
        return val == DBNull.Value || val == null ? 0 : Convert.ToInt32(val);
    }
}

public sealed class SqliteContractPartyRepository : IContractPartyRepository
{
    private readonly string _db;
    public SqliteContractPartyRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractParty party, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_parties (id, contract_id, role, json_data)
            VALUES ($id, $cid, $role, $json)
            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, role=excluded.role, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", party.Id);
        cmd.Parameters.AddWithValue("$cid", party.ContractId.Value);
        cmd.Parameters.AddWithValue("$role", (int)party.Role);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(party, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractParty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_parties WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractParty>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var p = JsonSerializer.Deserialize<ContractParty>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (p is not null) list.Add(p);
        }
        return list;
    }

    public async Task DeleteAsync(string partyId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_parties WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", partyId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractMilestoneRepository : IContractMilestoneRepository
{
    private readonly string _db;
    public SqliteContractMilestoneRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractMilestone milestone, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_milestones (id, contract_id, code, json_data)
            VALUES ($id, $cid, $code, $json)
            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, code=excluded.code, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", milestone.Id);
        cmd.Parameters.AddWithValue("$cid", milestone.ContractId.Value);
        cmd.Parameters.AddWithValue("$code", milestone.Code);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(milestone, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractMilestone>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_milestones WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractMilestone>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var m = JsonSerializer.Deserialize<ContractMilestone>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (m is not null) list.Add(m);
        }
        return list;
    }

    public async Task DeleteAsync(string milestoneId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_milestones WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", milestoneId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractScopeLinkRepository : IContractScopeLinkRepository
{
    private readonly string _db;
    public SqliteContractScopeLinkRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractScopeLink link, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_scope_links (id, contract_id, scope_type, ref_id, json_data)
            VALUES ($id, $cid, $type, $ref, $json)
            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, scope_type=excluded.scope_type, ref_id=excluded.ref_id, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", link.Id);
        cmd.Parameters.AddWithValue("$cid", link.ContractId.Value);
        cmd.Parameters.AddWithValue("$type", link.ScopeType);
        cmd.Parameters.AddWithValue("$ref", link.ReferenceId);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(link, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractScopeLink>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_scope_links WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractScopeLink>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var l = JsonSerializer.Deserialize<ContractScopeLink>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (l is not null) list.Add(l);
        }
        return list;
    }

    public async Task DeleteAsync(string linkId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_scope_links WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", linkId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractDocumentRepository : IContractDocumentRepository
{
    private readonly string _db;
    public SqliteContractDocumentRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractDocument document, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_documents (id, contract_id, doc_type, json_data)
            VALUES ($id, $cid, $type, $json)
            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, doc_type=excluded.doc_type, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", document.Id);
        cmd.Parameters.AddWithValue("$cid", document.ContractId.Value);
        cmd.Parameters.AddWithValue("$type", document.DocumentType);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(document, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractDocument>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_documents WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractDocument>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var d = JsonSerializer.Deserialize<ContractDocument>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (d is not null) list.Add(d);
        }
        return list;
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_documents WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", documentId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractAuditRepository : IContractAuditRepository
{
    private readonly string _db;
    public SqliteContractAuditRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task RecordAsync(ContractAuditEvent evt, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_audit (id, contract_id, action, actor, utc_ts, json_data)
            VALUES ($id, $cid, $act, $actor, $ts, $json);";
        cmd.Parameters.AddWithValue("$id", evt.Id);
        cmd.Parameters.AddWithValue("$cid", evt.ContractId.Value);
        cmd.Parameters.AddWithValue("$act", evt.Action);
        cmd.Parameters.AddWithValue("$actor", evt.Actor);
        cmd.Parameters.AddWithValue("$ts", evt.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evt, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractAuditEvent>> GetByContractAsync(ContractId contractId, int limit = 100, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_audit WHERE contract_id=$cid ORDER BY utc_ts DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        var list = new List<ContractAuditEvent>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var evt = JsonSerializer.Deserialize<ContractAuditEvent>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (evt is not null) list.Add(evt);
        }
        return list;
    }
}
