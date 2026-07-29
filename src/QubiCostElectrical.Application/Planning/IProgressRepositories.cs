using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IStatusDateRepository
{
    Task<ScheduleStatusDate?> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(ScheduleStatusDate statusDate, CancellationToken ct = default);
}

public interface IActivityProgressRepository
{
    Task<ActivityProgressUpdate?> GetByIdAsync(string progressUpdateId, CancellationToken ct = default);
    Task<ActivityProgressUpdate?> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default);
    Task<List<ActivityProgressUpdate>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(ActivityProgressUpdate update, CancellationToken ct = default);
    Task DeleteAsync(string progressUpdateId, CancellationToken ct = default);
}

public interface IProgressIntervalRepository
{
    Task<List<ProgressInterval>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default);
    Task<List<ProgressInterval>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default);
    Task SaveAsync(ProgressInterval interval, CancellationToken ct = default);
    Task DeleteAsync(string intervalId, CancellationToken ct = default);
}

public interface IDelayReasonRepository
{
    Task<List<DelayReasonEntry>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default);
    Task<List<DelayReasonEntry>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(DelayReasonEntry entry, CancellationToken ct = default);
    Task DeleteAsync(string entryId, CancellationToken ct = default);
}

public interface IProgressEvidenceRepository
{
    Task<List<ProgressEvidence>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default);
    Task SaveAsync(ProgressEvidence evidence, CancellationToken ct = default);
    Task DeleteAsync(string evidenceId, CancellationToken ct = default);
}

public interface IProgressSnapshotRepository
{
    Task<ProgressSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default);
    Task<List<ProgressSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(ProgressSnapshot snapshot, CancellationToken ct = default);
    Task SaveItemsAsync(string snapshotId, IReadOnlyList<ProgressSnapshotItem> items, CancellationToken ct = default);
    Task<List<ProgressSnapshotItem>> GetItemsAsync(string snapshotId, CancellationToken ct = default);
    Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
