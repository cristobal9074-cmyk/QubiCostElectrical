using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteExtraWorkRepository : IExtraWorkRepository
{
    private readonly string _db;
    public SqliteExtraWorkRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ExtraWork extraWork, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var json = JsonSerializer.Serialize(extraWork, ProfessionalDbHelpers.JsonOptions);
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractChangeTable.ExtraWorks} (
                id, contract_id, change_request_id, change_order_id, visible_number,
                instruction, description, origin, instruction_date, wbs_code, activity_code,
                resources, materials, labor, equipment, quantities, hours,
                estimated_cost, final_cost, currency_code, evidence, responsible,
                approved_by, approval_date, valuation_amount, status, row_version,
                created_utc, created_by, modified_utc, modified_by, json_data)
            VALUES (
                $id, $cid, $crid, $coid, $vis,
                $ins, $desc, $org, $idate, $wbs, $ac,
                $res, $mat, $lab, $eqp, $qty, $hrs,
                $ec, $fc, $cur, $evi, $resp,
                $appr, $ad, $va, $st, $rv,
                $cu, $cb, $mu, $mb, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, change_request_id=excluded.change_request_id,
                change_order_id=excluded.change_order_id, visible_number=excluded.visible_number,
                instruction=excluded.instruction, description=excluded.description,
                origin=excluded.origin, instruction_date=excluded.instruction_date,
                wbs_code=excluded.wbs_code, activity_code=excluded.activity_code,
                resources=excluded.resources, materials=excluded.materials,
                labor=excluded.labor, equipment=excluded.equipment,
                quantities=excluded.quantities, hours=excluded.hours,
                estimated_cost=excluded.estimated_cost, final_cost=excluded.final_cost,
                currency_code=excluded.currency_code, evidence=excluded.evidence,
                responsible=excluded.responsible, approved_by=excluded.approved_by,
                approval_date=excluded.approval_date, valuation_amount=excluded.valuation_amount,
                status=excluded.status, row_version=excluded.row_version,
                modified_utc=excluded.modified_utc, modified_by=excluded.modified_by,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", extraWork.Id.Value);
        cmd.Parameters.AddWithValue("$cid", extraWork.ContractId.Value);
        cmd.Parameters.AddWithValue("$crid", extraWork.ChangeRequestId?.Value ?? "");
        cmd.Parameters.AddWithValue("$coid", extraWork.ChangeOrderId?.Value ?? "");
        cmd.Parameters.AddWithValue("$vis", extraWork.VisibleNumber);
        cmd.Parameters.AddWithValue("$ins", extraWork.Instruction);
        cmd.Parameters.AddWithValue("$desc", extraWork.Description);
        cmd.Parameters.AddWithValue("$org", (int)extraWork.Origin);
        cmd.Parameters.AddWithValue("$idate", extraWork.InstructionDate.ToString("O"));
        cmd.Parameters.AddWithValue("$wbs", extraWork.WbsCode);
        cmd.Parameters.AddWithValue("$ac", extraWork.ActivityCode);
        cmd.Parameters.AddWithValue("$res", extraWork.Resources);
        cmd.Parameters.AddWithValue("$mat", extraWork.Materials);
        cmd.Parameters.AddWithValue("$lab", extraWork.Labor);
        cmd.Parameters.AddWithValue("$eqp", extraWork.Equipment);
        cmd.Parameters.AddWithValue("$qty", extraWork.Quantities);
        cmd.Parameters.AddWithValue("$hrs", extraWork.Hours);
        cmd.Parameters.AddWithValue("$ec", extraWork.EstimatedCost);
        cmd.Parameters.AddWithValue("$fc", extraWork.FinalCost);
        cmd.Parameters.AddWithValue("$cur", extraWork.CurrencyCode);
        cmd.Parameters.AddWithValue("$evi", extraWork.Evidence);
        cmd.Parameters.AddWithValue("$resp", extraWork.Responsible);
        cmd.Parameters.AddWithValue("$appr", extraWork.ApprovedBy);
        cmd.Parameters.AddWithValue("$ad", extraWork.ApprovalDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$va", extraWork.ValuationAmount);
        cmd.Parameters.AddWithValue("$st", (int)extraWork.Status);
        cmd.Parameters.AddWithValue("$rv", extraWork.RowVersion);
        cmd.Parameters.AddWithValue("$cu", extraWork.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", extraWork.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", extraWork.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", extraWork.ModifiedBy);
        cmd.Parameters.AddWithValue("$json", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ExtraWork?> GetByIdAsync(ExtraWorkId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ExtraWorks} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ExtraWork>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ExtraWork>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ExtraWorks} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadExtraWorkListAsync(cmd, ct);
    }

    public async Task<List<ExtraWork>> SearchAsync(string projectPath, string? text, ExtraWorkStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var sql = $"SELECT json_data FROM {SqliteContractChangeTable.ExtraWorks} WHERE project_path=$pp";
        if (!string.IsNullOrEmpty(text)) sql += " AND (instruction LIKE $t OR description LIKE $t OR visible_number LIKE $t)";
        if (status.HasValue) sql += " AND status=$st";
        sql += " ORDER BY created_utc DESC LIMIT $take OFFSET $skip;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$pp", projectPath);
        if (!string.IsNullOrEmpty(text)) cmd.Parameters.AddWithValue("$t", $"%{text}%");
        if (status.HasValue) cmd.Parameters.AddWithValue("$st", (int)status.Value);
        cmd.Parameters.AddWithValue("$take", take);
        cmd.Parameters.AddWithValue("$skip", skip);
        return await ReadExtraWorkListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractChangeTable.ExtraWorks} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", projectPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static async Task<List<ExtraWork>> ReadExtraWorkListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ExtraWork>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ExtraWork>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
