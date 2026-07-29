using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractInvoiceRepository : IContractInvoiceRepository
{
    private readonly string _db;
    public SqliteContractInvoiceRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractInvoice invoice, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractAdministrationTable.Invoices} (
                id, contract_id, project_path, visible_number, invoice_number,
                date, supplier, client, currency_code,
                subtotal, tax_amount, total, status,
                payment_certificate_reference, contract_reference,
                comment, row_version,
                created_by, created_utc, modified_by, modified_utc, json_data)
            VALUES (
                $id, $cid, $pp, $vis, $invnum,
                $date, $supp, $cli, $cur,
                $sub, $tax, $tot, $st,
                $pcr, $cr,
                $com, $rv,
                $cb, $cu, $mb, $mu, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, project_path=excluded.project_path,
                visible_number=excluded.visible_number, invoice_number=excluded.invoice_number,
                date=excluded.date, supplier=excluded.supplier,
                client=excluded.client, currency_code=excluded.currency_code,
                subtotal=excluded.subtotal, tax_amount=excluded.tax_amount,
                total=excluded.total, status=excluded.status,
                payment_certificate_reference=excluded.payment_certificate_reference,
                contract_reference=excluded.contract_reference,
                comment=excluded.comment, row_version=excluded.row_version,
                modified_by=excluded.modified_by, modified_utc=excluded.modified_utc,
                json_data=excluded.json_data;";
        BindParams(cmd, invoice);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractInvoice?> GetByIdAsync(InvoiceId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Invoices} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractInvoice>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractInvoice>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractAdministrationTable.Invoices} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractAdministrationTable.Invoices} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", contractPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static void BindParams(SqliteCommand cmd, ContractInvoice inv)
    {
        var json = JsonSerializer.Serialize(inv, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", inv.Id.Value);
        cmd.Parameters.AddWithValue("$cid", inv.ContractId.Value);
        cmd.Parameters.AddWithValue("$pp", inv.ProjectPath);
        cmd.Parameters.AddWithValue("$vis", inv.VisibleNumber);
        cmd.Parameters.AddWithValue("$invnum", inv.InvoiceNumber);
        cmd.Parameters.AddWithValue("$date", inv.Date.ToString("O"));
        cmd.Parameters.AddWithValue("$supp", inv.Supplier);
        cmd.Parameters.AddWithValue("$cli", inv.Client);
        cmd.Parameters.AddWithValue("$cur", inv.CurrencyCode);
        cmd.Parameters.AddWithValue("$sub", inv.Subtotal);
        cmd.Parameters.AddWithValue("$tax", inv.TaxAmount);
        cmd.Parameters.AddWithValue("$tot", inv.Total);
        cmd.Parameters.AddWithValue("$st", (int)inv.Status);
        cmd.Parameters.AddWithValue("$pcr", inv.PaymentCertificateReference);
        cmd.Parameters.AddWithValue("$cr", inv.ContractReference);
        cmd.Parameters.AddWithValue("$com", inv.Comment);
        cmd.Parameters.AddWithValue("$rv", inv.RowVersion);
        cmd.Parameters.AddWithValue("$cb", inv.CreatedBy);
        cmd.Parameters.AddWithValue("$cu", inv.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", inv.ModifiedBy);
        cmd.Parameters.AddWithValue("$mu", inv.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ContractInvoice>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractInvoice>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractInvoice>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
