using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractAdministrationService
{
    // ── Adjustments ────────────────────────────────────────────
    Task<ContractAdjustment> CreateAdjustmentAsync(ContractAdjustment draft, string actor, CancellationToken ct = default);
    Task<ContractAdjustment> UpdateAdjustmentDraftAsync(ContractAdjustment adjustment, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractAdjustment> CalculateAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractAdjustment> ApplyAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractAdjustment> RejectAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<ContractAdjustment> CloseAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractAdjustment?> GetAdjustmentAsync(AdjustmentId id, CancellationToken ct = default);
    Task<List<ContractAdjustment>> GetAdjustmentsByContractAsync(ContractId contractId, CancellationToken ct = default);

    // ── Adjustment Formulas ─────────────────────────────────────
    Task<AdjustmentFormula> SaveFormulaAsync(AdjustmentFormula formula, string actor, CancellationToken ct = default);
    Task<AdjustmentFormula?> GetFormulaAsync(AdjustmentFormulaId id, CancellationToken ct = default);
    Task<List<AdjustmentFormula>> GetFormulasByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<decimal> CalculatePolynomialFormulaAsync(AdjustmentFormula formula, CancellationToken ct = default);

    // ── Adjustment Indexes ──────────────────────────────────────
    Task<AdjustmentIndex> SaveIndexAsync(AdjustmentIndex index, string actor, CancellationToken ct = default);
    Task<List<AdjustmentIndex>> SearchIndexesAsync(string? name, string? code, CancellationToken ct = default);

    // ── Guarantees ──────────────────────────────────────────────
    Task<ContractGuarantee> CreateGuaranteeAsync(ContractGuarantee draft, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> UpdateGuaranteeDraftAsync(ContractGuarantee guarantee, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> ActivateGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> RenewGuaranteeAsync(GuaranteeId id, GuaranteeRenewal renewal, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> ReleaseGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> ExpireGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> ExecuteGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractGuarantee> CancelGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<ContractGuarantee?> GetGuaranteeAsync(GuaranteeId id, CancellationToken ct = default);
    Task<List<ContractGuarantee>> GetGuaranteesByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ContractGuarantee>> GetExpiringGuaranteesAsync(int withinDays, CancellationToken ct = default);

    // ── Penalties ───────────────────────────────────────────────
    Task<ContractPenalty> CreatePenaltyAsync(ContractPenalty draft, string actor, CancellationToken ct = default);
    Task<ContractPenalty> UpdatePenaltyDraftAsync(ContractPenalty penalty, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty> ProposePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty> ReviewPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty> ApprovePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty> RejectPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<ContractPenalty> ApplyPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty> ClosePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractPenalty?> GetPenaltyAsync(PenaltyId id, CancellationToken ct = default);
    Task<List<ContractPenalty>> GetPenaltiesByContractAsync(ContractId contractId, CancellationToken ct = default);

    // ── Invoices ────────────────────────────────────────────────
    Task<ContractInvoice> CreateInvoiceAsync(ContractInvoice draft, string actor, CancellationToken ct = default);
    Task<ContractInvoice> UpdateInvoiceDraftAsync(ContractInvoice invoice, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractInvoice> IssueInvoiceAsync(InvoiceId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractInvoice> MarkInvoicePaidAsync(InvoiceId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractInvoice> CancelInvoiceAsync(InvoiceId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<ContractInvoice?> GetInvoiceAsync(InvoiceId id, CancellationToken ct = default);
    Task<List<ContractInvoice>> GetInvoicesByContractAsync(ContractId contractId, CancellationToken ct = default);

    // ── Audit ───────────────────────────────────────────────────
    Task<List<ContractAdministrationAuditEvent>> GetAdministrationAuditLogAsync(ContractId contractId, int skip, int take, CancellationToken ct = default);
    Task<List<ContractAdministrationAuditEvent>> GetEntityAuditLogAsync(string entityType, string entityId, CancellationToken ct = default);

    // ── Export ──────────────────────────────────────────────────
    Task<string> ExportAdjustmentsCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportGuaranteesCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportPenaltiesCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportInvoicesCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportAdministrationAuditCsvAsync(ContractId contractId, CancellationToken ct = default);
}
