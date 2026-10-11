using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractPartyRepository
{
    Task SaveAsync(ContractParty party, CancellationToken ct = default(CancellationToken));

    Task<List<ContractParty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

    Task DeleteAsync(string partyId, CancellationToken ct = default(CancellationToken));
}
