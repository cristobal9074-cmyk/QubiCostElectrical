using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IEarnedValueRepository
{
    Task SaveLatestAsync(EarnedValueResult result, CancellationToken ct = default);
    Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default);
}

public interface IEarnedValueSnapshotRepository
{
    Task SaveAsync(EarnedValueSnapshot snapshot, CancellationToken ct = default);
    Task<EarnedValueSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default);
    Task<List<EarnedValueSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default);
}

public interface IEarnedValueHistoryRepository
{
    Task AppendAsync(EarnedValueHistoryEntry entry, CancellationToken ct = default);
    Task<List<EarnedValueHistoryEntry>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default);
}
