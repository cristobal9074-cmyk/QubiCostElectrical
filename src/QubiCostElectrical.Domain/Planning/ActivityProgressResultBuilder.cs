using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Construye el resultado estructurado de seguimiento por actividad.</summary>
public static class ActivityProgressResultBuilder
{
    public static ActivityProgressResult Build(
        Activity activity,
        ActivityProgressUpdate? progress,
        DateTime statusDate,
        DateTime? baselineStart,
        DateTime? baselineFinish,
        IReadOnlyList<OutOfSequenceWarning>? warnings = null,
        IReadOnlyList<ProgressValidationIssue>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var status = progress is null
            ? ProgressTrackingStatus.NotStarted
            : ProgressStatusDeriver.Derive(progress, activity, statusDate);

        var (fStart, fFinish) = ProgressForecastEngine.ForecastActivity(activity, progress, statusDate);

        double? startVar = null;
        double? finishVar = null;
        double? durVar = null;

        if (progress?.ActualStart is not null && (baselineStart ?? activity.PlannedStart) is DateTime bs)
            startVar = (progress.ActualStart.Value.Date - bs.Date).TotalDays;

        if (progress?.ActualFinish is not null && (baselineFinish ?? activity.PlannedFinish) is DateTime bf)
            finishVar = (progress.ActualFinish.Value.Date - bf.Date).TotalDays;

        if (progress?.ActualDuration is not null)
            durVar = progress.ActualDuration.ToWorkDays().Value - activity.PlannedDuration.ToWorkDays().Value;

        var delayReasons = new List<DelayReasonCode>();
        if (progress?.DelayReason is DelayReasonCode code)
            delayReasons.Add(code);

        return new ActivityProgressResult
        {
            ActivityId = activity.Id,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            Status = status,
            StatusDate = statusDate,
            BaselineStart = baselineStart,
            BaselineFinish = baselineFinish,
            PlannedStart = activity.PlannedStart,
            PlannedFinish = activity.PlannedFinish,
            ActualStart = progress?.ActualStart,
            ActualFinish = progress?.ActualFinish,
            ForecastStart = progress?.ForecastStart ?? fStart,
            ForecastFinish = progress?.ForecastFinish ?? fFinish,
            PercentComplete = progress?.PercentComplete ?? 0m,
            PhysicalProgress = progress?.PhysicalProgress ?? 0m,
            FinancialProgress = progress?.FinancialProgress ?? 0m,
            StartVarianceDays = startVar,
            FinishVarianceDays = finishVar,
            DurationVarianceDays = durVar,
            RemainingDuration = progress is null
                ? activity.PlannedDuration
                : RemainingDurationCalculator.CalculateRemaining(activity, progress, statusDate),
            ExecutedQuantity = progress?.ExecutedQuantity,
            RemainingQuantity = progress?.RemainingQuantity ??
                ProgressCalculationEngine.RemainingQuantity(progress?.PlannedQuantity ?? activity.PlannedQuantity, progress?.ExecutedQuantity),
            ActualCost = progress?.ActualCost,
            CommittedCost = progress?.CommittedCost,
            Productivity = progress?.Productivity,
            IsCritical = activity.IsCritical,
            DelayReasons = delayReasons,
            Warnings = warnings?.Where(w => w.ActivityId.Value == activity.Id.Value).ToList()
                        ?? (IReadOnlyList<OutOfSequenceWarning>)Array.Empty<OutOfSequenceWarning>(),
            Errors = errors ?? Array.Empty<ProgressValidationIssue>(),
            Explanation = BuildExplanation(status, progress)
        };
    }

    private static string BuildExplanation(ProgressTrackingStatus status, ActivityProgressUpdate? progress)
    {
        if (progress is null) return "Sin actualización de avance.";
        return status switch
        {
            ProgressTrackingStatus.Completed => "Actividad completada con fechas reales registradas.",
            ProgressTrackingStatus.NotStarted => "Actividad no iniciada a la fecha de estado.",
            ProgressTrackingStatus.Delayed => "Avance por debajo de lo esperado a la fecha de estado.",
            ProgressTrackingStatus.Ahead => "Avance adelantado respecto del plan.",
            ProgressTrackingStatus.Suspended => "Actividad suspendida.",
            _ => $"Estado {status}; avance {progress.PercentComplete}%."
        };
    }
}
