using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProgressTrackingService
{
    Task<ScheduleStatusDate> SetStatusDateAsync(ScheduleId scheduleId, DateTime statusDate, string timeZoneId, string comment, string actor, CancellationToken ct = default);
    Task<ScheduleStatusDate?> GetStatusDateAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<ActivityProgressUpdate> RegisterProgressAsync(ActivityProgressUpdate update, string actor, bool allowPercentDecrease = false, CancellationToken ct = default);
    Task<ActivityProgressUpdate> CompleteActivityAsync(ScheduleId scheduleId, ActivityId activityId, DateTime actualFinish, string actor, CancellationToken ct = default);
    Task SuspendActivityAsync(ScheduleId scheduleId, ActivityId activityId, string reason, string actor, CancellationToken ct = default);
    Task ResumeActivityAsync(ScheduleId scheduleId, ActivityId activityId, string actor, CancellationToken ct = default);
    Task<ActivityProgressUpdate> RegisterQuantityAsync(ScheduleId scheduleId, ActivityId activityId, decimal executedQuantity, string actor, CancellationToken ct = default);
    Task<ActivityProgressUpdate> RegisterCostAsync(ScheduleId scheduleId, ActivityId activityId, decimal? actualCost, decimal? committedCost, string actor, CancellationToken ct = default);
    Task<DelayReasonEntry> RegisterDelayReasonAsync(DelayReasonEntry entry, string actor, CancellationToken ct = default);
    Task<ActivityProgressUpdate> RecalculateRemainingAsync(ScheduleId scheduleId, ActivityId activityId, string actor, CancellationToken ct = default);
    Task<ProgressRescheduleEngine.RescheduleResult> RescheduleFromStatusDateAsync(ScheduleId scheduleId, string actor, CancellationToken ct = default);
    Task<ProgressSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default);
    Task<ProgressComparisonResult> CompareSnapshotsAsync(string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct = default);
    Task<ScheduleProgressSummary> GetSummaryAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<IReadOnlyList<OutOfSequenceWarning>> DetectOutOfSequenceAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<ActivityProgressResult> GetActivityResultAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default);
    Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken ct = default);
    Task<List<ActivityProgressUpdate>> ListProgressAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
