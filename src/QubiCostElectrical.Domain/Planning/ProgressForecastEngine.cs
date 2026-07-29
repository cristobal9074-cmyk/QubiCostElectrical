using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Forecast básico de fechas (no EAC/ETC económico).</summary>
public static class ProgressForecastEngine
{
    public static (DateTime? ForecastStart, DateTime? ForecastFinish) ForecastActivity(
        Activity activity,
        ActivityProgressUpdate? progress,
        DateTime statusDate,
        ICpmCalendarCalculator? calendarCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (progress is null || !progress.ActualStart.HasValue)
        {
            var start = activity.PlannedStart ?? activity.EarlyStart;
            if (start.HasValue && start.Value.Date < statusDate.Date)
                start = statusDate;
            var remaining = activity.PlannedDuration;
            var finish = AddRemaining(start, remaining, activity.CalendarId, calendarCalculator);
            return (start, finish);
        }

        if (progress.ActualFinish.HasValue || progress.PercentComplete >= 100m)
            return (progress.ActualStart, progress.ActualFinish ?? progress.ActualStart);

        var forecastStart = progress.ActualStart;
        var rem = RemainingDurationCalculator.CalculateRemaining(activity, progress, statusDate);
        var forecastFinish = AddRemaining(statusDate, rem, activity.CalendarId, calendarCalculator);
        return (forecastStart, forecastFinish);
    }

    public static DateTime? ForecastProjectFinish(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressById,
        DateTime statusDate,
        ICpmCalendarCalculator? calendarCalculator = null)
    {
        DateTime? max = null;
        foreach (var act in activities)
        {
            if (act.Type == ActivityType.Summary) continue;
            progressById.TryGetValue(act.Id.Value, out var prog);
            var (_, finish) = ForecastActivity(act, prog, statusDate, calendarCalculator);
            if (finish.HasValue && (!max.HasValue || finish.Value > max.Value))
                max = finish;
        }
        return max;
    }

    private static DateTime? AddRemaining(
        DateTime? start,
        PlanningDuration remaining,
        CalendarId? calendarId,
        ICpmCalendarCalculator? calculator)
    {
        if (!start.HasValue) return null;
        if (remaining.Value <= 0) return start;
        if (calculator is not null)
            return calculator.AddDuration(start.Value, remaining, calendarId);
        return remaining.Unit switch
        {
            DurationUnit.WorkDays => start.Value.AddDays(remaining.Value),
            DurationUnit.Hours => start.Value.AddHours(remaining.Value),
            DurationUnit.Minutes => start.Value.AddMinutes(remaining.Value),
            DurationUnit.WorkWeeks => start.Value.AddDays(remaining.Value * 7),
            _ => start.Value.AddDays(remaining.Value)
        };
    }
}
