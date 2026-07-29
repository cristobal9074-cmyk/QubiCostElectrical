using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ICalendarCalculationService
{
    bool IsWorkingTime(DateTime dateTime, Calendar calendar);
    DateTime GetNextWorkingInstant(DateTime dateTime, Calendar calendar);
    DateTime GetPreviousWorkingInstant(DateTime dateTime, Calendar calendar);
    DateTime AddDuration(DateTime start, PlanningDuration duration, Calendar calendar);
    PlanningDuration SubtractDates(DateTime start, DateTime end, Calendar calendar);
    double GetWorkingHoursForDay(DateTime date, Calendar calendar);
    List<DateTime> GetWorkingDaysBetween(DateTime start, DateTime end, Calendar calendar);
}
