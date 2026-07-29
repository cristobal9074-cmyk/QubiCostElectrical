using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractDashboardService
{
    Task<ContractExecutiveDashboard> GetExecutiveAsync(
        ContractId contractId,
        ContractDashboardFilter? filter,
        string actor,
        CancellationToken ct = default);

    Task<List<ContractDashboardSnapshot>> ListSnapshotsAsync(
        ContractId contractId,
        CancellationToken ct = default);

    Task<ContractDashboardSnapshot> CreateSnapshotAsync(
        ContractId contractId,
        string comment,
        string actor,
        CancellationToken ct = default);

    Task<ContractComparisonResult> CompareSnapshotsAsync(
        string snapshotIdA,
        string snapshotIdB,
        string actor,
        CancellationToken ct = default);

    Task AcknowledgeAlertAsync(
        string alertId,
        string actor,
        string? resolution,
        CancellationToken ct = default);

    Task ResolveAlertAsync(
        string alertId,
        string actor,
        string resolution,
        CancellationToken ct = default);

    Task<string> ExportDashboardCsvAsync(
        ContractId contractId,
        CancellationToken ct = default);

    Task<string> ExportDashboardJsonAsync(
        ContractId contractId,
        CancellationToken ct = default);
}

public sealed record ContractDashboardFilter
{
    public string? CurrencyCode { get; init; }
    public bool IncludeEvm { get; init; } = true;
    public bool IncludePurchases { get; init; } = true;
}
