using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class ScheduleBaselineService : IScheduleBaselineService
{
    private readonly IScheduleBaselineRepository _baselineRepo;
    private readonly IScheduleBaselineSnapshotRepository _snapshotRepo;
    private readonly IScheduleRepository _scheduleRepo;
    private readonly IActivityRepository _activityRepo;
    private readonly IActivityDependencyRepository _dependencyRepo;
    private readonly ICalendarRepository _calendarRepo;
    private readonly IPlanningAuditRepository _auditRepo;

    public ScheduleBaselineService(
        IScheduleBaselineRepository baselineRepo,
        IScheduleBaselineSnapshotRepository snapshotRepo,
        IScheduleRepository scheduleRepo,
        IActivityRepository activityRepo,
        IActivityDependencyRepository dependencyRepo,
        ICalendarRepository calendarRepo,
        IPlanningAuditRepository auditRepo)
    {
        _baselineRepo = baselineRepo ?? throw new ArgumentNullException(nameof(baselineRepo));
        _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
        _scheduleRepo = scheduleRepo ?? throw new ArgumentNullException(nameof(scheduleRepo));
        _activityRepo = activityRepo ?? throw new ArgumentNullException(nameof(activityRepo));
        _dependencyRepo = dependencyRepo ?? throw new ArgumentNullException(nameof(dependencyRepo));
        _calendarRepo = calendarRepo ?? throw new ArgumentNullException(nameof(calendarRepo));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
    }

    public async Task<ScheduleBaseline> CreateBaselineAsync(ScheduleId scheduleId, string name, string description, string createdBy, CancellationToken ct = default)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct) ?? throw new InvalidOperationException("Schedule not found.");
        var activities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var dependencies = await _dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        var calendars = await _calendarRepo.GetAllAsync(ct);

        var snapshot = new ScheduleBaselineSnapshot
        {
            Schedule = schedule,
            Activities = activities,
            Dependencies = dependencies,
            Calendars = calendars
        };

        string sha = BaselineHasher.ComputeHash(snapshot);
        int nextVer = await _baselineRepo.GetNextVersionAsync(scheduleId, ct);

        var baseline = new ScheduleBaseline
        {
            BaselineId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            Version = nextVer,
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            Status = "Draft",
            SHA256 = sha,
            SnapshotDate = DateTime.UtcNow,
            IsOfficial = false
        };

        await _baselineRepo.SaveAsync(baseline, ct);
        await _snapshotRepo.SaveAsync(baseline.BaselineId, snapshot, ct);

        await _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId.Value,
            Action = "CreateBaseline",
            Comment = $"Created baseline version {nextVer} '{name}'",
            Actor = createdBy,
            UtcTimestamp = DateTime.UtcNow
        }, ct);

        return baseline;
    }

    public async Task RestoreBaselineAsync(string baselineId, string restoredBy, CancellationToken ct = default)
    {
        var baseline = await _baselineRepo.GetByIdAsync(baselineId, ct) ?? throw new InvalidOperationException("Baseline not found.");
        
        bool integrity = await VerifyIntegrityAsync(baselineId, ct);
        if (!integrity)
        {
            await _auditRepo.RecordAsync(new PlanningAuditEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                ScheduleId = baseline.ScheduleId.Value,
                Action = "RestoreFailed",
                Comment = $"Integrity validation failed for baseline {baseline.Version}",
                Actor = restoredBy,
                UtcTimestamp = DateTime.UtcNow
            }, ct);
            throw new InvalidOperationException("Baseline integrity verification failed. Snapshot modified or corrupted.");
        }

        var snapshot = await _snapshotRepo.GetByBaselineIdAsync(baselineId, ct) ?? throw new InvalidOperationException("Snapshot not found.");

        // Clear existing activities and dependencies
        var currentActivities = await _activityRepo.GetByScheduleAsync(baseline.ScheduleId, ct);
        foreach (var act in currentActivities)
        {
            await _activityRepo.DeleteAsync(act.Id, softDelete: false, ct);
        }

        var currentDeps = await _dependencyRepo.GetByScheduleAsync(baseline.ScheduleId, ct);
        foreach (var dep in currentDeps)
        {
            await _dependencyRepo.DeleteAsync(dep.Id, ct);
        }

        // Restore schedule metadata
        var sched = snapshot.Schedule;
        await _scheduleRepo.SaveAsync(sched, ct);

        // Restore activities
        foreach (var act in snapshot.Activities)
        {
            await _activityRepo.SaveAsync(act, ct);
        }

        // Restore dependencies
        foreach (var dep in snapshot.Dependencies)
        {
            await _dependencyRepo.SaveAsync(dep, ct);
        }

        await _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = baseline.ScheduleId.Value,
            Action = "RestoreBaseline",
            Comment = $"Restored baseline version {baseline.Version} '{baseline.Name}'",
            Actor = restoredBy,
            UtcTimestamp = DateTime.UtcNow
        }, ct);
    }

    public async Task<ScheduleComparisonResult> CompareBaselinesAsync(string baselineIdA, string baselineIdB, CancellationToken ct = default)
    {
        var snapshotA = await _snapshotRepo.GetByBaselineIdAsync(baselineIdA, ct) ?? throw new InvalidOperationException("Snapshot A not found.");
        var snapshotB = await _snapshotRepo.GetByBaselineIdAsync(baselineIdB, ct) ?? throw new InvalidOperationException("Snapshot B not found.");

        return RunComparison(snapshotA, snapshotB);
    }

    public async Task<ScheduleComparisonResult> CompareWithCurrentAsync(ScheduleId scheduleId, string baselineId, CancellationToken ct = default)
    {
        var snapshotA = await _snapshotRepo.GetByBaselineIdAsync(baselineId, ct) ?? throw new InvalidOperationException("Baseline snapshot not found.");
        
        var currentSched = await _scheduleRepo.GetByIdAsync(scheduleId, ct) ?? throw new InvalidOperationException("Schedule not found.");
        var currentActivities = await _activityRepo.GetByScheduleAsync(scheduleId, ct);
        var currentDeps = await _dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        var currentCals = await _calendarRepo.GetAllAsync(ct);

        var snapshotB = new ScheduleBaselineSnapshot
        {
            Schedule = currentSched,
            Activities = currentActivities,
            Dependencies = currentDeps,
            Calendars = currentCals
        };

        return RunComparison(snapshotA, snapshotB);
    }

    public async Task<bool> VerifyIntegrityAsync(string baselineId, CancellationToken ct = default)
    {
        var baseline = await _baselineRepo.GetByIdAsync(baselineId, ct);
        if (baseline == null) return false;

        var snapshot = await _snapshotRepo.GetByBaselineIdAsync(baselineId, ct);
        if (snapshot == null) return false;

        string currentHash = BaselineHasher.ComputeHash(snapshot);
        bool match = string.Equals(baseline.SHA256, currentHash, StringComparison.OrdinalIgnoreCase);

        if (!match)
        {
            await _auditRepo.RecordAsync(new PlanningAuditEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                ScheduleId = baseline.ScheduleId.Value,
                Action = "IntegrityError",
                Comment = $"Baseline {baseline.Version} integrity compromised. Hash mismatch.",
                Actor = "System",
                UtcTimestamp = DateTime.UtcNow
            }, ct);
        }

        return match;
    }

    public Task<List<ScheduleBaseline>> ListBaselinesAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        return _baselineRepo.GetByScheduleAsync(scheduleId, ct);
    }

    public async Task DeleteDraftBaselineAsync(string baselineId, CancellationToken ct = default)
    {
        var baseline = await _baselineRepo.GetByIdAsync(baselineId, ct) ?? throw new InvalidOperationException("Baseline not found.");
        if (baseline.IsOfficial || baseline.Status == "Official")
        {
            throw new InvalidOperationException("Cannot delete an official baseline.");
        }

        await _baselineRepo.DeleteAsync(baselineId, ct);
        await _snapshotRepo.DeleteAsync(baselineId, ct);

        await _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = baseline.ScheduleId.Value,
            Action = "DeleteBaseline",
            Comment = $"Deleted draft baseline version {baseline.Version} '{baseline.Name}'",
            Actor = "System",
            UtcTimestamp = DateTime.UtcNow
        }, ct);
    }

    public async Task MarkOfficialAsync(string baselineId, CancellationToken ct = default)
    {
        var baseline = await _baselineRepo.GetByIdAsync(baselineId, ct) ?? throw new InvalidOperationException("Baseline not found.");
        var updated = new ScheduleBaseline
        {
            BaselineId = baseline.BaselineId,
            ScheduleId = baseline.ScheduleId,
            Version = baseline.Version,
            Name = baseline.Name,
            Description = baseline.Description,
            CreatedUtc = baseline.CreatedUtc,
            CreatedBy = baseline.CreatedBy,
            Status = "Official",
            SHA256 = baseline.SHA256,
            SnapshotDate = baseline.SnapshotDate,
            IsOfficial = true,
            Notes = baseline.Notes,
            Metadata = baseline.Metadata
        };

        await _baselineRepo.SaveAsync(updated, ct);

        await _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = baseline.ScheduleId.Value,
            Action = "MarkOfficial",
            Comment = $"Baseline version {baseline.Version} '{baseline.Name}' marked as official.",
            Actor = "System",
            UtcTimestamp = DateTime.UtcNow
        }, ct);
    }

    private static ScheduleComparisonResult RunComparison(ScheduleBaselineSnapshot a, ScheduleBaselineSnapshot b)
    {
        var result = new ScheduleComparisonResult();

        var actsA = a.Activities.ToDictionary(act => act.Id);
        var actsB = b.Activities.ToDictionary(act => act.Id);

        // Added / Modified
        foreach (var actB in b.Activities)
        {
            if (!actsA.TryGetValue(actB.Id, out var actA))
            {
                result.AddedActivitiesCount++;
                result.ActivityDifferences.Add(new ActivityDiff
                {
                    ActivityId = actB.Id,
                    Code = actB.Code,
                    Name = actB.Name,
                    DiffType = "Added"
                });
            }
            else
            {
                bool modified = false;
                double? durDiff = null;
                double? startDiff = null;
                double? finishDiff = null;
                string critDiff = "No Change";
                string constDiff = string.Empty;

                if (actA.PlannedDuration.Value != actB.PlannedDuration.Value)
                {
                    modified = true;
                    durDiff = actB.PlannedDuration.Value - actA.PlannedDuration.Value;
                }

                if (actA.EarlyStart != actB.EarlyStart)
                {
                    modified = true;
                    if (actA.EarlyStart.HasValue && actB.EarlyStart.HasValue)
                        startDiff = (actB.EarlyStart.Value - actA.EarlyStart.Value).TotalDays;
                }

                if (actA.EarlyFinish != actB.EarlyFinish)
                {
                    modified = true;
                    if (actA.EarlyFinish.HasValue && actB.EarlyFinish.HasValue)
                        finishDiff = (actB.EarlyFinish.Value - actA.EarlyFinish.Value).TotalDays;
                }

                if (actA.IsCritical != actB.IsCritical)
                {
                    modified = true;
                    critDiff = actB.IsCritical ? "Became Critical" : "No Longer Critical";
                }

                if (actA.ConstraintType != actB.ConstraintType || actA.ConstraintDate != actB.ConstraintDate)
                {
                    modified = true;
                    constDiff = $"Changed from {actA.ConstraintType} to {actB.ConstraintType}";
                }

                if (modified)
                {
                    result.ModifiedActivitiesCount++;
                    result.ActivityDifferences.Add(new ActivityDiff
                    {
                        ActivityId = actB.Id,
                        Code = actB.Code,
                        Name = actB.Name,
                        DiffType = "Modified",
                        DurationDiff = durDiff,
                        StartOffsetDaysDiff = startDiff,
                        FinishOffsetDaysDiff = finishDiff,
                        CriticalDiff = critDiff,
                        ConstraintDiff = constDiff
                    });
                }
            }
        }

        // Deleted
        foreach (var actA in a.Activities)
        {
            if (!actsB.ContainsKey(actA.Id))
            {
                result.DeletedActivitiesCount++;
                result.ActivityDifferences.Add(new ActivityDiff
                {
                    ActivityId = actA.Id,
                    Code = actA.Code,
                    Name = actA.Name,
                    DiffType = "Deleted"
                });
            }
        }

        return result;
    }
}
