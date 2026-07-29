using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractGuaranteeRepository : IContractGuaranteeRepository
{
    private readonly string _db;
    public SqliteContractGuaranteeRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractGuarantee guarantee, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.Guarantees} (
                id, contract_id, project_path, visible_number, guarantee_type,
                number, issuer, institution, beneficiary, amount, percentage,
                currency_code, start_date, expiry_date, status,
                supporting_documents, responsible, comment, row_version,
                created_by, created_utc, modified_by, modified_utc, json_data)
            VALUES (
                $id, $cid, $pp, $vis, $gt,
                $num, $iss, $inst, $ben, $amt, $pct,
                $cur, $sd, $ed, $st,
                $sdocs, $resp, $com, $rv,
                $cb, $cu, $mb, $mu, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, project_path=excluded.project_path,
                visible_number=excluded.visible_number, guarantee_type=excluded.guarantee_type,
                number=excluded.number, issuer=excluded.issuer,
                institution=excluded.institution, beneficiary=excluded.beneficiary,
                amount=excluded.amount, percentage=excluded.percentage,
                currency_code=excluded.currency_code, start_date=excluded.start_date,
                expiry_date=excluded.expiry_date, status=excluded.status,
                supporting_documents=excluded.supporting_documents,
                responsible=excluded.responsible, comment=excluded.comment,
                row_version=excluded.row_version,
                modified_by=excluded.modified_by, modified_utc=excluded.modified_utc,
                json_data=excluded.json_data;";
        BindParams(cmd, guarantee);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractGuarantee?> GetByIdAsync(GuaranteeId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Guarantees} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractGuarantee>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractGuarantee>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Guarantees} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<List<ContractGuarantee>> GetExpiringAsync(int withinDays, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            SELECT json_data FROM {SqliteContractAdministrationTable.Guarantees}
            WHERE status={(int)GuaranteeStatus.Active}
              AND expiry_date <= date('now', '+' || $days || ' days')
            ORDER BY expiry_date;";
        cmd.Parameters.AddWithValue("$days", withinDays);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractAdministrationTable.Guarantees} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", contractPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static void BindParams(SqliteCommand cmd, ContractGuarantee g)
    {
        var json = JsonSerializer.Serialize(g, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", g.Id.Value);
        cmd.Parameters.AddWithValue("$cid", g.ContractId.Value);
        cmd.Parameters.AddWithValue("$pp", g.ProjectPath);
        cmd.Parameters.AddWithValue("$vis", g.VisibleNumber);
        cmd.Parameters.AddWithValue("$gt", (int)g.GuaranteeType);
        cmd.Parameters.AddWithValue("$num", g.Number);
        cmd.Parameters.AddWithValue("$iss", g.Issuer);
        cmd.Parameters.AddWithValue("$inst", g.Institution);
        cmd.Parameters.AddWithValue("$ben", g.Beneficiary);
        cmd.Parameters.AddWithValue("$amt", g.Amount);
        cmd.Parameters.AddWithValue("$pct", g.Percentage);
        cmd.Parameters.AddWithValue("$cur", g.CurrencyCode);
        cmd.Parameters.AddWithValue("$sd", g.StartDate.ToString("O"));
        cmd.Parameters.AddWithValue("$ed", g.ExpiryDate.ToString("O"));
        cmd.Parameters.AddWithValue("$st", (int)g.Status);
        cmd.Parameters.AddWithValue("$sdocs", g.SupportingDocuments);
        cmd.Parameters.AddWithValue("$resp", g.Responsible);
        cmd.Parameters.AddWithValue("$com", g.Comment);
        cmd.Parameters.AddWithValue("$rv", g.RowVersion);
        cmd.Parameters.AddWithValue("$cb", g.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", g.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", g.ModifiedBy);
        cmd.Parameters.AddWithValue("$mu", g.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ContractGuarantee>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractGuarantee>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractGuarantee>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
