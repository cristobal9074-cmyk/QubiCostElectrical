using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractService
{
    Task<Contract> CreateAsync(Contract draft, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> UpdateDraftAsync(Contract contract, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> SubmitForReviewAsync(ContractId id, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> ApproveAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> RejectAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> SuspendAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> ReactivateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> InitiateCloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> CloseAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> TerminateAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> RescindAsync(ContractId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default(CancellationToken));

    Task<Contract> AddPartyAsync(ContractId id, ContractParty party, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> RemovePartyAsync(ContractId id, string partyId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> AddMilestoneAsync(ContractId id, ContractMilestone milestone, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> RemoveMilestoneAsync(ContractId id, string milestoneId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> AddScopeLinkAsync(ContractId id, ContractScopeLink link, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> RemoveScopeLinkAsync(ContractId id, string linkId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> AddDocumentAsync(ContractId id, ContractDocument document, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<Contract> RemoveDocumentAsync(ContractId id, string documentId, string expectedRowVersion, string actor, CancellationToken ct = default(CancellationToken));

    Task<List<ContractVersion>> GetVersionsAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

    Task<ContractVersion> VerifyIntegrityAsync(ContractVersionId versionId, CancellationToken ct = default(CancellationToken));

    Task<string> CompareVersionsAsync(ContractVersionId v1Id, ContractVersionId v2Id, CancellationToken ct = default(CancellationToken));

    Task<Contract?> GetAsync(ContractId id, CancellationToken ct = default(CancellationToken));

    Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default(CancellationToken));

    Task<List<ContractAuditEvent>> GetAuditLogAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));
}
