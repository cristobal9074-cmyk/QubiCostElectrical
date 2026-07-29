using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CpmCalculationService : ICpmCalculationService, ICpmCalendarCalculator
{
    private readonly IScheduleRepository scheduleRepo;
    private readonly IActivityRepository activityRepo;
    private readonly IActivityDependencyRepository dependencyRepo;
    private readonly ICalendarRepository calendarRepo;
    private readonly ICalendarCalculationService calendarCalc;
    private readonly IPlanningAuditRepository auditRepo;

    private Dictionary<CalendarId, Calendar> loadedCalendars = new();
    private Calendar defaultCalendar = null!;

    public CpmCalculationService(
        IScheduleRepository scheduleRepo,
        IActivityRepository activityRepo,
        IActivityDependencyRepository dependencyRepo,
        ICalendarRepository calendarRepo,
        ICalendarCalculationService calendarCalc,
        IPlanningAuditRepository auditRepo)
    {
        this.scheduleRepo = scheduleRepo;
        this.activityRepo = activityRepo;
        this.dependencyRepo = dependencyRepo;
        this.calendarRepo = calendarRepo;
        this.calendarCalc = calendarCalc;
        this.auditRepo = auditRepo;
    }

    public async Task<CpmResult> CalculateScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule == null)
            throw new InvalidOperationException("Schedule not found");

        if (schedule.Status == ScheduleStatus.Approved || schedule.Status == ScheduleStatus.Archived)
        {
            return new CpmResult
            {
                Success = false,
                ErrorMessage = "No se puede calcular un cronograma aprobado o archivado."
            };
        }

        var activities = await activityRepo.GetByScheduleAsync(scheduleId, ct);
        var dependencies = await dependencyRepo.GetByScheduleAsync(scheduleId, ct);
        var calendars = await calendarRepo.GetAllAsync(ct);

        loadedCalendars = calendars.ToDictionary(c => c.Id);
        if (loadedCalendars.TryGetValue(schedule.DefaultCalendarId, out var defCal))
        {
            defaultCalendar = defCal;
        }
        else
        {
            defaultCalendar = calendars.FirstOrDefault() ?? throw new InvalidOperationException("No calendars defined in the database");
        }

        var engine = new CpmCalculationEngine();
        var result = engine.Calculate(
            schedule.ProjectStartDate ?? DateTime.UtcNow,
            schedule.TargetEndDate,
            activities,
            dependencies,
            this,
            ct);

        if (result.Success)
        {
            // Save computed dates back to database
            foreach (var act in activities)
            {
                if (result.CalculatedDates.TryGetValue(act.Id, out var dates))
                {
                    act.EarlyStart = dates.EarlyStart;
                    act.EarlyFinish = dates.EarlyFinish;
                    act.LateStart = dates.LateStart;
                    act.LateFinish = dates.LateFinish;
                    act.TotalFloat = dates.TotalFloat;
                    act.FreeFloat = dates.FreeFloat;
                    act.IsCritical = dates.IsCritical;
                    act.PlannedStart = dates.EarlyStart;
                    act.PlannedFinish = dates.EarlyFinish;
                    await activityRepo.SaveAsync(act, ct);
                }
            }

            await auditRepo.RecordAsync(new PlanningAuditEvent
            {
                Actor = "System",
                ScheduleId = scheduleId.ToString(),
                EntityType = "Schedule",
                EntityId = scheduleId.ToString(),
                Action = "CpmCalculate",
                Result = "Success"
            }, ct);
        }
        else
        {
            await auditRepo.RecordAsync(new PlanningAuditEvent
            {
                Actor = "System",
                ScheduleId = scheduleId.ToString(),
                EntityType = "Schedule",
                EntityId = scheduleId.ToString(),
                Action = "CpmCalculate",
                Result = "Failed",
                RelevantValues = result.ErrorMessage
            }, ct);
        }

        return result;
    }

    private Calendar GetCalendar(CalendarId? calendarId)
    {
        if (calendarId != null && loadedCalendars.TryGetValue(calendarId, out var cal))
            return cal;
        return defaultCalendar;
    }

    // ICpmCalendarCalculator implementation
    public DateTime AddDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId)
    {
        var calendar = GetCalendar(calendarId);
        return calendarCalc.AddDuration(start, duration, calendar);
    }

    public DateTime SubtractDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId)
    {
        var calendar = GetCalendar(calendarId);
        if (duration.Unit == DurationUnit.Minutes || duration.Unit == DurationUnit.Hours)
        {
            var totalMinutes = duration.ToMinutes().Value;
            var current = start;
            double remaining = totalMinutes;

            while (remaining > 0)
            {
                if (!calendarCalc.IsWorkingTime(current, calendar))
                {
                    current = calendarCalc.GetPreviousWorkingInstant(current, calendar);
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
                        minutesInCurrentSlot = (TimeOnly.FromDateTime(current) - slot.StartTime).TotalMinutes;
                }
                else if (weeklyDay != null)
                {
                    var slot = weeklyDay.Shifts.FirstOrDefault(s => TimeOnly.FromDateTime(current) >= s.StartTime && TimeOnly.FromDateTime(current) < s.EndTime);
                    if (slot != null)
                        minutesInCurrentSlot = (TimeOnly.FromDateTime(current) - slot.StartTime).TotalMinutes;
                }

                if (minutesInCurrentSlot <= 0)
                {
                    current = calendarCalc.GetPreviousWorkingInstant(current.AddMinutes(-1), calendar);
                    continue;
                }

                var take = Math.Min(remaining, minutesInCurrentSlot);
                current = current.AddMinutes(-take);
                remaining -= take;
            }
            return current;
        }
        else
        {
            var workDays = (int)duration.ToWorkDays().Value;
            var current = start.Date;
            int daysSubtracted = 0;

            while (daysSubtracted < workDays)
            {
                current = current.AddDays(-1);
                if (calendarCalc.GetWorkingHoursForDay(current, calendar) > 0)
                    daysSubtracted++;
            }
            return current + (start - start.Date);
        }
    }

    public PlanningDuration SubtractDates(DateTime start, DateTime end, CalendarId? calendarId)
    {
        var calendar = GetCalendar(calendarId);
        return calendarCalc.SubtractDates(start, end, calendar);
    }
}
