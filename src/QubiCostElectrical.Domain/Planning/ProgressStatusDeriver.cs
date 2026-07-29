using System;

namespace QubiCostElectrical;

/// <summary>Deriva estado de seguimiento a partir de datos reales y fecha de estado.</summary>
public static class ProgressStatusDeriver
{
    public static ProgressTrackingStatus Derive(
        ActivityProgressUpdate update,
        Activity activity,
        DateTime statusDate,
        bool isExplicitlySuspended = false,
        bool isExplicitlyBlocked = false,
        bool isExplicitlyCancelled = false)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.Type == ActivityType.Summary)
            return ProgressTrackingStatus.NotApplicable;

        if (isExplicitlyCancelled || update.ProgressStatus == ProgressTrackingStatus.Cancelled)
            return ProgressTrackingStatus.Cancelled;

        if (isExplicitlyBlocked || update.ProgressStatus == ProgressTrackingStatus.Blocked)
            return ProgressTrackingStatus.Blocked;

        if (isExplicitlySuspended || update.ProgressStatus == ProgressTrackingStatus.Suspended)
            return ProgressTrackingStatus.Suspended;

        if (update.PercentComplete >= 100m && update.ActualFinish.HasValue)
            return ProgressTrackingStatus.Completed;

        if (activity.Type == ActivityType.Milestone && update.ActualFinish.HasValue)
            return ProgressTrackingStatus.Completed;

        if (!update.ActualStart.HasValue && update.PercentComplete <= 0m)
        {
            if (activity.PlannedStart.HasValue && statusDate.Date > activity.PlannedStart.Value.Date)
                return ProgressTrackingStatus.Delayed;
            return ProgressTrackingStatus.NotStarted;
        }

        if (update.ActualStart.HasValue && update.PercentComplete <= 0m)
            return ProgressTrackingStatus.Started;

        // En progreso: evaluar atraso / adelanto vs plan
        var plannedFinish = activity.PlannedFinish ?? activity.EarlyFinish;
        if (plannedFinish.HasValue && !update.ActualFinish.HasValue)
        {
            if (statusDate.Date > plannedFinish.Value.Date && update.PercentComplete < 100m)
                return ProgressTrackingStatus.Delayed;

            if (activity.PlannedStart.HasValue &&
                update.ActualStart.HasValue &&
                update.ActualStart.Value.Date < activity.PlannedStart.Value.Date &&
                update.PercentComplete > 0m)
                return ProgressTrackingStatus.Ahead;
        }

        if (update.PercentComplete > 0m && update.PercentComplete < 100m)
        {
            if (IsOnTrack(update, activity, statusDate))
                return ProgressTrackingStatus.OnTrack;
            return ProgressTrackingStatus.InProgress;
        }

        return ProgressTrackingStatus.InProgress;
    }

    private static bool IsOnTrack(ActivityProgressUpdate update, Activity activity, DateTime statusDate)
    {
        if (!activity.PlannedStart.HasValue || !activity.PlannedFinish.HasValue)
            return false;
        var span = (activity.PlannedFinish.Value - activity.PlannedStart.Value).TotalDays;
        if (span <= 0) return update.PercentComplete >= 100m;
        var elapsed = (statusDate.Date - activity.PlannedStart.Value.Date).TotalDays;
        if (elapsed < 0) elapsed = 0;
        var expected = (decimal)(elapsed / span) * 100m;
        if (expected > 100m) expected = 100m;
        var delta = update.PercentComplete - expected;
        return delta >= -5m && delta <= 5m;
    }
}
