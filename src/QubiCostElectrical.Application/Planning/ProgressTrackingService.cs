using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ProgressTrackingService : IProgressTrackingService
{
    private readonly IStatusDateRepository _statusDateRepo;
    private readonly IActivityProgressRepository _progressRepo;
    private readonly IProgressIntervalRepository _intervalRepo;
    private readonly IDelayReasonRepository _delayRepo;
    private readonly IProgressEvidenceRepository _evidenceRepo;
    private readonly IProgressSnapshotRepository _snapshotRepo;
    private readonly IScheduleRepository _scheduleRepo;
    private readonly IActivityRepository _activityRepo;
    private readonly IActivityDependencyRepository _dependencyRepo;
    private readonly IPlanningAuditRepository _auditRepo;
    private readonly ICpmCalculationService? _cpm;
    private readonly IPlanningPermissionChecker _permissions;
    private readonly ProgressPolicy _policy;

    public ProgressTrackingService(
        IStatusDateRepository statusDateRepo,
        IActivityProgressRepository progressRepo,
        IProgressIntervalRepository intervalRepo,
        IDelayReasonRepository delayRepo,
        IProgressEvidenceRepository evidenceRepo,
        IProgressSnapshotRepository snapshotRepo,
        IScheduleRepository scheduleRepo,
        IActivityRepository activityRepo,
        IActivityDependencyRepository dependencyRepo,
        IPlanningAuditRepository auditRepo,
        IPlanningPermissionChecker permissions,
        ProgressPolicy? policy = null,
        ICpmCalculationService? cpm = null)
    {
        _statusDateRepo = statusDateRepo ?? throw new ArgumentNullException(nameof(statusDateRepo));
        _progressRepo = progressRepo ?? throw new ArgumentNullException(nameof(progressRepo));
        _intervalRepo = intervalRepo ?? throw new ArgumentNullException(nameof(intervalRepo));
        _delayRepo = delayRepo ?? throw new ArgumentNullException(nameof(delayRepo));
        _evidenceRepo = evidenceRepo ?? throw new ArgumentNullException(nameof(evidenceRepo));
        _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
        _scheduleRepo = scheduleRepo ?? throw new ArgumentNullException(nameof(scheduleRepo));
        _activityRepo = activityRepo ?? throw new ArgumentNullException(nameof(activityRepo));
        _dependencyRepo = dependencyRepo ?? throw new ArgumentNullException(nameof(dependencyRepo));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _policy = policy ?? ProgressPolicy.Default;
        _cpm = cpm;
    }

    public async Task<ScheduleStatusDate> SetStatusDateAsync(
        ScheduleId scheduleId, DateTime statusDate, string timeZoneId, string comment, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.StatusDateManage, actor, scheduleId, "StatusDate", "SetStatusDate");
        _ = await _scheduleRepo.GetByIdAsync(scheduleId, ct) ?? throw new InvalidOperationException("Cronograma no encontrado.");

        var existing = await _statusDateRepo.GetByScheduleAsync(scheduleId, ct);
        var entity = new ScheduleStatusDate
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            StatusDate = DateTime.SpecifyKind(statusDate, DateTimeKind.Unspecified),
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId,
            UpdatedUtc = DateTime.UtcNow,
            UpdatedBy = actor,
            Comment = comment ?? string.Empty,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _statusDateRepo.SaveAsync(entity, ct);
        await AuditAsync(scheduleId, "StatusDate", entity.Id, "SetStatusDate", actor,
            existing?.StatusDate.ToString("O"), entity.StatusDate.ToString("O"), comment, "Ok", ct);
        return entity;
    }

    public Task<ScheduleStatusDate?> GetStatusDateAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressView, "system", scheduleId, "StatusDate", "GetStatusDate");
        return _statusDateRepo.GetByScheduleAsync(scheduleId, ct);
    }

    public async Task<ActivityProgressUpdate> RegisterProgressAsync(
        ActivityProgressUpdate update, string actor, bool allowPercentDecrease = false, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressUpdate, actor, update.ScheduleId, "ProgressUpdate", "RegisterProgress");
        var activity = await _activityRepo.GetByIdAsync(update.ActivityId, ct)
            ?? throw new InvalidOperationException("Actividad no encontrada.");

        var previous = await _progressRepo.GetByActivityAsync(update.ScheduleId, update.ActivityId, ct);
        var policy = _policy with { AllowPercentDecreaseWithoutComment = allowPercentDecrease || _policy.AllowPercentDecreaseWithoutComment };

        var enriched = EnrichUpdate(update, activity, previous, actor);
        var issues = ProgressValidation.Validate(enriched, activity, policy, previous);
        if (ProgressValidation.HasErrors(issues))
            throw new InvalidOperationException(string.Join("; ", issues.Where(i => i.IsError).Select(i => i.Message)));

        await _progressRepo.SaveAsync(enriched, ct);
        await AuditAsync(update.ScheduleId, "ProgressUpdate", enriched.ProgressUpdateId, "RegisterProgress", actor,
            previous?.PercentComplete.ToString(), enriched.PercentComplete.ToString(), enriched.Comment, "Ok", ct);
        return enriched;
    }

    public Task<List<ActivityProgressUpdate>> ListProgressAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.ProgressView, "system", scheduleId, "ProgressUpdate", "List");
        return _progressRepo.GetByScheduleAsync(scheduleId, ct);
    }

    private ActivityProgressUpdate EnrichUpdate(
        ActivityProgressUpdate update, Activity activity, ActivityProgressUpdate? previous, string actor)
    {
        var plannedQty = update.PlannedQuantity ?? activity.PlannedQuantity;
        var executed = update.ExecutedQuantity;
        var percent = ProgressCalculationEngine.ResolvePercentComplete(
            update.ProgressMethod, update.PercentComplete,
            activity.PlannedDuration, update.RemainingDuration, plannedQty, executed);

        if (update.ProgressMethod == ProgressMethod.Manual)
            percent = ProgressCalculationEngine.ClampPercent(update.PercentComplete);

        var physical = update.PhysicalProgress;
        if (update.ProgressMethod is ProgressMethod.ByQuantity or ProgressMethod.ByPhysicalUnits)
            physical = ProgressCalculationEngine.CalculatePhysicalByQuantity(plannedQty, executed, _policy.AllowOverProgress);
        else if (physical == 0m && percent > 0m)
            physical = percent;

        var productivity = update.Productivity
            ?? ProgressCalculationEngine.CalculateProductivity(executed, update.ActualHours);

        var temp = new ActivityProgressUpdate
        {
            ProgressUpdateId = string.IsNullOrWhiteSpace(update.ProgressUpdateId)
                ? previous?.ProgressUpdateId ?? Guid.NewGuid().ToString("N")
                : update.ProgressUpdateId,
            ScheduleId = update.ScheduleId,
            ActivityId = update.ActivityId,
            StatusDate = update.StatusDate,
            ActualStart = update.ActualStart,
            ActualFinish = update.ActualFinish,
            ActualDuration = update.ActualDuration ?? RemainingDurationCalculator.CalculateActualConsumed(update, update.StatusDate),
            PercentComplete = percent,
            PhysicalProgress = ProgressCalculationEngine.ClampPercent(physical),
            FinancialProgress = ProgressCalculationEngine.ClampPercent(update.FinancialProgress),
            ProgressMethod = update.ProgressMethod,
            PlannedQuantity = plannedQty,
            ExecutedQuantity = executed,
            QuantityUnit = string.IsNullOrWhiteSpace(update.QuantityUnit) ? activity.QuantityUnit : update.QuantityUnit,
            ActualHours = update.ActualHours,
            ActualCost = update.ActualCost,
            CommittedCost = update.CommittedCost,
            CurrencyCode = string.IsNullOrWhiteSpace(update.CurrencyCode) ? _policy.CurrencyCode : update.CurrencyCode,
            RemainingDuration = update.RemainingDuration,
            RemainingQuantity = ProgressCalculationEngine.RemainingQuantity(plannedQty, executed),
            Productivity = productivity,
            PlannedProductivity = update.PlannedProductivity,
            CrewInfo = update.CrewInfo ?? string.Empty,
            ProgressStatus = update.ProgressStatus,
            DelayReason = update.DelayReason,
            Comment = update.Comment ?? string.Empty,
            EvidenceReference = update.EvidenceReference ?? string.Empty,
            ManualWeight = update.ManualWeight,
            ForecastStart = update.ForecastStart,
            ForecastFinish = update.ForecastFinish,
            CreatedUtc = previous?.CreatedUtc ?? DateTime.UtcNow,
            CreatedBy = previous?.CreatedBy ?? actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

        var remaining = RemainingDurationCalculator.CalculateRemaining(activity, temp, temp.StatusDate);
        var status = ProgressStatusDeriver.Derive(temp, activity, temp.StatusDate,
            isExplicitlySuspended: temp.ProgressStatus == ProgressTrackingStatus.Suspended,
            isExplicitlyBlocked: temp.ProgressStatus == ProgressTrackingStatus.Blocked,
            isExplicitlyCancelled: temp.ProgressStatus == ProgressTrackingStatus.Cancelled);
        var (fs, ff) = ProgressForecastEngine.ForecastActivity(activity, temp, temp.StatusDate);

        return new ActivityProgressUpdate
        {
            ProgressUpdateId = temp.ProgressUpdateId,
            ScheduleId = temp.ScheduleId,
            ActivityId = temp.ActivityId,
            StatusDate = temp.StatusDate,
            ActualStart = temp.ActualStart,
            ActualFinish = temp.ActualFinish,
            ActualDuration = temp.ActualDuration,
            PercentComplete = temp.PercentComplete,
            PhysicalProgress = temp.PhysicalProgress,
            FinancialProgress = temp.FinancialProgress,
            ProgressMethod = temp.ProgressMethod,
            PlannedQuantity = temp.PlannedQuantity,
            ExecutedQuantity = temp.ExecutedQuantity,
            QuantityUnit = temp.QuantityUnit,
            ActualHours = temp.ActualHours,
            ActualCost = temp.ActualCost,
            CommittedCost = temp.CommittedCost,
            CurrencyCode = temp.CurrencyCode,
            RemainingDuration = remaining,
            RemainingQuantity = temp.RemainingQuantity,
            Productivity = temp.Productivity,
            PlannedProductivity = temp.PlannedProductivity,
            CrewInfo = temp.CrewInfo,
            ProgressStatus = status,
            DelayReason = temp.DelayReason,
            Comment = temp.Comment,
            EvidenceReference = temp.EvidenceReference,
            ManualWeight = temp.ManualWeight,
            ForecastStart = fs,
            ForecastFinish = ff,
            CreatedUtc = temp.CreatedUtc,
            CreatedBy = temp.CreatedBy,
            ModifiedUtc = temp.ModifiedUtc,
            ModifiedBy = temp.ModifiedBy,
            RowVersion = temp.RowVersion
        };
    }
}
