using System;

namespace QubiCostElectrical;

public interface ICpmCalendarCalculator
{
    DateTime AddDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId);
    DateTime SubtractDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId);
    PlanningDuration SubtractDates(DateTime start, DateTime end, CalendarId? calendarId);
}
