using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class WeeklyWorkDay
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public List<WorkShift> Shifts { get; set; } = new();
}
