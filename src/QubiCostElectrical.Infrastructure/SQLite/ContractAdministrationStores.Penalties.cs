using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractPenaltyRepository : IContractPenaltyRepository
{
    private readonly string _db;
    public SqliteContractPenaltyRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractPenalty penalty, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.Penalties} (
                id, contract_id, project_path, visible_number, penalty_type,
                cause, date, amount, percentage, currency_code, status,
                observations, responsible, comment, row_version,
                created_by, created_utc, modified_by, modified_utc, json_data)
            VALUES (
                $id, $cid, $pp, $vis, $pt,
                $cause, $date, $amt, $pct, $cur, $st,
                $obs, $resp, $com, $rv,
                $cb, $cu, $mb, $mu, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, project_path=excluded.project_path,
                visible_number=excluded.visible_number, penalty_type=excluded.penalty_type,
                cause=excluded.cause, date=excluded.date,
                amount=excluded.amount, percentage=excluded.percentage,
                currency_code=excluded.currency_code, status=excluded.status,
                observations=excluded.observations, responsible=excluded.responsible,
                comment=excluded.comment, row_version=excluded.row_version,
                modified_by=excluded.modified_by, modified_utc=excluded.modified_utc,
                json_data=excluded.json_data;";
        BindParams(cmd, penalty);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractPenalty?> GetByIdAsync(PenaltyId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Penalties} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractPenalty>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractPenalty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Penalties} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractAdministrationTable.Penalties} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", contractPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static void BindParams(SqliteCommand cmd, ContractPenalty p)
    {
        var json = JsonSerializer.Serialize(p, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", p.Id.Value);
        cmd.Parameters.AddWithValue("$cid", p.ContractId.Value);
        cmd.Parameters.AddWithValue("$pp", p.ProjectPath);
        cmd.Parameters.AddWithValue("$vis", p.VisibleNumber);
        cmd.Parameters.AddWithValue("$pt", (int)p.PenaltyType);
        cmd.Parameters.AddWithValue("$cause", p.Cause);
        cmd.Parameters.AddWithValue("$date", p.Date.ToString("O"));
        cmd.Parameters.AddWithValue("$amt", p.Amount);
        cmd.Parameters.AddWithValue("$pct", p.Percentage);
        cmd.Parameters.AddWithValue("$cur", p.CurrencyCode);
        cmd.Parameters.AddWithValue("$st", (int)p.Status);
        cmd.Parameters.AddWithValue("$obs", p.Observations);
        cmd.Parameters.AddWithValue("$resp", p.Responsible);
        cmd.Parameters.AddWithValue("$com", p.Comment);
        cmd.Parameters.AddWithValue("$rv", p.RowVersion);
        cmd.Parameters.AddWithValue("$cb", p.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", p.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", p.ModifiedBy);
        cmd.Parameters.AddWithValue("$mu", p.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ContractPenalty>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractPenalty>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractPenalty>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
