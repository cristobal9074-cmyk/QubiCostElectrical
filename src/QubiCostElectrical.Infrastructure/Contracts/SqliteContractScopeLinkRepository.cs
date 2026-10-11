using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractScopeLinkRepository : IContractScopeLinkRepository
{
    private readonly string _db;

    public SqliteContractScopeLinkRepository(string databasePath)
    {
        _db = databasePath ?? throw new ArgumentNullException("databasePath");
    }

    public async Task SaveAsync(ContractScopeLink link, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "\r\n            INSERT INTO contract_scope_links (id, contract_id, scope_type, ref_id, json_data)\r\n            VALUES ($id, $cid, $type, $ref, $json)\r\n            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, scope_type=excluded.scope_type, ref_id=excluded.ref_id, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", link.Id);
        cmd.Parameters.AddWithValue("$cid", link.ContractId.Value);
        cmd.Parameters.AddWithValue("$type", link.ScopeType);
        cmd.Parameters.AddWithValue("$ref", link.ReferenceId);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(link, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractScopeLink>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_scope_links WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        List<ContractScopeLink> list = new List<ContractScopeLink>();
        using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            ContractScopeLink? contractScopeLink = JsonSerializer.Deserialize<ContractScopeLink>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (contractScopeLink != null)
            {
                list.Add(contractScopeLink);
            }
        }
        return list;
    }

    public async Task DeleteAsync(string linkId, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_scope_links WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", linkId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
