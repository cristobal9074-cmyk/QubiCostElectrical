using System;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IScheduleBaselineSnapshotRepository
{
    Task<ScheduleBaselineSnapshot?> GetByBaselineIdAsync(string baselineId, CancellationToken ct = default);
    Task SaveAsync(string baselineId, ScheduleBaselineSnapshot snapshot, CancellationToken ct = default);
    Task DeleteAsync(string baselineId, CancellationToken ct = default);
}
