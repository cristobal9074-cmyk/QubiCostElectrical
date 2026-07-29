using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractChangeAuditRepository : IContractChangeAuditRepository
{
    private readonly string _db;
    public SqliteContractChangeAuditRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractChangeAuditEvent auditEvent, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {SqliteContractChangeTable.ChangeAudit}
            (id, contract_id, entity_type, entity_id, action, actor, timestamp_utc, details_json, is_denied_attempt)
            VALUES ($id, $cid, $et, $eid, $act, $actor, $ts, $dj, $ida);";
        cmd.Parameters.AddWithValue("$id", auditEvent.Id.Value);
        cmd.Parameters.AddWithValue("$cid", auditEvent.ContractId.Value);
        cmd.Parameters.AddWithValue("$et", auditEvent.EntityType);
        cmd.Parameters.AddWithValue("$eid", auditEvent.EntityId);
        cmd.Parameters.AddWithValue("$act", auditEvent.Action);
        cmd.Parameters.AddWithValue("$actor", auditEvent.Actor);
        cmd.Parameters.AddWithValue("$ts", auditEvent.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$dj", auditEvent.DetailsJson);
        cmd.Parameters.AddWithValue("$ida", auditEvent.IsDeniedAttempt ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ContractChangeAuditEvent>> GetByContractAsync(ContractId contractId, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ChangeAudit} WHERE contract_id=$cid ORDER BY timestamp_utc DESC LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        cmd.Parameters.AddWithValue("$take", take);
        cmd.Parameters.AddWithValue("$skip", skip);
        return await ReadAuditListAsync(cmd, ct);
    }

    public async Task<List<ContractChangeAuditEvent>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ChangeAudit} WHERE entity_type=$et AND entity_id=$eid ORDER BY timestamp_utc DESC;";
        cmd.Parameters.AddWithValue("$et", entityType);
        cmd.Parameters.AddWithValue("$eid", entityId);
        return await ReadAuditListAsync(cmd, ct);
    }

    private static async Task<List<ContractChangeAuditEvent>> ReadAuditListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractChangeAuditEvent>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ContractChangeAuditEvent
            {
                Id = ContractChangeAuditEventId.From(reader.GetString(0)),
                ContractId = ContractId.From(reader.GetString(1)),
                EntityType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                EntityId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Action = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Actor = reader.IsDBNull(5) ? "" : reader.GetString(5),
                TimestampUtc = DateTime.Parse(reader.GetString(6)),
                DetailsJson = reader.IsDBNull(7) ? "" : reader.GetString(7),
                IsDeniedAttempt = reader.GetInt32(8) == 1
            });
        }
        return list;
    }
}
