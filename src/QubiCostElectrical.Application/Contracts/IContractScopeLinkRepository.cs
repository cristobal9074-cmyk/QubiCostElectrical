using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractScopeLinkRepository
{
    Task SaveAsync(ContractScopeLink link, CancellationToken ct = default(CancellationToken));

    Task<List<ContractScopeLink>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

    Task DeleteAsync(string linkId, CancellationToken ct = default(CancellationToken));
}
