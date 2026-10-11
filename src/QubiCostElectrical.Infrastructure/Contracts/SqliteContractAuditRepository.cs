using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractAuditRepository : IContractAuditRepository
{
    private readonly string _db;

    public SqliteContractAuditRepository(string databasePath)
    {
        _db = databasePath ?? throw new ArgumentNullException("databasePath");
    }

    public async Task RecordAsync(ContractAuditEvent evt, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "\r\n            INSERT INTO contract_audit (id, contract_id, action, actor, utc_ts, json_data)\r\n            VALUES ($id, $cid, $act, $actor, $ts, $json);";
        cmd.Parameters.AddWithValue("$id", evt.Id);
        cmd.Parameters.AddWithValue("$cid", evt.ContractId.Value);
        cmd.Parameters.AddWithValue("$act", evt.Action);
        cmd.Parameters.AddWithValue("$actor", evt.Actor);
        cmd.Parameters.AddWithValue("$ts", evt.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evt, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractAuditEvent>> GetByContractAsync(ContractId contractId, int limit = 100, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_audit WHERE contract_id=$cid ORDER BY utc_ts DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        List<ContractAuditEvent> list = new List<ContractAuditEvent>();
        using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            ContractAuditEvent? contractAuditEvent = JsonSerializer.Deserialize<ContractAuditEvent>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (contractAuditEvent != null)
            {
                list.Add(contractAuditEvent);
            }
        }
        return list;
    }
}
