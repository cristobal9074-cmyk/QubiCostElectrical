using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Reprogramación explícita del trabajo restante desde fecha de estado.
/// No altera baselines. No es silenciosa: el llamador debe invocarla.
/// </summary>
public static class ProgressRescheduleEngine
{
    public sealed record RescheduleResult(
        IReadOnlyList<Activity> UpdatedActivities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> UpdatedProgress,
        IReadOnlyList<string> ChangedActivityIds);

    public static RescheduleResult RescheduleRemaining(
        DateTime statusDate,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressById,
        ICpmCalendarCalculator? calendarCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(progressById);

        var updatedActs = new List<Activity>();
        var updatedProg = new Dictionary<string, ActivityProgressUpdate>(StringComparer.Ordinal);
        var changed = new List<string>();

        foreach (var act in activities)
        {
            if (act.IsDeleted || act.Type == ActivityType.Summary)
            {
                updatedActs.Add(act);
                if (progressById.TryGetValue(act.Id.Value, out var existing))
                    updatedProg[act.Id.Value] = existing;
                continue;
            }

            progressById.TryGetValue(act.Id.Value, out var prog);

            if (prog is not null && (prog.ActualFinish.HasValue || prog.PercentComplete >= 100m))
            {
                // Completadas: conservar fechas reales; no mover
                updatedActs.Add(act);
                updatedProg[act.Id.Value] = prog;
                continue;
            }

            if (prog is null || !prog.ActualStart.HasValue)
            {
                // No iniciadas: mover inicio planificado a status date si está en el pasado
                var clone = CloneActivity(act);
                var newStart = act.PlannedStart;
                if (!newStart.HasValue || newStart.Value.Date < statusDate.Date)
                    newStart = statusDate;

                var finish = ProgressForecastEngine.ForecastActivity(act, null, statusDate, calendarCalculator).ForecastFinish
                             ?? newStart;
                if (clone.PlannedStart != newStart || clone.PlannedFinish != finish)
                {
                    clone.PlannedStart = newStart;
                    clone.PlannedFinish = finish;
                    changed.Add(act.Id.Value);
                }
                updatedActs.Add(clone);
                if (prog is not null) updatedProg[act.Id.Value] = prog;
                continue;
            }

            // En progreso: conservar ActualStart; recalcular restante y forecast finish
            var remaining = RemainingDurationCalculator.CalculateRemaining(act, prog, statusDate);
            var progForForecast = CloneProgress(prog, statusDate, remaining, null);
            var forecastFinish = ProgressForecastEngine.ForecastActivity(act, progForForecast, statusDate, calendarCalculator).ForecastFinish;

            var actClone = CloneActivity(act);
            actClone.PlannedFinish = forecastFinish;
            if (act.PlannedFinish != forecastFinish)
                changed.Add(act.Id.Value);
            updatedActs.Add(actClone);

            updatedProg[act.Id.Value] = CloneProgress(prog, statusDate, remaining, forecastFinish);
        }

        return new RescheduleResult(updatedActs, updatedProg, changed);
    }

    private static ActivityProgressUpdate CloneProgress(
        ActivityProgressUpdate prog,
        DateTime statusDate,
        PlanningDuration? remaining,
        DateTime? forecastFinish) =>
        new()
        {
            ProgressUpdateId = prog.ProgressUpdateId,
            ScheduleId = prog.ScheduleId,
            ActivityId = prog.ActivityId,
            StatusDate = statusDate,
            ActualStart = prog.ActualStart,
            ActualFinish = prog.ActualFinish,
            ActualDuration = prog.ActualDuration,
            PercentComplete = prog.PercentComplete,
            PhysicalProgress = prog.PhysicalProgress,
            FinancialProgress = prog.FinancialProgress,
            ProgressMethod = prog.ProgressMethod,
            PlannedQuantity = prog.PlannedQuantity,
            ExecutedQuantity = prog.ExecutedQuantity,
            QuantityUnit = prog.QuantityUnit,
            ActualHours = prog.ActualHours,
            ActualCost = prog.ActualCost,
            CommittedCost = prog.CommittedCost,
            CurrencyCode = prog.CurrencyCode,
            RemainingDuration = remaining ?? prog.RemainingDuration,
            RemainingQuantity = prog.RemainingQuantity,
            Productivity = prog.Productivity,
            PlannedProductivity = prog.PlannedProductivity,
            CrewInfo = prog.CrewInfo,
            ProgressStatus = prog.ProgressStatus,
            DelayReason = prog.DelayReason,
            Comment = prog.Comment,
            EvidenceReference = prog.EvidenceReference,
            ManualWeight = prog.ManualWeight,
            ForecastStart = prog.ActualStart,
            ForecastFinish = forecastFinish ?? prog.ForecastFinish,
            CreatedUtc = prog.CreatedUtc,
            CreatedBy = prog.CreatedBy,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = prog.ModifiedBy,
            RowVersion = Guid.NewGuid().ToString("N")
        };

    private static Activity CloneActivity(Activity source)
    {
        return new Activity(source.Id, source.ScheduleId, source.WbsNodeId, source.Code, source.Name, source.Type, source.PlannedDuration, source.Author)
        {
            Description = source.Description,
            CalendarId = source.CalendarId,
            PlannedStart = source.PlannedStart,
            PlannedFinish = source.PlannedFinish,
            MinDate = source.MinDate,
            MaxDate = source.MaxDate,
            Priority = source.Priority,
            Status = source.Status,
            ResponsibleId = source.ResponsibleId,
            PlannedProgressPct = source.PlannedProgressPct,
            PlannedQuantity = source.PlannedQuantity,
            QuantityUnit = source.QuantityUnit,
            Tags = source.Tags,
            Remarks = source.Remarks,
            EarlyStart = source.EarlyStart,
            EarlyFinish = source.EarlyFinish,
            LateStart = source.LateStart,
            LateFinish = source.LateFinish,
            TotalFloat = source.TotalFloat,
            FreeFloat = source.FreeFloat,
            IsCritical = source.IsCritical,
            Deadline = source.Deadline,
            ConstraintType = source.ConstraintType,
            ConstraintDate = source.ConstraintDate,
            LastEditor = source.LastEditor,
            ModifiedAt = source.ModifiedAt,
            RowVersion = source.RowVersion,
            IsDeleted = source.IsDeleted
        };
    }
}
