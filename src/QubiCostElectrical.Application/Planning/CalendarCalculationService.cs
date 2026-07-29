using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QubiCostElectrical;

public sealed class CalendarCalculationService : ICalendarCalculationService
{
    public bool IsWorkingTime(DateTime dateTime, Calendar calendar)
    {
        var exception = calendar.Exceptions.FirstOrDefault(e => e.Date.Date == dateTime.Date);
        if (exception != null)
        {
            if (exception.Type == CalendarExceptionType.Holiday || exception.Type == CalendarExceptionType.NonWorkingDay || exception.Type == CalendarExceptionType.TemporaryClosure)
                return false;
            if (exception.SpecialShifts != null && exception.SpecialShifts.Count > 0)
                return exception.SpecialShifts.Any(s => TimeOnly.FromDateTime(dateTime) >= s.StartTime && TimeOnly.FromDateTime(dateTime) < s.EndTime);
        }

        var dayOfWeek = dateTime.DayOfWeek;
        var weeklyDay = calendar.WeeklyPattern.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
        if (weeklyDay == null || !weeklyDay.IsWorkingDay)
            return false;

        var timeOfDay = TimeOnly.FromDateTime(dateTime);
        return weeklyDay.Shifts.Any(s => timeOfDay >= s.StartTime && timeOfDay < s.EndTime);
    }

    public DateTime GetNextWorkingInstant(DateTime dateTime, Calendar calendar)
    {
        var current = dateTime;
        int maxIterations = 365 * 5;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            if (IsWorkingTime(current, calendar))
                return current;

            var exception = calendar.Exceptions.FirstOrDefault(e => e.Date.Date == current.Date);
            if (exception != null && (exception.Type == CalendarExceptionType.Holiday || exception.Type == CalendarExceptionType.NonWorkingDay || exception.Type == CalendarExceptionType.TemporaryClosure))
            {
                current = current.Date.AddDays(1);
                iteration++;
                continue;
            }

            var dayOfWeek = current.DayOfWeek;
            var weeklyDay = calendar.WeeklyPattern.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
            if (weeklyDay != null && weeklyDay.IsWorkingDay && weeklyDay.Shifts.Count > 0)
            {
                var nextShift = weeklyDay.Shifts
                    .Where(s => TimeOnly.FromDateTime(current) < s.EndTime)
                    .OrderBy(s => s.StartTime)
                    .FirstOrDefault();
                if (nextShift != null)
                {
                    current = current.Date + nextShift.StartTime.ToTimeSpan();
                    continue;
                }
            }
            current = current.Date.AddDays(1);
            iteration++;
        }
        return current;
    }

    public DateTime GetPreviousWorkingInstant(DateTime dateTime, Calendar calendar)
    {
        var current = dateTime;
        int maxIterations = 365 * 5;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            if (IsWorkingTime(current, calendar))
                return current;

            current = current.AddMinutes(-1);
            iteration++;
        }
        return current;
    }

    public DateTime AddDuration(DateTime start, PlanningDuration duration, Calendar calendar)
    {
        if (duration.Unit == DurationUnit.Minutes || duration.Unit == DurationUnit.Hours)
        {
            var totalMinutes = duration.ToMinutes().Value;
            var current = start;
            double remaining = totalMinutes;

            while (remaining > 0)
            {
                if (!IsWorkingTime(current, calendar))
                {
                    current = GetNextWorkingInstant(current, calendar);
                    continue;
                }

                var dayOfWeek = current.DayOfWeek;
                var weeklyDay = calendar.WeeklyPattern.FirstOrDefault(w => w.DayOfWeek == dayOfWeek);
                var exception = calendar.Exceptions.FirstOrDefault(e => e.Date.Date == current.Date);

                double minutesInCurrentSlot = 0;
                if (exception?.SpecialShifts != null && exception.SpecialShifts.Count > 0)
                {
                    var slot = exception.SpecialShifts.FirstOrDefault(s => TimeOnly.FromDateTime(current) >= s.StartTime && TimeOnly.FromDateTime(current) < s.EndTime);
                    if (slot != null)
                        minutesInCurrentSlot = (slot.EndTime - TimeOnly.FromDateTime(current)).TotalMinutes;
                }
                else if (weeklyDay != null)
                {
                    var slot = weeklyDay.Shifts.FirstOrDefault(s => TimeOnly.FromDateTime(current) >= s.StartTime && TimeOnly.FromDateTime(current) < s.EndTime);
                    if (slot != null)
                        minutesInCurrentSlot = (slot.EndTime - TimeOnly.FromDateTime(current)).TotalMinutes;
                }

                if (minutesInCurrentSlot <= 0)
                {
                    current = GetNextWorkingInstant(current.AddMinutes(1), calendar);
                    continue;
                }

                var take = Math.Min(remaining, minutesInCurrentSlot);
                current = current.AddMinutes(take);
                remaining -= take;
            }
            return current;
        }
        else
        {
            var workDays = (int)duration.ToWorkDays().Value;
            var current = start.Date;
            int daysAdded = 0;

            while (daysAdded < workDays)
            {
                current = current.AddDays(1);
                if (IsWorkingDay(current, calendar))
                    daysAdded++;
            }
            return current + (start - start.Date);
        }
    }

    public PlanningDuration SubtractDates(DateTime start, DateTime end, Calendar calendar)
    {
        double totalMinutes = 0;
        var current = start;

        while (current < end)
        {
            if (IsWorkingTime(current, calendar))
                totalMinutes++;
            current = current.AddMinutes(1);
        }
        return new PlanningDuration(totalMinutes, DurationUnit.Minutes);
    }

    public double GetWorkingHoursForDay(DateTime date, Calendar calendar)
    {
        var exception = calendar.Exceptions.FirstOrDefault(e => e.Date.Date == date.Date);
        if (exception != null)
        {
            if (exception.Type == CalendarExceptionType.Holiday || exception.Type == CalendarExceptionType.NonWorkingDay || exception.Type == CalendarExceptionType.TemporaryClosure)
                return 0;
            if (exception.SpecialShifts != null && exception.SpecialShifts.Count > 0)
                return exception.SpecialShifts.Sum(s => s.TotalHours);
        }

        var weeklyDay = calendar.WeeklyPattern.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);
        if (weeklyDay == null || !weeklyDay.IsWorkingDay)
            return 0;

        return weeklyDay.Shifts.Sum(s => s.TotalHours);
    }

    public List<DateTime> GetWorkingDaysBetween(DateTime start, DateTime end, Calendar calendar)
    {
        var result = new List<DateTime>();
        var current = start.Date;
        while (current <= end.Date)
        {
            if (IsWorkingDay(current, calendar))
                result.Add(current);
            current = current.AddDays(1);
        }
        return result;
    }

    private bool IsWorkingDay(DateTime date, Calendar calendar)
    {
        var exception = calendar.Exceptions.FirstOrDefault(e => e.Date.Date == date.Date);
        if (exception != null)
        {
            if (exception.Type == CalendarExceptionType.Holiday || exception.Type == CalendarExceptionType.NonWorkingDay || exception.Type == CalendarExceptionType.TemporaryClosure)
                return false;
            if (exception.SpecialShifts != null && exception.SpecialShifts.Count > 0)
                return exception.SpecialShifts.Sum(s => s.TotalHours) > 0;
        }

        var weeklyDay = calendar.WeeklyPattern.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);
        return weeklyDay?.IsWorkingDay == true;
    }
}
