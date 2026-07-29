using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ProgressTrackingService
{
    public async Task<ProgressRescheduleEngine.RescheduleResult> RescheduleFromStatusDateAsync(
        ScheduleId scheduleId, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ScheduleReschedule, actor, scheduleId, "Schedule", "Reschedule");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);

        var result = ProgressRescheduleEngine.RescheduleRemaining(statusDate.StatusDate, activities, progressMap);

        foreach (var act in result.UpdatedActivities)
        {
            if (result.ChangedActivityIds.Contains(act.Id.Value))
                await _activityRepo.SaveAsync(act, ct);
        }

        foreach (var kv in result.UpdatedProgress)
            await _progressRepo.SaveAsync(kv.Value, ct);

        if (_cpm is not null)
            await _cpm.CalculateScheduleAsync(scheduleId, ct);

        await AuditAsync(scheduleId, "Schedule", scheduleId.Value, "Reschedule", actor,
            null, statusDate.StatusDate.ToString("O"),
            $"Actividades afectadas: {result.ChangedActivityIds.Count}", "Ok", ct);
        return result;
    }

    public async Task<ProgressSnapshot> CreateSnapshotAsync(
        ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressSnapshotCreate, actor, scheduleId, "ProgressSnapshot", "Create");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);
        var deps = await _dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        var oos = OutOfSequenceDetector.Detect(statusDate.StatusDate, activities, deps, progressMap);
        var forecastFinish = ProgressForecastEngine.ForecastProjectFinish(activities, progressMap, statusDate.StatusDate);
        var summary = ScheduleProgressSummaryCalculator.Calculate(
            scheduleId, statusDate.StatusDate, activities, progressMap, _policy, oos, forecastFinish);

        var version = await _snapshotRepo.GetNextVersionAsync(scheduleId, ct);
        var items = new List<ProgressSnapshotItem>();
        foreach (var act in activities.Where(a => !a.IsDeleted && a.Type != ActivityType.Summary))
        {
            progressMap.TryGetValue(act.Id.Value, out var prog);
            var (fs, ff) = ProgressForecastEngine.ForecastActivity(act, prog, statusDate.StatusDate);
            items.Add(new ProgressSnapshotItem
            {
                SnapshotId = string.Empty,
                ActivityId = act.Id,
                Progress = prog ?? new ActivityProgressUpdate
                {
                    ProgressUpdateId = Guid.NewGuid().ToString("N"),
                    ScheduleId = scheduleId,
                    ActivityId = act.Id,
                    StatusDate = statusDate.StatusDate,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = actor,
                    ModifiedUtc = DateTime.UtcNow,
                    ModifiedBy = actor,
                    RowVersion = Guid.NewGuid().ToString("N")
                },
                ForecastStart = fs,
                ForecastFinish = ff,
                IsCritical = act.IsCritical
            });
        }

        var snapshotId = Guid.NewGuid().ToString("N");
        items = items.Select(i => new ProgressSnapshotItem
        {
            SnapshotId = snapshotId,
            ActivityId = i.ActivityId,
            Progress = i.Progress,
            ForecastStart = i.ForecastStart,
            ForecastFinish = i.ForecastFinish,
            IsCritical = i.IsCritical
        }).ToList();

        var criticalIds = activities.Where(a => a.IsCritical).Select(a => a.Id.Value).ToList();
        var draft = new ProgressSnapshot
        {
            SnapshotId = snapshotId,
            ScheduleId = scheduleId,
            Version = version,
            StatusDate = statusDate.StatusDate,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            Comment = comment ?? string.Empty,
            SHA256 = string.Empty,
            Summary = summary,
            ForecastProjectFinish = forecastFinish,
            CriticalPathActivityIds = criticalIds
        };
        var hash = ProgressSnapshotHasher.ComputeHash(draft, items);
        var snapshot = new ProgressSnapshot
        {
            SnapshotId = snapshotId,
            ScheduleId = scheduleId,
            Version = version,
            StatusDate = statusDate.StatusDate,
            CreatedUtc = draft.CreatedUtc,
            CreatedBy = actor,
            Comment = draft.Comment,
            SHA256 = hash,
            Summary = summary,
            ForecastProjectFinish = forecastFinish,
            CriticalPathActivityIds = criticalIds
        };

        await _snapshotRepo.SaveAsync(snapshot, ct);
        await _snapshotRepo.SaveItemsAsync(snapshotId, items, ct);
        await AuditAsync(scheduleId, "ProgressSnapshot", snapshotId, "CreateSnapshot", actor, null, $"v{version}", comment, "Ok", ct);
        return snapshot;
    }

    public async Task<ProgressComparisonResult> CompareSnapshotsAsync(
        string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct = default)
    {
        var snapA = await _snapshotRepo.GetByIdAsync(snapshotIdA, ct)
            ?? throw new InvalidOperationException("Snapshot A no encontrado.");
        Ensure(PlanningPermissions.ProgressSnapshotCompare, actor, snapA.ScheduleId, "ProgressSnapshot", "Compare");
        var snapB = await _snapshotRepo.GetByIdAsync(snapshotIdB, ct)
            ?? throw new InvalidOperationException("Snapshot B no encontrado.");

        var itemsA = await _snapshotRepo.GetItemsAsync(snapshotIdA, ct);
        var itemsB = await _snapshotRepo.GetItemsAsync(snapshotIdB, ct);
        var left = itemsA.ToDictionary(i => i.ActivityId.Value, i => i.Progress);
        var right = itemsB.ToDictionary(i => i.ActivityId.Value, i => i.Progress);
        var leftCrit = itemsA.Where(i => i.IsCritical).Select(i => i.ActivityId.Value).ToHashSet();
        var rightCrit = itemsB.Where(i => i.IsCritical).Select(i => i.ActivityId.Value).ToHashSet();

        var result = ProgressComparisonEngine.Compare(
            $"Snapshot v{snapA.Version}", $"Snapshot v{snapB.Version}",
            snapA.StatusDate, snapB.StatusDate, left, right, null, leftCrit, rightCrit);

        await AuditAsync(snapA.ScheduleId, "ProgressSnapshot", snapshotIdA, "Compare", actor, snapshotIdA, snapshotIdB, null, "Ok", ct);
        return result;
    }

    public async Task<ScheduleProgressSummary> GetSummaryAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressView, "system", scheduleId, "ProgressUpdate", "Summary");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);
        var deps = await _dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        var oos = OutOfSequenceDetector.Detect(statusDate.StatusDate, activities, deps, progressMap);
        var forecast = ProgressForecastEngine.ForecastProjectFinish(activities, progressMap, statusDate.StatusDate);
        return ScheduleProgressSummaryCalculator.Calculate(
            scheduleId, statusDate.StatusDate, activities, progressMap, _policy, oos, forecast);
    }

    public async Task<IReadOnlyList<OutOfSequenceWarning>> DetectOutOfSequenceAsync(
        ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressView, "system", scheduleId, "ProgressUpdate", "OOS");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);
        var deps = await _dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        return OutOfSequenceDetector.Detect(statusDate.StatusDate, activities, deps, progressMap);
    }

    public async Task<ActivityProgressResult> GetActivityResultAsync(
        ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressView, "system", scheduleId, "ProgressUpdate", "ActivityResult");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activity = await _activityRepo.GetByIdAsync(activityId, ct)
            ?? throw new InvalidOperationException("Actividad no encontrada.");
        var progress = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct);
        var oos = await DetectOutOfSequenceAsync(scheduleId, ct);
        return ActivityProgressResultBuilder.Build(activity, progress, statusDate.StatusDate, null, null, oos);
    }

    public async Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken ct = default)
    {
        var snap = await _snapshotRepo.GetByIdAsync(snapshotId, ct);
        if (snap is null) return false;
        Ensure(PlanningPermissions.ProgressAuditView, "system", snap.ScheduleId, "ProgressSnapshot", "Verify");
        var items = await _snapshotRepo.GetItemsAsync(snapshotId, ct);
        return ProgressSnapshotHasher.Verify(snap, items);
    }

    private async Task<ScheduleStatusDate> RequireStatusDateAsync(ScheduleId scheduleId, CancellationToken ct)
    {
        return await _statusDateRepo.GetByScheduleAsync(scheduleId, ct)
            ?? throw new InvalidOperationException("Debe establecerse una fecha de estado antes de continuar.");
    }

    private void Ensure(string permission, string actor, ScheduleId scheduleId, string entityType, string action)
    {
        if (_permissions.Has(permission)) return;
        _ = AuditAsync(scheduleId, entityType, scheduleId.Value, "Denied", actor, null, permission, action, "Denied", CancellationToken.None);
        throw new UnauthorizedAccessException($"Permiso denegado: {permission}");
    }

    private Task AuditAsync(
        ScheduleId scheduleId, string entityType, string entityId, string action, string actor,
        string? previous, string? next, string? comment, string result, CancellationToken ct) =>
        _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId.Value,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            UtcTimestamp = DateTime.UtcNow,
            PreviousState = previous,
            NewState = next,
            Comment = comment ?? string.Empty,
            Result = result
        }, ct);

    private static ActivityProgressUpdate CloneWithStatus(
        ActivityProgressUpdate previous, DateTime statusDate, ProgressTrackingStatus status, string actor, string comment) =>
        new()
        {
            ProgressUpdateId = previous.ProgressUpdateId,
            ScheduleId = previous.ScheduleId,
            ActivityId = previous.ActivityId,
            StatusDate = statusDate,
            ActualStart = previous.ActualStart,
            ActualFinish = previous.ActualFinish,
            ActualDuration = previous.ActualDuration,
            PercentComplete = previous.PercentComplete,
            PhysicalProgress = previous.PhysicalProgress,
            FinancialProgress = previous.FinancialProgress,
            ProgressMethod = previous.ProgressMethod,
            PlannedQuantity = previous.PlannedQuantity,
            ExecutedQuantity = previous.ExecutedQuantity,
            QuantityUnit = previous.QuantityUnit,
            ActualHours = previous.ActualHours,
            ActualCost = previous.ActualCost,
            CommittedCost = previous.CommittedCost,
            CurrencyCode = previous.CurrencyCode,
            RemainingDuration = previous.RemainingDuration,
            RemainingQuantity = previous.RemainingQuantity,
            Productivity = previous.Productivity,
            PlannedProductivity = previous.PlannedProductivity,
            CrewInfo = previous.CrewInfo,
            ProgressStatus = status,
            DelayReason = previous.DelayReason,
            Comment = comment ?? previous.Comment,
            EvidenceReference = previous.EvidenceReference,
            ManualWeight = previous.ManualWeight,
            ForecastStart = previous.ForecastStart,
            ForecastFinish = previous.ForecastFinish,
            CreatedUtc = previous.CreatedUtc,
            CreatedBy = previous.CreatedBy,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

    private static ActivityProgressUpdate BaseFromPrevious(
        ActivityProgressUpdate? previous, ScheduleId scheduleId, ActivityId activityId, DateTime statusDate, string actor) =>
        previous ?? new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };
}
