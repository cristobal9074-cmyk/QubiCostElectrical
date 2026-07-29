using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractDashboardRepository
{
    Task<List<ContractDashboardSnapshot>> ListSnapshotsAsync(ContractId contractId, CancellationToken ct = default);
    Task<ContractExecutiveDashboard?> GetSnapshotAsync(string snapshotId, CancellationToken ct = default);
    Task SaveSnapshotAsync(ContractDashboardSnapshot snapshot, string jsonData, CancellationToken ct = default);
    Task<ContractAlert?> GetAlertAsync(string alertId, CancellationToken ct = default);
    Task UpdateAlertStatusAsync(string alertId, ContractAlertStatus newStatus, string actor, CancellationToken ct = default);
    Task<List<ContractAlert>> GetAlertsByContractAsync(ContractId contractId, CancellationToken ct = default);
}
