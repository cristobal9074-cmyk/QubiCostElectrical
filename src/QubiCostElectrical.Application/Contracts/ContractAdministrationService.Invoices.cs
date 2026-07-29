using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ContractInvoice operations
// ──────────────────────────────────────────────────────────────
partial class ContractAdministrationService
{
    public async Task<ContractInvoice> CreateInvoiceAsync(ContractInvoice draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateInvoice(_userPermissions.CurrentRole), "CreateInvoice");

        var id = InvoiceId.New();
        var nextNum = await _invoiceRepo.GetNextNumberAsync(draft.ProjectPath, ct);

        // Ensure line IDs via initializer
        var lines = draft.Lines.Select(l => new InvoiceLine
        {
            Id = l.Id ?? InvoiceLineId.New(),
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            Reference = l.Reference
        }).ToList();

        var invoice = new ContractInvoice
        {
            Id = id,
            ContractId = draft.ContractId,
            VisibleNumber = nextNum,
            InvoiceNumber = draft.InvoiceNumber,
            Date = draft.Date == default ? DateTime.UtcNow : draft.Date,
            Supplier = draft.Supplier,
            Client = draft.Client,
            CurrencyCode = draft.CurrencyCode,
            Subtotal = draft.Subtotal,
            TaxAmount = draft.TaxAmount,
            Total = draft.Total,
            Status = InvoiceStatus.Draft,
            PaymentCertificateReference = draft.PaymentCertificateReference,
            ContractReference = draft.ContractReference,
            Lines = lines,
            ProjectPath = draft.ProjectPath,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

        await _invoiceRepo.SaveAsync(invoice, ct);
        await RecordAuditAsync(invoice.ContractId, "ContractInvoice", id.Value, "CREAR", actor, "Creación de factura informativa", ct: ct);

        return invoice;
    }

    public async Task<ContractInvoice> UpdateInvoiceDraftAsync(ContractInvoice invoice, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditInvoice(_userPermissions.CurrentRole), "EditInvoice");

        var existing = await LoadInvoiceWritable(invoice.Id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is InvoiceStatus.Issued or InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new InvalidOperationException("No se puede editar una factura emitida, pagada o cancelada.");

        existing.InvoiceNumber = invoice.InvoiceNumber;
        existing.Date = invoice.Date;
        existing.Supplier = invoice.Supplier;
        existing.Client = invoice.Client;
        existing.CurrencyCode = invoice.CurrencyCode;
        existing.Subtotal = invoice.Subtotal;
        existing.TaxAmount = invoice.TaxAmount;
        existing.Total = invoice.Total;
        existing.PaymentCertificateReference = invoice.PaymentCertificateReference;
        existing.ContractReference = invoice.ContractReference;
        existing.Comment = invoice.Comment;

        // Replace lines (new InvoiceLine instances with proper IDs)
        var replacedLines = invoice.Lines.Select(l => new InvoiceLine
        {
            Id = l.Id ?? InvoiceLineId.New(),
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            Reference = l.Reference
        }).ToList();
        existing.Lines.Clear();
        existing.Lines.AddRange(replacedLines);

        StampRowVersion(existing, actor);

        await _invoiceRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractInvoice", existing.Id.Value, "EDITAR", actor, "Edición de factura informativa", ct: ct);

        return existing;
    }

    public async Task<ContractInvoice> IssueInvoiceAsync(InvoiceId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditInvoice(_userPermissions.CurrentRole), "IssueInvoice");

        var existing = await LoadInvoiceWritable(id, expectedRowVersion, ct);
        InvoiceStatusTransitions.EnsureCanTransition(existing.Status, InvoiceStatus.Issued);

        existing.Status = InvoiceStatus.Issued;
        StampRowVersion(existing, actor);

        await _invoiceRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractInvoice", id.Value, "EMITIR", actor, "Factura emitida", ct: ct);

        return existing;
    }

    public async Task<ContractInvoice> MarkInvoicePaidAsync(InvoiceId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditInvoice(_userPermissions.CurrentRole), "MarkInvoicePaid");

        var existing = await LoadInvoiceWritable(id, expectedRowVersion, ct);

        var from = existing.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid ? existing.Status : InvoiceStatus.Issued;
        InvoiceStatusTransitions.EnsureCanTransition(from, InvoiceStatus.Paid);

        existing.Status = InvoiceStatus.Paid;
        StampRowVersion(existing, actor);

        await _invoiceRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractInvoice", id.Value, "PAGAR", actor, "Factura pagada", ct: ct);

        return existing;
    }

    public async Task<ContractInvoice> CancelInvoiceAsync(InvoiceId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditInvoice(_userPermissions.CurrentRole), "CancelInvoice");

        var existing = await LoadInvoiceWritable(id, expectedRowVersion, ct);
        InvoiceStatusTransitions.EnsureCanTransition(existing.Status, InvoiceStatus.Cancelled);

        existing.Status = InvoiceStatus.Cancelled;
        existing.Comment = reason;
        StampRowVersion(existing, actor);

        await _invoiceRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractInvoice", id.Value, "CANCELAR", actor, $"Factura cancelada: {reason}", ct: ct);

        return existing;
    }

    public Task<ContractInvoice?> GetInvoiceAsync(InvoiceId id, CancellationToken ct = default)
        => _invoiceRepo.GetByIdAsync(id, ct);

    public Task<List<ContractInvoice>> GetInvoicesByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _invoiceRepo.GetByContractAsync(contractId, ct);
}
