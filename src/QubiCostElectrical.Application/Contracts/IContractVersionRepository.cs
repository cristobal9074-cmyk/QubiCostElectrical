using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractVersionRepository
{
    Task SaveAsync(ContractVersion version, CancellationToken ct = default(CancellationToken));

    Task<ContractVersion?> GetByIdAsync(ContractVersionId id, CancellationToken ct = default(CancellationToken));

    Task<List<ContractVersion>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

    Task<int> GetLatestVersionNumberAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));
}
