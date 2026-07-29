using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteContractAdministrationTable
{
    internal const string Adjustments = "contract_adjustments";
    internal const string AdjustmentFormulas = "adjustment_formulas";
    internal const string AdjustmentIndexes = "adjustment_indexes";
    internal const string Guarantees = "contract_guarantees";
    internal const string Penalties = "contract_penalties";
    internal const string Invoices = "contract_invoices";
    internal const string Audit = "contract_administration_audit";
}

public sealed class SqliteContractAdjustmentRepository : IContractAdjustmentRepository
{
    private readonly string _db;
    public SqliteContractAdjustmentRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractAdjustment adjustment, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.Adjustments} (
                id, contract_id, project_path, visible_number, period,
                base_date, calculation_date, adjustment_type, currency_code,
                base_amount, adjusted_amount, status, description,
                responsible, comment, hash, row_version,
                created_by, created_utc, modified_by, modified_utc, json_data)
            VALUES (
                $id, $cid, $pp, $vis, $per,
                $bd, $cd, $at, $cur,
                $ba, $aa, $st, $desc,
                $resp, $com, $hash, $rv,
                $cb, $cu, $mb, $mu, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, project_path=excluded.project_path,
                visible_number=excluded.visible_number, period=excluded.period,
                base_date=excluded.base_date, calculation_date=excluded.calculation_date,
                adjustment_type=excluded.adjustment_type, currency_code=excluded.currency_code,
                base_amount=excluded.base_amount, adjusted_amount=excluded.adjusted_amount,
                status=excluded.status, description=excluded.description,
                responsible=excluded.responsible, comment=excluded.comment,
                hash=excluded.hash, row_version=excluded.row_version,
                modified_by=excluded.modified_by, modified_utc=excluded.modified_utc,
                json_data=excluded.json_data;";
        BindParams(cmd, adjustment);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractAdjustment?> GetByIdAsync(AdjustmentId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Adjustments} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractAdjustment>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractAdjustment>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Adjustments} WHERE contract_id=$cid ORDER BY period DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractAdministrationTable.Adjustments} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", contractPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static void BindParams(SqliteCommand cmd, ContractAdjustment a)
    {
        var json = JsonSerializer.Serialize(a, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", a.Id.Value);
        cmd.Parameters.AddWithValue("$cid", a.ContractId.Value);
        cmd.Parameters.AddWithValue("$pp", a.ProjectPath);
        cmd.Parameters.AddWithValue("$vis", a.VisibleNumber);
        cmd.Parameters.AddWithValue("$per", a.Period);
        cmd.Parameters.AddWithValue("$bd", a.BaseDate.ToString("O"));
        cmd.Parameters.AddWithValue("$cd", a.CalculationDate.ToString("O"));
        cmd.Parameters.AddWithValue("$at", (int)a.AdjustmentType);
        cmd.Parameters.AddWithValue("$cur", a.CurrencyCode);
        cmd.Parameters.AddWithValue("$ba", a.BaseAmount);
        cmd.Parameters.AddWithValue("$aa", a.AdjustedAmount);
        cmd.Parameters.AddWithValue("$st", (int)a.Status);
        cmd.Parameters.AddWithValue("$desc", a.Description);
        cmd.Parameters.AddWithValue("$resp", a.Responsible);
        cmd.Parameters.AddWithValue("$com", a.Comment);
        cmd.Parameters.AddWithValue("$hash", a.Hash);
        cmd.Parameters.AddWithValue("$rv", a.RowVersion);
        cmd.Parameters.AddWithValue("$cb", a.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", a.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", a.ModifiedBy);
        cmd.Parameters.AddWithValue("$mu", a.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ContractAdjustment>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractAdjustment>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractAdjustment>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteAdjustmentFormulaRepository : IAdjustmentFormulaRepository
{
    private readonly string _db;
    public SqliteAdjustmentFormulaRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(AdjustmentFormula formula, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.AdjustmentFormulas} (
                id, contract_id, name, description, created_by, created_utc, json_data)
            VALUES ($id, $cid, $name, $desc, $cb, $cu, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, name=excluded.name,
                description=excluded.description, json_data=excluded.json_data;";
        BindParams(cmd, formula);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AdjustmentFormula?> GetByIdAsync(AdjustmentFormulaId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.AdjustmentFormulas} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<AdjustmentFormula>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<AdjustmentFormula>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.AdjustmentFormulas} WHERE contract_id=$cid ORDER BY name;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    private static void BindParams(SqliteCommand cmd, AdjustmentFormula f)
    {
        var json = JsonSerializer.Serialize(f, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", f.Id.Value);
        cmd.Parameters.AddWithValue("$cid", f.ContractId.Value);
        cmd.Parameters.AddWithValue("$name", f.Name);
        cmd.Parameters.AddWithValue("$desc", f.Description);
        cmd.Parameters.AddWithValue("$cb", f.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", f.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<AdjustmentFormula>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<AdjustmentFormula>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<AdjustmentFormula>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteAdjustmentIndexRepository : IAdjustmentIndexRepository
{
    private readonly string _db;
    public SqliteAdjustmentIndexRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(AdjustmentIndex index, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.AdjustmentIndexes} (
                id, name, code, value, date, source, json_data)
            VALUES ($id, $name, $code, $val, $date, $src, $json)
            ON CONFLICT(id) DO UPDATE SET
                name=excluded.name, code=excluded.code, value=excluded.value,
                date=excluded.date, source=excluded.source,
                json_data=excluded.json_data;";
        BindParams(cmd, index);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AdjustmentIndex?> GetByIdAsync(AdjustmentIndexId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.AdjustmentIndexes} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<AdjustmentIndex>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<AdjustmentIndex>> SearchAsync(string? name, string? code, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var sql = $"SELECT json_data FROM {SqliteContractAdministrationTable.AdjustmentIndexes} WHERE 1=1";
        if (!string.IsNullOrEmpty(name)) sql += " AND name LIKE $name";
        if (!string.IsNullOrEmpty(code)) sql += " AND code LIKE $code";
        sql += " ORDER BY date DESC;";
        cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(name)) cmd.Parameters.AddWithValue("$name", $"%{name}%");
        if (!string.IsNullOrEmpty(code)) cmd.Parameters.AddWithValue("$code", $"%{code}%");
        return await ReadListAsync(cmd, ct);
    }

    private static void BindParams(SqliteCommand cmd, AdjustmentIndex idx)
    {
        var json = JsonSerializer.Serialize(idx, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", idx.Id.Value);
        cmd.Parameters.AddWithValue("$name", idx.Name);
        cmd.Parameters.AddWithValue("$code", idx.Code);
        cmd.Parameters.AddWithValue("$val", idx.Value);
        cmd.Parameters.AddWithValue("$date", idx.Date.ToString("O"));
        cmd.Parameters.AddWithValue("$src", idx.Source);
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<AdjustmentIndex>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<AdjustmentIndex>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<AdjustmentIndex>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
