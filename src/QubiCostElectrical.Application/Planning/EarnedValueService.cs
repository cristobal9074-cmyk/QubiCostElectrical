using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class EarnedValueService : IEarnedValueService
{
    private readonly IEarnedValueRepository _latestRepo;
    private readonly IEarnedValueSnapshotRepository _snapshotRepo;
    private readonly IEarnedValueHistoryRepository _historyRepo;
    private readonly IScheduleRepository _scheduleRepo;
    private readonly IActivityRepository _activityRepo;
    private readonly IActivityProgressRepository _progressRepo;
    private readonly IStatusDateRepository _statusDateRepo;
    private readonly IScheduleBaselineRepository _baselineRepo;
    private readonly IProgressTrackingService? _progressTracking;
    private readonly IPlanningAuditRepository _auditRepo;
    private readonly IPlanningPermissionChecker _permissions;

    public EarnedValueService(
        IEarnedValueRepository latestRepo,
        IEarnedValueSnapshotRepository snapshotRepo,
        IEarnedValueHistoryRepository historyRepo,
        IScheduleRepository scheduleRepo,
        IActivityRepository activityRepo,
        IActivityProgressRepository progressRepo,
        IStatusDateRepository statusDateRepo,
        IScheduleBaselineRepository baselineRepo,
        IPlanningAuditRepository auditRepo,
        IPlanningPermissionChecker permissions,
        IProgressTrackingService? progressTracking = null)
    {
        _latestRepo = latestRepo ?? throw new ArgumentNullException(nameof(latestRepo));
        _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
        _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
        _scheduleRepo = scheduleRepo ?? throw new ArgumentNullException(nameof(scheduleRepo));
        _activityRepo = activityRepo ?? throw new ArgumentNullException(nameof(activityRepo));
        _progressRepo = progressRepo ?? throw new ArgumentNullException(nameof(progressRepo));
        _statusDateRepo = statusDateRepo ?? throw new ArgumentNullException(nameof(statusDateRepo));
        _baselineRepo = baselineRepo ?? throw new ArgumentNullException(nameof(baselineRepo));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _progressTracking = progressTracking;
    }

    public async Task<EarnedValueResult> CalculateAsync(
        ScheduleId scheduleId, string actor, decimal? projectBac = null, EacMethod? eacMethod = null, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueCalculate, actor, scheduleId, "EarnedValue", "Calculate");
        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct)
            ?? throw new InvalidOperationException("Cronograma no encontrado.");
        var status = await _statusDateRepo.GetByScheduleAsync(scheduleId, ct)
            ?? throw new InvalidOperationException("Debe existir fecha de estado para calcular EVM.");

        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);

        var baselines = await _baselineRepo.GetByScheduleAsync(scheduleId, ct);
        var hasOfficial = baselines.Any(b => b.IsOfficial);
        DateTime? baselineFinish = schedule.TargetEndDate;

        DateTime? forecastFinish = null;
        if (_progressTracking is not null)
        {
            try
            {
                var summary = await _progressTracking.GetSummaryAsync(scheduleId, ct);
                forecastFinish = summary.ForecastProjectFinish;
            }
            catch { /* sin resumen aún */ }
        }

        var input = EarnedValueInputBuilder.Build(
            schedule, status.StatusDate, activities, progressMap,
            projectBac, baselineFinish, forecastFinish, hasOfficial,
            EarnedValueMethod.PercentComplete, eacMethod ?? EacMethod.BacOverCpi);

        if (!input.HasStatusDate)
            input = CloneWithFlags(input, hasStatusDate: false);

        var result = EarnedValueEngine.Calculate(input, DateTime.UtcNow);
        await _latestRepo.SaveLatestAsync(result, ct);
        await _historyRepo.AppendAsync(new EarnedValueHistoryEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            StatusDate = result.StatusDate,
            RecordedUtc = DateTime.UtcNow,
            RecordedBy = actor,
            Pv = result.Indicators.Pv,
            Ev = result.Indicators.Ev,
            Ac = result.Indicators.Ac,
            Bac = result.Indicators.Bac,
            Spi = result.Indicators.Spi,
            Cpi = result.Indicators.Cpi,
            Tcpi = result.Indicators.Tcpi,
            Eac = result.Indicators.Eac,
            Source = "Calculate"
        }, ct);

        await AuditAsync(scheduleId, "EarnedValue", scheduleId.Value, "Calculate", actor,
            null, $"SPI={result.Indicators.Spi};CPI={result.Indicators.Cpi}", null, result.IsValid ? "Ok" : "Invalid", ct);
        return result;
    }

    public async Task<EarnedValueSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueSnapshotCreate, actor, scheduleId, "EarnedValueSnapshot", "Create");
        var latest = await _latestRepo.GetLatestAsync(scheduleId, ct)
            ?? await CalculateAsync(scheduleId, actor, ct: ct);

        var version = await _snapshotRepo.GetNextVersionAsync(scheduleId, ct);
        var draft = new EarnedValueSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            Version = version,
            StatusDate = latest.StatusDate,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            Comment = comment ?? string.Empty,
            SHA256 = string.Empty,
            Result = latest
        };
        var hash = EarnedValueSnapshotHasher.ComputeHash(draft);
        var snap = new EarnedValueSnapshot
        {
            SnapshotId = draft.SnapshotId,
            ScheduleId = scheduleId,
            Version = version,
            StatusDate = draft.StatusDate,
            CreatedUtc = draft.CreatedUtc,
            CreatedBy = actor,
            Comment = draft.Comment,
            SHA256 = hash,
            Result = latest
        };
        await _snapshotRepo.SaveAsync(snap, ct);
        await AuditAsync(scheduleId, "EarnedValueSnapshot", snap.SnapshotId, "CreateSnapshot", actor, null, $"v{version}", comment, "Ok", ct);
        return snap;
    }

    public async Task<EarnedValueComparison> CompareSnapshotsAsync(string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct = default)
    {
        var a = await _snapshotRepo.GetByIdAsync(snapshotIdA, ct) ?? throw new InvalidOperationException("Snapshot A no encontrado.");
        Ensure(PlanningPermissions.EarnedValueCompare, actor, a.ScheduleId, "EarnedValueSnapshot", "Compare");
        var b = await _snapshotRepo.GetByIdAsync(snapshotIdB, ct) ?? throw new InvalidOperationException("Snapshot B no encontrado.");
        var cmp = EarnedValueComparisonEngine.Compare($"EVM v{a.Version}", $"EVM v{b.Version}", a.Result, b.Result);
        await AuditAsync(a.ScheduleId, "EarnedValueSnapshot", snapshotIdA, "Compare", actor, snapshotIdA, snapshotIdB, null, "Ok", ct);
        return cmp;
    }

    public Task<EarnedValueComparison> CompareResultsAsync(
        EarnedValueResult left, EarnedValueResult right, string leftLabel, string rightLabel, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueCompare, actor, left.ScheduleId, "EarnedValue", "CompareResults");
        return Task.FromResult(EarnedValueComparisonEngine.Compare(leftLabel, rightLabel, left, right));
    }

    public async Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken ct = default)
    {
        var snap = await _snapshotRepo.GetByIdAsync(snapshotId, ct);
        if (snap is null) return false;
        Ensure(PlanningPermissions.EarnedValueAuditView, "system", snap.ScheduleId, "EarnedValueSnapshot", "Verify");
        return EarnedValueSnapshotHasher.Verify(snap);
    }

    public Task<List<EarnedValueSnapshot>> ListSnapshotsAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueView, "system", scheduleId, "EarnedValueSnapshot", "List");
        return _snapshotRepo.GetByScheduleAsync(scheduleId, ct);
    }

    public Task<List<EarnedValueHistoryEntry>> GetHistoryAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueView, "system", scheduleId, "EarnedValueHistory", "List");
        return _historyRepo.GetByScheduleAsync(scheduleId, limit, ct);
    }

    public Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.EarnedValueView, "system", scheduleId, "EarnedValue", "GetLatest");
        return _latestRepo.GetLatestAsync(scheduleId, ct);
    }

    private static EarnedValueInput CloneWithFlags(EarnedValueInput input, bool hasStatusDate) =>
        new()
        {
            ScheduleId = input.ScheduleId,
            StatusDate = input.StatusDate,
            CurrencyCode = input.CurrencyCode,
            Bac = input.Bac,
            Pv = input.Pv,
            Ev = input.Ev,
            Ac = input.Ac,
            PercentPlanned = input.PercentPlanned,
            PercentEarned = input.PercentEarned,
            PercentActual = input.PercentActual,
            EvMethod = input.EvMethod,
            EacMethod = input.EacMethod,
            BaselineFinish = input.BaselineFinish,
            ForecastFinish = input.ForecastFinish,
            PlannedFinish = input.PlannedFinish,
            HasBaseline = input.HasBaseline,
            HasStatusDate = hasStatusDate,
            HasBudget = input.HasBudget,
            HasProgress = input.HasProgress,
            IsProjectFinished = input.IsProjectFinished,
            IsProjectCancelled = input.IsProjectCancelled,
            IsProjectSuspended = input.IsProjectSuspended,
            Activities = input.Activities
        };

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
}
