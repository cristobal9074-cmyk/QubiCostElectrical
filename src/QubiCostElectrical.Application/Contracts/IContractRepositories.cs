using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractRepository
{
    Task SaveAsync(Contract contract, CancellationToken ct = default);
    Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default);
    Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default);
    Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
}

public interface IContractVersionRepository
{
    Task SaveAsync(ContractVersion version, CancellationToken ct = default);
    Task<ContractVersion?> GetByIdAsync(ContractVersionId id, CancellationToken ct = default);
    Task<List<ContractVersion>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<int> GetLatestVersionNumberAsync(ContractId contractId, CancellationToken ct = default);
}

public interface IContractPartyRepository
{
    Task SaveAsync(ContractParty party, CancellationToken ct = default);
    Task<List<ContractParty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task DeleteAsync(string partyId, CancellationToken ct = default);
}

public interface IContractMilestoneRepository
{
    Task SaveAsync(ContractMilestone milestone, CancellationToken ct = default);
    Task<List<ContractMilestone>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task DeleteAsync(string milestoneId, CancellationToken ct = default);
}

public interface IContractScopeLinkRepository
{
    Task SaveAsync(ContractScopeLink link, CancellationToken ct = default);
    Task<List<ContractScopeLink>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task DeleteAsync(string linkId, CancellationToken ct = default);
}

public interface IContractDocumentRepository
{
    Task SaveAsync(ContractDocument document, CancellationToken ct = default);
    Task<List<ContractDocument>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}

public interface IContractAuditRepository
{
    Task RecordAsync(ContractAuditEvent evt, CancellationToken ct = default);
    Task<List<ContractAuditEvent>> GetByContractAsync(ContractId contractId, int limit = 100, CancellationToken ct = default);
}
