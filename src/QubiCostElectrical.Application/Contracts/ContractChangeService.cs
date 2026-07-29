using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────────
// Interfaces
// ──────────────────────────────────────────────────────────────────

public interface IContractChangeService
{
    // ── ChangeRequest ────────────────────────────────────────────
    Task<ChangeRequest> CreateChangeRequestAsync(ChangeRequest draft, string actor, CancellationToken ct = default);
    Task<ChangeRequest> UpdateChangeRequestDraftAsync(ChangeRequest request, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeRequest> SubmitChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeRequest> AnalyzeChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeRequest> ApproveChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeRequest> RejectChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeRequest> CancelChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeRequest> ConvertToChangeOrderAsync(ChangeRequestId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeRequest?> GetChangeRequestAsync(ChangeRequestId id, CancellationToken ct = default);
    Task<List<ChangeRequest>> SearchChangeRequestsAsync(string projectPath, string? text, ChangeRequestStatus? status, int skip, int take, CancellationToken ct = default);

    // ── ChangeOrder ──────────────────────────────────────────────
    Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder draft, string actor, CancellationToken ct = default);
    Task<ChangeOrder> UpdateChangeOrderDraftAsync(ChangeOrder order, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeOrder> SubmitChangeOrderForReviewAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeOrder> NegotiateChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeOrder> ApproveChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeOrder> RejectChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ChangeOrder> ImplementChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeOrder> CloseChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ChangeOrder?> GetChangeOrderAsync(ChangeOrderId id, CancellationToken ct = default);
    Task<List<ChangeOrder>> SearchChangeOrdersAsync(string projectPath, string? text, ChangeOrderStatus? status, int skip, int take, CancellationToken ct = default);

    // ── ChangeOrder Versions & Snapshots ─────────────────────────
    Task<ChangeOrderVersion> CreateChangeOrderVersionAsync(ChangeOrderId orderId, string actor, string reason, CancellationToken ct = default);
    Task<List<ChangeOrderVersion>> GetChangeOrderVersionsAsync(ChangeOrderId orderId, CancellationToken ct = default);
    Task<ChangeOrderVersion> VerifyChangeOrderIntegrityAsync(ChangeOrderVersionId versionId, CancellationToken ct = default);
    Task<ChangeOrderSnapshot> CreateChangeOrderSnapshotAsync(ChangeOrderId orderId, ContractId contractId, string projectPath, string versionLabel, CancellationToken ct = default);
    Task<List<ChangeOrderSnapshot>> GetChangeOrderSnapshotsAsync(ChangeOrderId orderId, CancellationToken ct = default);

    // ── ChangeOrder Lines ────────────────────────────────────────
    Task SetChangeOrderLinesAsync(ChangeOrderId orderId, IEnumerable<ChangeOrderLine> lines, CancellationToken ct = default);
    Task<List<ChangeOrderLine>> GetChangeOrderLinesAsync(ChangeOrderId orderId, CancellationToken ct = default);

    // ── ExtraWork ────────────────────────────────────────────────
    Task<ExtraWork> CreateExtraWorkAsync(ExtraWork draft, string actor, CancellationToken ct = default);
    Task<ExtraWork> UpdateExtraWorkDraftAsync(ExtraWork extraWork, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ExtraWork> InstructExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, string instruction, CancellationToken ct = default);
    Task<ExtraWork> ExecuteExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ExtraWork> SubmitExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ExtraWork> ApproveExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ExtraWork> RejectExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ExtraWork> ValueExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, decimal valuationAmount, CancellationToken ct = default);
    Task<ExtraWork> CloseExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ExtraWork?> GetExtraWorkAsync(ExtraWorkId id, CancellationToken ct = default);
    Task<List<ExtraWork>> SearchExtraWorksAsync(string projectPath, string? text, ExtraWorkStatus? status, int skip, int take, CancellationToken ct = default);

    // ── Claim ────────────────────────────────────────────────────
    Task<ContractClaim> CreateClaimAsync(ContractClaim draft, string actor, CancellationToken ct = default);
    Task<ContractClaim> UpdateClaimDraftAsync(ContractClaim claim, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractClaim> NotifyClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractClaim> SubmitClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractClaim> EvaluateClaimAsync(ClaimId id, string expectedRowVersion, string actor, decimal evaluatedAmount, int evaluatedDays, string justification, string recommendation, CancellationToken ct = default);
    Task<ContractClaim> RespondToClaimAsync(ClaimId id, string expectedRowVersion, string actor, string responseType, string responseDetail, decimal offeredAmount, int offeredDays, CancellationToken ct = default);
    Task<ContractClaim> ApproveClaimAsync(ClaimId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default);
    Task<ContractClaim> WithdrawClaimAsync(ClaimId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<ContractClaim> CloseClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<ContractClaim?> GetClaimAsync(ClaimId id, CancellationToken ct = default);
    Task<List<ContractClaim>> SearchClaimsAsync(string projectPath, string? text, ClaimStatus? status, int skip, int take, CancellationToken ct = default);

    // ── Claim Sub-entities ───────────────────────────────────────
    Task<ClaimEvent> AddClaimEventAsync(ClaimId claimId, string eventType, string description, string actor, CancellationToken ct = default);
    Task<List<ClaimEvent>> GetClaimEventsAsync(ClaimId claimId, CancellationToken ct = default);
    Task<ClaimEvidence> AddClaimEvidenceAsync(ClaimId claimId, string documentReference, string description, string fileName, string uploadedBy, CancellationToken ct = default);
    Task<List<ClaimEvidence>> GetClaimEvidenceAsync(ClaimId claimId, CancellationToken ct = default);
    Task<ClaimEvaluation?> GetClaimEvaluationAsync(ClaimId claimId, CancellationToken ct = default);
    Task<ClaimResponse?> GetClaimResponseAsync(ClaimId claimId, CancellationToken ct = default);

    // ── Impact Analysis ──────────────────────────────────────────
    Task<ImpactAnalysisResult> CalculateImpactAsync(ChangeOrderId orderId, CancellationToken ct = default);
    Task<ImpactAnalysisResult> CalculateChangeRequestImpactAsync(ChangeRequestId requestId, CancellationToken ct = default);

    // ── Audit ────────────────────────────────────────────────────
    Task<List<ContractChangeAuditEvent>> GetContractChangeAuditLogAsync(ContractId contractId, int skip, int take, CancellationToken ct = default);
    Task<List<ContractChangeAuditEvent>> GetEntityAuditLogAsync(string entityType, string entityId, CancellationToken ct = default);

    // ── Export ───────────────────────────────────────────────────
    Task<string> ExportChangeRequestsCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportChangeOrdersCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportExtraWorksCsvAsync(ContractId contractId, CancellationToken ct = default);
    Task<string> ExportClaimsCsvAsync(ContractId contractId, CancellationToken ct = default);
}

// ──────────────────────────────────────────────────────────────────
// Implementation (partial — shared fields, constructor, helpers)
// ──────────────────────────────────────────────────────────────────

public sealed partial class ContractChangeService : IContractChangeService
{
    // Repositories
    private readonly IChangeRequestRepository _changeRequestRepo;
    private readonly IChangeOrderRepository _changeOrderRepo;
    private readonly IChangeOrderLineRepository _changeOrderLineRepo;
    private readonly IExtraWorkRepository _extraWorkRepo;
    private readonly IContractClaimRepository _claimRepo;
    private readonly IContractChangeAuditRepository _auditRepo;
    private readonly IContractChangePermissionChecker _permissions;
    private readonly IPermissionService _userPermissions;
    private readonly SoDPolicyConfig _sod;

    public ContractChangeService(
        IChangeRequestRepository changeRequestRepo,
        IChangeOrderRepository changeOrderRepo,
        IChangeOrderLineRepository changeOrderLineRepo,
        IExtraWorkRepository extraWorkRepo,
        IContractClaimRepository claimRepo,
        IContractChangeAuditRepository auditRepo,
        IContractChangePermissionChecker permissions,
        IPermissionService userPermissions,
        SoDPolicyConfig? sod = null)
    {
        _changeRequestRepo = changeRequestRepo;
        _changeOrderRepo = changeOrderRepo;
        _changeOrderLineRepo = changeOrderLineRepo;
        _extraWorkRepo = extraWorkRepo;
        _claimRepo = claimRepo;
        _auditRepo = auditRepo;
        _permissions = permissions;
        _userPermissions = userPermissions;
        _sod = sod ?? new SoDPolicyConfig();
    }

    // ── Helpers ─────────────────────────────────────────────

    private void Ensure(bool canDo, string action)
    {
        if (!canDo)
            throw new UnauthorizedAccessException($"No tiene permisos para la acción de cambio contractual: {action}.");
    }

    private async Task RecordAuditAsync(ContractId contractId, string entityType, string entityId, string action, string actor, string details, bool isDenied = false, CancellationToken ct = default)
    {
        var audit = new ContractChangeAuditEvent
        {
            Id = ContractChangeAuditEventId.New(),
            ContractId = contractId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Action = action, Details = details, Date = DateTime.UtcNow }),
            IsDeniedAttempt = isDenied
        };
        await _auditRepo.SaveAsync(audit, ct);
    }

    private static string ComputeHash(string json)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }
}
