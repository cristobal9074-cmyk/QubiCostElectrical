using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Resumen del cronograma con ponderación explícita (no promedio simple).</summary>
public static class ScheduleProgressSummaryCalculator
{
    public static ScheduleProgressSummary Calculate(
        ScheduleId scheduleId,
        DateTime statusDate,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressById,
        ProgressPolicy policy,
        IReadOnlyList<OutOfSequenceWarning>? oosWarnings = null,
        DateTime? forecastProjectFinish = null)
    {
        ArgumentNullException.ThrowIfNull(scheduleId);
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(progressById);
        ArgumentNullException.ThrowIfNull(policy);

        var work = activities.Where(a => a.Type != ActivityType.Summary && !a.IsDeleted).ToList();
        int notStarted = 0, inProgress = 0, completed = 0, delayed = 0, ahead = 0, blocked = 0, suspended = 0, critical = 0, negFloat = 0;
        decimal actualCost = 0m, committed = 0m;
        double remainingMinutes = 0;

        foreach (var act in work)
        {
            progressById.TryGetValue(act.Id.Value, out var prog);
            var status = prog is null
                ? ProgressTrackingStatus.NotStarted
                : ProgressStatusDeriver.Derive(prog, act, statusDate);

            switch (status)
            {
                case ProgressTrackingStatus.NotStarted: notStarted++; break;
                case ProgressTrackingStatus.Completed: completed++; break;
                case ProgressTrackingStatus.Delayed: delayed++; break;
                case ProgressTrackingStatus.Ahead: ahead++; break;
                case ProgressTrackingStatus.Blocked: blocked++; break;
                case ProgressTrackingStatus.Suspended: suspended++; break;
                case ProgressTrackingStatus.Started:
                case ProgressTrackingStatus.InProgress:
                case ProgressTrackingStatus.OnTrack:
                    inProgress++;
                    break;
            }

            if (act.IsCritical) critical++;
            if (act.TotalFloat.HasValue && act.TotalFloat.Value < 0) negFloat++;

            if (prog is not null)
            {
                actualCost += prog.ActualCost ?? 0m;
                committed += prog.CommittedCost ?? 0m;
                if (prog.RemainingDuration is not null)
                    remainingMinutes += prog.RemainingDuration.ToMinutes().Value;
                else if (status != ProgressTrackingStatus.Completed)
                    remainingMinutes += act.PlannedDuration.ToMinutes().Value;
            }
            else
            {
                remainingMinutes += act.PlannedDuration.ToMinutes().Value;
            }
        }

        var (physical, financial) = WeightedGlobalProgress(work, progressById, policy);

        return new ScheduleProgressSummary
        {
            ScheduleId = scheduleId,
            StatusDate = statusDate,
            TotalActivities = work.Count,
            NotStarted = notStarted,
            InProgress = inProgress,
            Completed = completed,
            Delayed = delayed,
            Ahead = ahead,
            Blocked = blocked,
            Suspended = suspended,
            Critical = critical,
            NegativeFloat = negFloat,
            GlobalPhysicalProgress = physical,
            GlobalFinancialProgress = financial,
            AccumulatedActualCost = actualCost,
            AccumulatedCommittedCost = committed,
            RemainingDuration = new PlanningDuration(remainingMinutes, DurationUnit.Minutes),
            ForecastProjectFinish = forecastProjectFinish,
            WeightingUsed = policy.GlobalWeighting,
            OutOfSequenceWarnings = oosWarnings ?? Array.Empty<OutOfSequenceWarning>()
        };
    }

    public static (decimal Physical, decimal Financial) WeightedGlobalProgress(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressById,
        ProgressPolicy policy)
    {
        decimal sumPhy = 0m, sumFin = 0m, sumW = 0m;
        foreach (var act in activities)
        {
            if (act.Type == ActivityType.Summary || act.IsDeleted) continue;
            progressById.TryGetValue(act.Id.Value, out var prog);
            var w = ResolveWeight(act, prog, policy.GlobalWeighting);
            if (w <= 0m) continue;
            var phy = prog?.PhysicalProgress ?? prog?.PercentComplete ?? 0m;
            var fin = prog?.FinancialProgress ?? 0m;
            sumPhy += phy * w;
            sumFin += fin * w;
            sumW += w;
        }

        if (sumW <= 0m) return (0m, 0m);
        return (
            ProgressCalculationEngine.ClampPercent(sumPhy / sumW),
            ProgressCalculationEngine.ClampPercent(sumFin / sumW));
    }

    private static decimal ResolveWeight(Activity act, ActivityProgressUpdate? prog, GlobalProgressWeighting weighting)
    {
        return weighting switch
        {
            GlobalProgressWeighting.ByDuration => (decimal)Math.Max(act.PlannedDuration.ToHours().Value, 0.0001),
            GlobalProgressWeighting.ByQuantity => Math.Max(act.PlannedQuantity ?? prog?.PlannedQuantity ?? 0m, 0.0001m),
            GlobalProgressWeighting.ByManualWeight => Math.Max(prog?.ManualWeight ?? 1m, 0.0001m),
            GlobalProgressWeighting.ByMilestones => act.Type == ActivityType.Milestone ? 1m : 0.0001m,
            GlobalProgressWeighting.ByPlannedCost => Math.Max(prog?.ActualCost ?? prog?.CommittedCost ?? 1m, 0.0001m),
            _ => 1m
        };
    }
}
