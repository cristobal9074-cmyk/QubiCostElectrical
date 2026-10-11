using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractRepository
{
    Task SaveAsync(Contract contract, CancellationToken ct = default(CancellationToken));

    Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default(CancellationToken));

    Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default(CancellationToken));

    Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default(CancellationToken));

    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default(CancellationToken));
}
