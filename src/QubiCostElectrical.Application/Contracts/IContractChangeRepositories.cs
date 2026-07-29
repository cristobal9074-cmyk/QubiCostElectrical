using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IChangeRequestRepository
{
    Task SaveAsync(ChangeRequest request, CancellationToken ct = default);
    Task<ChangeRequest?> GetByIdAsync(ChangeRequestId id, CancellationToken ct = default);
    Task<List<ChangeRequest>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ChangeRequest>> SearchAsync(string projectPath, string? text, ChangeRequestStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
}

public interface IChangeOrderRepository
{
    Task SaveAsync(ChangeOrder order, CancellationToken ct = default);
    Task<ChangeOrder?> GetByIdAsync(ChangeOrderId id, CancellationToken ct = default);
    Task<List<ChangeOrder>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ChangeOrder>> GetByChangeRequestAsync(ChangeRequestId changeRequestId, CancellationToken ct = default);
    Task<List<ChangeOrder>> SearchAsync(string projectPath, string? text, ChangeOrderStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
    Task SaveVersionAsync(ChangeOrderVersion version, CancellationToken ct = default);
    Task<List<ChangeOrderVersion>> GetVersionsAsync(ChangeOrderId orderId, CancellationToken ct = default);
    Task SaveSnapshotAsync(ChangeOrderSnapshot snapshot, CancellationToken ct = default);
    Task<List<ChangeOrderSnapshot>> GetSnapshotsAsync(ChangeOrderId orderId, CancellationToken ct = default);
}

public interface IChangeOrderLineRepository
{
    Task SaveRangeAsync(IEnumerable<ChangeOrderLine> lines, CancellationToken ct = default);
    Task<List<ChangeOrderLine>> GetByChangeOrderAsync(ChangeOrderId orderId, CancellationToken ct = default);
    Task DeleteByChangeOrderAsync(ChangeOrderId orderId, CancellationToken ct = default);
}

public interface IExtraWorkRepository
{
    Task SaveAsync(ExtraWork extraWork, CancellationToken ct = default);
    Task<ExtraWork?> GetByIdAsync(ExtraWorkId id, CancellationToken ct = default);
    Task<List<ExtraWork>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ExtraWork>> SearchAsync(string projectPath, string? text, ExtraWorkStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
}

public interface IContractClaimRepository
{
    Task SaveAsync(ContractClaim claim, CancellationToken ct = default);
    Task<ContractClaim?> GetByIdAsync(ClaimId id, CancellationToken ct = default);
    Task<List<ContractClaim>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ContractClaim>> SearchAsync(string projectPath, string? text, ClaimStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
    Task SaveEventAsync(ClaimEvent claimEvent, CancellationToken ct = default);
    Task<List<ClaimEvent>> GetEventsAsync(ClaimId claimId, CancellationToken ct = default);
    Task SaveEvidenceAsync(ClaimEvidence evidence, CancellationToken ct = default);
    Task<List<ClaimEvidence>> GetEvidenceAsync(ClaimId claimId, CancellationToken ct = default);
    Task SaveEvaluationAsync(ClaimEvaluation evaluation, CancellationToken ct = default);
    Task<ClaimEvaluation?> GetEvaluationAsync(ClaimId claimId, CancellationToken ct = default);
    Task SaveResponseAsync(ClaimResponse response, CancellationToken ct = default);
    Task<ClaimResponse?> GetResponseAsync(ClaimId claimId, CancellationToken ct = default);
}

public interface IContractChangeAuditRepository
{
    Task SaveAsync(ContractChangeAuditEvent auditEvent, CancellationToken ct = default);
    Task<List<ContractChangeAuditEvent>> GetByContractAsync(ContractId contractId, int skip, int take, CancellationToken ct = default);
    Task<List<ContractChangeAuditEvent>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default);
}

public interface IContractChangePermissionChecker
{
    bool CanViewChangeRequest(UserRole role);
    bool CanCreateChangeRequest(UserRole role);
    bool CanEditChangeRequest(UserRole role);
    bool CanSubmitChangeRequest(UserRole role);
    bool CanAnalyzeChangeRequest(UserRole role);
    bool CanCreateChangeOrder(UserRole role);
    bool CanEditChangeOrderDraft(UserRole role);
    bool CanNegotiateChangeOrder(UserRole role);
    bool CanApproveChangeOrder(UserRole role);
    bool CanRejectChangeOrder(UserRole role);
    bool CanImplementChangeOrder(UserRole role);
    bool CanManageExtraWork(UserRole role);
    bool CanViewClaim(UserRole role);
    bool CanCreateClaim(UserRole role);
    bool CanSubmitClaim(UserRole role);
    bool CanEvaluateClaim(UserRole role);
    bool CanRespondClaim(UserRole role);
    bool CanApproveClaim(UserRole role);
    bool CanViewContractChangeAudit(UserRole role);
    bool CanExportContractChange(UserRole role);
}
