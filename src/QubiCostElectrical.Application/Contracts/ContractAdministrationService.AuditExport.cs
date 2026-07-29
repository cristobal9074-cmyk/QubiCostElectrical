using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// Audit & Export operations
// ──────────────────────────────────────────────────────────────
partial class ContractAdministrationService
{
    // ── Audit ────────────────────────────────────────────

    public Task<List<ContractAdministrationAuditEvent>> GetAdministrationAuditLogAsync(ContractId contractId, int skip, int take, CancellationToken ct = default)
        => _auditRepo.GetByContractAsync(contractId, skip, take, ct);

    public Task<List<ContractAdministrationAuditEvent>> GetEntityAuditLogAsync(string entityType, string entityId, CancellationToken ct = default)
        => _auditRepo.GetByEntityAsync(entityType, entityId, ct);

    // ── Export ────────────────────────────────────────────

    public async Task<string> ExportAdjustmentsCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractAdministration(_userPermissions.CurrentRole), "Export");
        var items = await _adjustmentRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Period,Type,BaseAmount,AdjustedAmount,Difference,Status,BaseDate,CalculationDate,CreatedBy,CreatedUtc");
        foreach (var a in items)
        {
            csv.AppendLine($"\"{a.Id.Value}\",{a.VisibleNumber},{a.Period},\"{a.AdjustmentType}\",{a.BaseAmount},{a.AdjustedAmount},{a.Difference},\"{a.Status}\",{a.BaseDate:O},{a.CalculationDate:O},\"{a.CreatedBy}\",{a.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportGuaranteesCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractAdministration(_userPermissions.CurrentRole), "Export");
        var items = await _guaranteeRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Type,Number,Issuer,Institution,Amount,Percentage,Status,StartDate,ExpiryDate,CreatedBy,CreatedUtc");
        foreach (var g in items)
        {
            csv.AppendLine($"\"{g.Id.Value}\",{g.VisibleNumber},\"{g.GuaranteeType}\",\"{g.Number}\",\"{g.Issuer}\",\"{g.Institution}\",{g.Amount},{g.Percentage},\"{g.Status}\",{g.StartDate:O},{g.ExpiryDate:O},\"{g.CreatedBy}\",{g.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportPenaltiesCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractAdministration(_userPermissions.CurrentRole), "Export");
        var items = await _penaltyRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Type,Cause,Date,Amount,Percentage,Status,Responsible,CreatedBy,CreatedUtc");
        foreach (var p in items)
        {
            csv.AppendLine($"\"{p.Id.Value}\",{p.VisibleNumber},\"{p.PenaltyType}\",\"{p.Cause}\",{p.Date:O},{p.Amount},{p.Percentage},\"{p.Status}\",\"{p.Responsible}\",\"{p.CreatedBy}\",{p.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportInvoicesCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractAdministration(_userPermissions.CurrentRole), "Export");
        var items = await _invoiceRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,InvoiceNumber,Date,Supplier,Client,Subtotal,TaxAmount,Total,Status,CreatedBy,CreatedUtc");
        foreach (var i in items)
        {
            csv.AppendLine($"\"{i.Id.Value}\",{i.VisibleNumber},\"{i.InvoiceNumber}\",{i.Date:O},\"{i.Supplier}\",\"{i.Client}\",{i.Subtotal},{i.TaxAmount},{i.Total},\"{i.Status}\",\"{i.CreatedBy}\",{i.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportAdministrationAuditCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractAdministration(_userPermissions.CurrentRole), "Export");
        var items = await _auditRepo.GetByContractAsync(contractId, 0, 10000, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,EntityType,EntityId,Action,Actor,TimestampUtc,IsDeniedAttempt");
        foreach (var e in items)
        {
            csv.AppendLine($"\"{e.Id.Value}\",\"{e.EntityType}\",\"{e.EntityId}\",\"{e.Action}\",\"{e.Actor}\",{e.TimestampUtc:O},{e.IsDeniedAttempt}");
        }
        return csv.ToString();
    }
}
