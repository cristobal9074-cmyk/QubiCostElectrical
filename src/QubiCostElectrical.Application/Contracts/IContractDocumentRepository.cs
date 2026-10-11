using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface IContractDocumentRepository
{
    Task SaveAsync(ContractDocument document, CancellationToken ct = default(CancellationToken));

    Task<List<ContractDocument>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken));

    Task DeleteAsync(string documentId, CancellationToken ct = default(CancellationToken));
}
