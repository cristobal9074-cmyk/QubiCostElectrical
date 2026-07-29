using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ScheduleBaselineSnapshot
{
    public Schedule Schedule { get; set; } = null!;
    public List<Activity> Activities { get; set; } = new();
    public List<ActivityDependency> Dependencies { get; set; } = new();
    public List<Calendar> Calendars { get; set; } = new();
    public string CpmEngineVersion { get; set; } = "1.0.0";
}
