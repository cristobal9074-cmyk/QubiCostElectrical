using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class CalendarTests
{
    private static Calendar CreateStandardCalendar()
    {
        var cal = new Calendar(CalendarId.New(), "STD", "Estándar", "America/Santiago", "test", CalendarType.Project);
        cal.WeeklyPattern.Add(new WeeklyWorkDay
        {
            DayOfWeek = DayOfWeek.Monday,
            IsWorkingDay = true,
            Shifts = { new WorkShift(new TimeOnly(8, 0), new TimeOnly(12, 0)), new WorkShift(new TimeOnly(13, 0), new TimeOnly(17, 0)) }
        });
        cal.WeeklyPattern.Add(new WeeklyWorkDay
        {
            DayOfWeek = DayOfWeek.Tuesday,
            IsWorkingDay = true,
            Shifts = { new WorkShift(new TimeOnly(8, 0), new TimeOnly(12, 0)), new WorkShift(new TimeOnly(13, 0), new TimeOnly(17, 0)) }
        });
        cal.WeeklyPattern.Add(new WeeklyWorkDay
        {
            DayOfWeek = DayOfWeek.Wednesday,
            IsWorkingDay = true,
            Shifts = { new WorkShift(new TimeOnly(8, 0), new TimeOnly(12, 0)), new WorkShift(new TimeOnly(13, 0), new TimeOnly(17, 0)) }
        });
        cal.WeeklyPattern.Add(new WeeklyWorkDay
        {
            DayOfWeek = DayOfWeek.Thursday,
            IsWorkingDay = true,
            Shifts = { new WorkShift(new TimeOnly(8, 0), new TimeOnly(12, 0)), new WorkShift(new TimeOnly(13, 0), new TimeOnly(17, 0)) }
        });
        cal.WeeklyPattern.Add(new WeeklyWorkDay
        {
            DayOfWeek = DayOfWeek.Friday,
            IsWorkingDay = true,
            Shifts = { new WorkShift(new TimeOnly(8, 0), new TimeOnly(12, 0)), new WorkShift(new TimeOnly(13, 0), new TimeOnly(17, 0)) }
        });
        return cal;
    }

    [Fact]
    public void WeeklyWorkPattern_MonToFri_IsWorking()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var monday9am = new DateTime(2026, 7, 27, 9, 0, 0);
        Assert.True(calc.IsWorkingTime(monday9am, cal));
    }

    [Fact]
    public void Weekend_IsNonWorking()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var saturday = new DateTime(2026, 8, 1, 10, 0, 0);
        Assert.False(calc.IsWorkingTime(saturday, cal));
    }

    [Fact]
    public void MultipleShifts_PerDay_Accepted()
    {
        var cal = CreateStandardCalendar();
        var monday = cal.WeeklyPattern.First(w => w.DayOfWeek == DayOfWeek.Monday);
        Assert.Equal(2, monday.Shifts.Count);
        Assert.Equal(4, monday.Shifts[0].TotalHours);
    }

    [Fact]
    public void HolidayException_DayBecomesNonWorking()
    {
        var cal = CreateStandardCalendar();
        cal.Exceptions.Add(new CalendarException
        {
            Date = new DateTime(2026, 7, 27),
            Type = CalendarExceptionType.Holiday,
            Reason = "Feriado"
        });
        var calc = new CalendarCalculationService();
        Assert.False(calc.IsWorkingTime(new DateTime(2026, 7, 27, 9, 0, 0), cal));
    }

    [Fact]
    public void ReducedHoursException_Applied()
    {
        var cal = CreateStandardCalendar();
        cal.Exceptions.Add(new CalendarException
        {
            Date = new DateTime(2026, 7, 28),
            Type = CalendarExceptionType.ReducedHours,
            SpecialShifts = new List<WorkShift> { new WorkShift(new TimeOnly(9, 0), new TimeOnly(13, 0)) }
        });
        var calc = new CalendarCalculationService();
        Assert.True(calc.IsWorkingTime(new DateTime(2026, 7, 28, 10, 0, 0), cal));
        Assert.False(calc.IsWorkingTime(new DateTime(2026, 7, 28, 14, 0, 0), cal));
    }

    [Fact]
    public void ExtendedHoursException_Applied()
    {
        var cal = CreateStandardCalendar();
        cal.Exceptions.Add(new CalendarException
        {
            Date = new DateTime(2026, 7, 29),
            Type = CalendarExceptionType.ExtendedHours,
            SpecialShifts = new List<WorkShift> { new WorkShift(new TimeOnly(8, 0), new TimeOnly(20, 0)) }
        });
        var calc = new CalendarCalculationService();
        Assert.True(calc.IsWorkingTime(new DateTime(2026, 7, 29, 18, 0, 0), cal));
        Assert.False(calc.IsWorkingTime(new DateTime(2026, 7, 29, 21, 0, 0), cal));
    }

    [Fact]
    public void IsWorkingTime_LunchBreak_ReturnsFalse()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var lunch = new DateTime(2026, 7, 27, 12, 30, 0);
        Assert.False(calc.IsWorkingTime(lunch, cal));
    }

    [Fact]
    public void GetNextWorkingInstant_AfterHours_ReturnsNextMorning()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var afterHours = new DateTime(2026, 7, 27, 18, 0, 0);
        var next = calc.GetNextWorkingInstant(afterHours, cal);
        Assert.Equal(new DateTime(2026, 7, 28, 8, 0, 0), next);
    }

    [Fact]
    public void GetPreviousWorkingInstant_WithinShift_ReturnsSameInstant()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var workingTime = new DateTime(2026, 7, 27, 10, 0, 0);
        var prev = calc.GetPreviousWorkingInstant(workingTime, cal);
        Assert.Equal(workingTime, prev);
    }

    [Fact]
    public void AddDuration_Hours_ReturnsCorrectEnd()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var start = new DateTime(2026, 7, 27, 8, 0, 0);
        var end = calc.AddDuration(start, new PlanningDuration(4, DurationUnit.Hours), cal);
        Assert.Equal(new DateTime(2026, 7, 27, 12, 0, 0), end);
    }

    [Fact]
    public void AddDuration_WorkDays_SkipsWeekend()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var friday = new DateTime(2026, 7, 31, 8, 0, 0);
        var end = calc.AddDuration(friday, new PlanningDuration(2, DurationUnit.WorkDays), cal);
        Assert.Equal(new DateTime(2026, 8, 4, 8, 0, 0), end);
    }

    [Fact]
    public void SubtractDates_ReturnsWorkingMinutes()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var start = new DateTime(2026, 7, 27, 8, 0, 0);
        var end = new DateTime(2026, 7, 27, 10, 0, 0);
        var duration = calc.SubtractDates(start, end, cal);
        Assert.Equal(120, duration.Value);
    }

    [Fact]
    public void GetWorkingHoursForDay_StandardDay_ReturnsEight()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var hours = calc.GetWorkingHoursForDay(new DateTime(2026, 7, 27), cal);
        Assert.Equal(8, hours);
    }

    [Fact]
    public void GetWorkingDaysBetween_ReturnsCorrectCount()
    {
        var cal = CreateStandardCalendar();
        var calc = new CalendarCalculationService();
        var start = new DateTime(2026, 7, 27);
        var end = new DateTime(2026, 7, 31);
        var days = calc.GetWorkingDaysBetween(start, end, cal);
        Assert.Equal(5, days.Count);
    }
}
