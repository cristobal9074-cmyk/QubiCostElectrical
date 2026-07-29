using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ProgressTrackingService
{
    public async Task<ActivityProgressUpdate> CompleteActivityAsync(
        ScheduleId scheduleId, ActivityId activityId, DateTime actualFinish, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ActualDatesManage, actor, scheduleId, "ProgressUpdate", "Complete");
        var activity = await _activityRepo.GetByIdAsync(activityId, ct)
            ?? throw new InvalidOperationException("Actividad no encontrada.");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct);

        var start = previous?.ActualStart ?? actualFinish;
        var update = new ActivityProgressUpdate
        {
            ProgressUpdateId = previous?.ProgressUpdateId ?? Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate.StatusDate,
            ActualStart = start,
            ActualFinish = actualFinish,
            PercentComplete = 100m,
            PhysicalProgress = 100m,
            FinancialProgress = previous?.FinancialProgress ?? 100m,
            ProgressMethod = previous?.ProgressMethod ?? ProgressMethod.Manual,
            PlannedQuantity = previous?.PlannedQuantity ?? activity.PlannedQuantity,
            ExecutedQuantity = previous?.ExecutedQuantity ?? activity.PlannedQuantity,
            QuantityUnit = previous?.QuantityUnit ?? activity.QuantityUnit,
            ActualHours = previous?.ActualHours,
            ActualCost = previous?.ActualCost,
            CommittedCost = previous?.CommittedCost,
            CurrencyCode = previous?.CurrencyCode ?? _policy.CurrencyCode,
            ProgressStatus = ProgressTrackingStatus.Completed,
            Comment = previous?.Comment ?? "Completada",
            EvidenceReference = previous?.EvidenceReference ?? string.Empty,
            ManualWeight = previous?.ManualWeight ?? 0m,
            CreatedUtc = previous?.CreatedUtc ?? DateTime.UtcNow,
            CreatedBy = previous?.CreatedBy ?? actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        return await RegisterProgressAsync(update, actor, ct: ct);
    }

    public async Task SuspendActivityAsync(
        ScheduleId scheduleId, ActivityId activityId, string reason, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressUpdate, actor, scheduleId, "ProgressUpdate", "Suspend");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct)
            ?? throw new InvalidOperationException("Debe existir avance antes de suspender.");

        var updated = CloneWithStatus(previous, statusDate.StatusDate, ProgressTrackingStatus.Suspended, actor, reason);
        await _progressRepo.SaveAsync(updated, ct);
        await _intervalRepo.SaveAsync(new ProgressInterval
        {
            IntervalId = Guid.NewGuid().ToString("N"),
            ProgressUpdateId = updated.ProgressUpdateId,
            ScheduleId = scheduleId,
            ActivityId = activityId,
            Start = statusDate.StatusDate,
            Finish = null,
            Reason = reason ?? string.Empty,
            IsSuspension = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor
        }, ct);
        await AuditAsync(scheduleId, "ProgressUpdate", updated.ProgressUpdateId, "Suspend", actor, null, reason, reason, "Ok", ct);
    }

    public async Task ResumeActivityAsync(
        ScheduleId scheduleId, ActivityId activityId, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressUpdate, actor, scheduleId, "ProgressUpdate", "Resume");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct)
            ?? throw new InvalidOperationException("Debe existir avance antes de reanudar.");

        var open = (await _intervalRepo.GetByActivityAsync(scheduleId, activityId, ct))
            .Where(i => i.IsSuspension && !i.Finish.HasValue)
            .OrderByDescending(i => i.Start)
            .FirstOrDefault();
        if (open is not null)
        {
            await _intervalRepo.SaveAsync(new ProgressInterval
            {
                IntervalId = open.IntervalId,
                ProgressUpdateId = open.ProgressUpdateId,
                ScheduleId = open.ScheduleId,
                ActivityId = open.ActivityId,
                Start = open.Start,
                Finish = statusDate.StatusDate,
                Reason = open.Reason,
                Hours = open.Hours,
                Production = open.Production,
                Observation = open.Observation,
                IsSuspension = true,
                CreatedUtc = open.CreatedUtc,
                CreatedBy = open.CreatedBy
            }, ct);
        }

        var updated = CloneWithStatus(previous, statusDate.StatusDate, ProgressTrackingStatus.InProgress, actor, "Reanudada");
        await _progressRepo.SaveAsync(updated, ct);
        await AuditAsync(scheduleId, "ProgressUpdate", updated.ProgressUpdateId, "Resume", actor, null, null, null, "Ok", ct);
    }

    public async Task<ActivityProgressUpdate> RegisterQuantityAsync(
        ScheduleId scheduleId, ActivityId activityId, decimal executedQuantity, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressUpdate, actor, scheduleId, "ProgressUpdate", "RegisterQuantity");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activity = await _activityRepo.GetByIdAsync(activityId, ct)
            ?? throw new InvalidOperationException("Actividad no encontrada.");
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct);

        var update = BaseFromPrevious(previous, scheduleId, activityId, statusDate.StatusDate, actor);
        update = new ActivityProgressUpdate
        {
            ProgressUpdateId = update.ProgressUpdateId,
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate.StatusDate,
            ActualStart = update.ActualStart ?? statusDate.StatusDate,
            ActualFinish = update.ActualFinish,
            PercentComplete = update.PercentComplete,
            PhysicalProgress = update.PhysicalProgress,
            FinancialProgress = update.FinancialProgress,
            ProgressMethod = ProgressMethod.ByQuantity,
            PlannedQuantity = activity.PlannedQuantity,
            ExecutedQuantity = executedQuantity,
            QuantityUnit = activity.QuantityUnit,
            ActualHours = update.ActualHours,
            ActualCost = update.ActualCost,
            CommittedCost = update.CommittedCost,
            CurrencyCode = update.CurrencyCode,
            ProgressStatus = update.ProgressStatus,
            Comment = update.Comment,
            EvidenceReference = update.EvidenceReference,
            ManualWeight = update.ManualWeight,
            CreatedUtc = update.CreatedUtc,
            CreatedBy = update.CreatedBy,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        return await RegisterProgressAsync(update, actor, ct: ct);
    }

    public async Task<ActivityProgressUpdate> RegisterCostAsync(
        ScheduleId scheduleId, ActivityId activityId, decimal? actualCost, decimal? committedCost, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ActualCostManage, actor, scheduleId, "ProgressUpdate", "RegisterCost");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct)
            ?? throw new InvalidOperationException("Debe existir avance antes de registrar costo.");

        var plannedCost = previous.ActualCost ?? committedCost ?? actualCost;
        var financial = ProgressCalculationEngine.CalculateFinancialFromCosts(actualCost, committedCost, plannedCost, preferCommitted: false);
        if (plannedCost is null || plannedCost <= 0m)
            financial = previous.FinancialProgress;

        var update = new ActivityProgressUpdate
        {
            ProgressUpdateId = previous.ProgressUpdateId,
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate.StatusDate,
            ActualStart = previous.ActualStart,
            ActualFinish = previous.ActualFinish,
            ActualDuration = previous.ActualDuration,
            PercentComplete = previous.PercentComplete,
            PhysicalProgress = previous.PhysicalProgress,
            FinancialProgress = financial,
            ProgressMethod = previous.ProgressMethod,
            PlannedQuantity = previous.PlannedQuantity,
            ExecutedQuantity = previous.ExecutedQuantity,
            QuantityUnit = previous.QuantityUnit,
            ActualHours = previous.ActualHours,
            ActualCost = actualCost ?? previous.ActualCost,
            CommittedCost = committedCost ?? previous.CommittedCost,
            CurrencyCode = previous.CurrencyCode,
            RemainingDuration = previous.RemainingDuration,
            RemainingQuantity = previous.RemainingQuantity,
            Productivity = previous.Productivity,
            PlannedProductivity = previous.PlannedProductivity,
            CrewInfo = previous.CrewInfo,
            ProgressStatus = previous.ProgressStatus,
            DelayReason = previous.DelayReason,
            Comment = previous.Comment,
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
        return await RegisterProgressAsync(update, actor, ct: ct);
    }

    public async Task<DelayReasonEntry> RegisterDelayReasonAsync(DelayReasonEntry entry, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.DelayReasonManage, actor, entry.ScheduleId, "DelayReason", "Register");
        var saved = new DelayReasonEntry
        {
            EntryId = string.IsNullOrWhiteSpace(entry.EntryId) ? Guid.NewGuid().ToString("N") : entry.EntryId,
            ScheduleId = entry.ScheduleId,
            ActivityId = entry.ActivityId,
            ProgressUpdateId = entry.ProgressUpdateId,
            Code = entry.Code,
            Description = entry.Description ?? string.Empty,
            Responsible = entry.Responsible ?? string.Empty,
            ImpactDays = entry.ImpactDays,
            Comment = entry.Comment ?? string.Empty,
            EvidenceReference = entry.EvidenceReference ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor
        };
        await _delayRepo.SaveAsync(saved, ct);
        await AuditAsync(entry.ScheduleId, "DelayReason", saved.EntryId, "RegisterDelayReason", actor, null, saved.Code.ToString(), saved.Comment, "Ok", ct);
        return saved;
    }

    public async Task<ActivityProgressUpdate> RecalculateRemainingAsync(
        ScheduleId scheduleId, ActivityId activityId, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressUpdate, actor, scheduleId, "ProgressUpdate", "RecalculateRemaining");
        var statusDate = await RequireStatusDateAsync(scheduleId, ct);
        var activity = await _activityRepo.GetByIdAsync(activityId, ct)
            ?? throw new InvalidOperationException("Actividad no encontrada.");
        var previous = await _progressRepo.GetByActivityAsync(scheduleId, activityId, ct)
            ?? throw new InvalidOperationException("Sin avance para recalcular.");

        var remaining = RemainingDurationCalculator.CalculateRemaining(activity, previous, statusDate.StatusDate);
        var update = new ActivityProgressUpdate
        {
            ProgressUpdateId = previous.ProgressUpdateId,
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate.StatusDate,
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
            RemainingDuration = remaining,
            RemainingQuantity = previous.RemainingQuantity,
            Productivity = previous.Productivity,
            PlannedProductivity = previous.PlannedProductivity,
            CrewInfo = previous.CrewInfo,
            ProgressStatus = previous.ProgressStatus,
            DelayReason = previous.DelayReason,
            Comment = previous.Comment,
            EvidenceReference = previous.EvidenceReference,
            ManualWeight = previous.ManualWeight,
            CreatedUtc = previous.CreatedUtc,
            CreatedBy = previous.CreatedBy,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        return await RegisterProgressAsync(update, actor, ct: ct);
    }
}
