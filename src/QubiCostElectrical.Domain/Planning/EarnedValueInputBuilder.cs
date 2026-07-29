using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Construye <see cref="EarnedValueInput"/> a partir de datos existentes (no muta nada).
/// </summary>
public static class EarnedValueInputBuilder
{
    public static EarnedValueInput Build(
        Schedule schedule,
        DateTime statusDate,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressById,
        decimal? projectBac = null,
        DateTime? baselineFinish = null,
        DateTime? forecastFinish = null,
        bool hasOfficialBaseline = false,
        EarnedValueMethod evMethod = EarnedValueMethod.PercentComplete,
        EacMethod eacMethod = EacMethod.BacOverCpi,
        string currencyCode = "CLP")
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(progressById);

        var work = activities.Where(a => !a.IsDeleted && a.Type != ActivityType.Summary).ToList();
        var weights = work.Select(a =>
        {
            progressById.TryGetValue(a.Id.Value, out var p);
            if (p?.ManualWeight > 0m) return p.ManualWeight;
            if (p?.CommittedCost is > 0m) return p.CommittedCost.Value;
            return (decimal)Math.Max(a.PlannedDuration.ToHours().Value, 0.0001);
        }).ToList();

        var weightSum = weights.Sum();
        if (weightSum <= 0m) weightSum = 1m;

        var totalBac = projectBac is > 0m ? projectBac.Value : weightSum;
        var actInputs = new List<EarnedValueActivityInput>();
        decimal pv = 0m, ev = 0m, ac = 0m;
        bool anyProgress = false;

        for (var i = 0; i < work.Count; i++)
        {
            var act = work[i];
            progressById.TryGetValue(act.Id.Value, out var prog);
            var budget = decimal.Round(totalBac * (weights[i] / weightSum), 2, MidpointRounding.ToEven);
            var pct = prog?.PercentComplete ?? 0m;
            var phy = prog?.PhysicalProgress ?? pct;
            var actPv = EarnedValueEngine.ComputePvAtStatus(
                budget, act.PlannedStart ?? act.EarlyStart, act.PlannedFinish ?? act.EarlyFinish, statusDate);
            var actEv = EarnedValueEngine.ComputeActivityEv(budget, pct, evMethod, phy);
            var actAc = prog?.ActualCost ?? 0m;
            if (prog is not null && (pct > 0m || actAc > 0m || prog.ActualStart.HasValue))
                anyProgress = true;

            pv += actPv;
            ev += actEv;
            ac += actAc;

            actInputs.Add(new EarnedValueActivityInput
            {
                ActivityId = act.Id,
                Code = act.Code,
                Name = act.Name,
                Budget = budget,
                PlannedValueAtStatus = actPv,
                EarnedValueAtStatus = actEv,
                ActualCostAtStatus = actAc,
                PercentComplete = pct,
                PhysicalProgress = phy,
                IsCritical = act.IsCritical
            });
        }

        var finished = schedule.Status is ScheduleStatus.Archived or ScheduleStatus.Obsolete;

        return new EarnedValueInput
        {
            ScheduleId = schedule.Id,
            StatusDate = statusDate,
            CurrencyCode = currencyCode,
            Bac = totalBac,
            Pv = pv,
            Ev = ev,
            Ac = ac,
            PercentPlanned = totalBac > 0m ? pv / totalBac * 100m : 0m,
            PercentEarned = totalBac > 0m ? ev / totalBac * 100m : 0m,
            PercentActual = totalBac > 0m ? ac / totalBac * 100m : 0m,
            EvMethod = evMethod,
            EacMethod = eacMethod,
            BaselineFinish = baselineFinish,
            ForecastFinish = forecastFinish,
            PlannedFinish = schedule.TargetEndDate,
            HasBaseline = hasOfficialBaseline,
            HasStatusDate = true,
            HasBudget = totalBac > 0m,
            HasProgress = anyProgress,
            IsProjectFinished = finished,
            IsProjectCancelled = false,
            IsProjectSuspended = false,
            Activities = actInputs
        };
    }
}
