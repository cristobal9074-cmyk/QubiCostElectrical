using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IEarnedValueService
{
    Task<EarnedValueResult> CalculateAsync(ScheduleId scheduleId, string actor, decimal? projectBac = null, EacMethod? eacMethod = null, CancellationToken ct = default);
    Task<EarnedValueComparison> CompareSnapshotsAsync(string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct = default);
    Task<EarnedValueComparison> CompareResultsAsync(EarnedValueResult left, EarnedValueResult right, string leftLabel, string rightLabel, string actor, CancellationToken ct = default);
    Task<EarnedValueSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default);
    Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken ct = default);
    Task<List<EarnedValueSnapshot>> ListSnapshotsAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<List<EarnedValueHistoryEntry>> GetHistoryAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default);
    Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
