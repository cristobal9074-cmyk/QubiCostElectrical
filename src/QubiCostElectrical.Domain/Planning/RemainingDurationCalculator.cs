using System;

namespace QubiCostElectrical;

/// <summary>Cálculo de duración real consumida, restante y estimada al término.</summary>
public static class RemainingDurationCalculator
{
    public static PlanningDuration CalculateRemaining(
        Activity activity,
        ActivityProgressUpdate update,
        DateTime statusDate)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(update);

        if (update.ProgressStatus == ProgressTrackingStatus.Completed ||
            update.PercentComplete >= 100m ||
            update.ActualFinish.HasValue)
            return new PlanningDuration(0, activity.PlannedDuration.Unit);

        if (update.RemainingDuration is not null)
        {
            if (update.RemainingDuration.Value < 0)
                return new PlanningDuration(0, update.RemainingDuration.Unit);
            return update.RemainingDuration;
        }

        if (!update.ActualStart.HasValue)
            return activity.PlannedDuration;

        // En progreso: restante = planificado * (1 - pct/100)
        var planned = activity.PlannedDuration;
        var factor = 1m - (update.PercentComplete / 100m);
        if (factor < 0m) factor = 0m;
        var remainingValue = planned.Value * (double)factor;
        if (remainingValue < 0) remainingValue = 0;
        return new PlanningDuration(remainingValue, planned.Unit);
    }

    public static PlanningDuration? CalculateActualConsumed(
        ActivityProgressUpdate update,
        DateTime statusDate,
        double workHoursPerDay = 8)
    {
        if (!update.ActualStart.HasValue) return null;
        var end = update.ActualFinish ?? statusDate;
        if (end < update.ActualStart.Value) return new PlanningDuration(0, DurationUnit.WorkDays);
        var days = (end.Date - update.ActualStart.Value.Date).TotalDays;
        if (days < 0) days = 0;
        return new PlanningDuration(days, DurationUnit.WorkDays);
    }

    public static PlanningDuration EstimateAtCompletion(
        PlanningDuration? actualConsumed,
        PlanningDuration remaining)
    {
        if (actualConsumed is null) return remaining;
        return actualConsumed + remaining;
    }
}
