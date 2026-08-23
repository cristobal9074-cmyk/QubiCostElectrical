using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractAuditRepository
{
	Task RecordAsync(ContractAuditEvent evt, CancellationToken ct = default(CancellationToken));

	Task<List<ContractAuditEvent>> GetByContractAsync(ContractId contractId, int limit = 100, CancellationToken ct = default(CancellationToken));
}
