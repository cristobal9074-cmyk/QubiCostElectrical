using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractMilestoneRepository
{
	Task SaveAsync(ContractMilestone milestone, CancellationToken ct = default(CancellationToken));

	Task<List<ContractMilestone>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

	Task DeleteAsync(string milestoneId, CancellationToken ct = default(CancellationToken));
}
