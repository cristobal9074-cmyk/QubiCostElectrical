using System;

namespace QubiCostElectrical;

public sealed class ActivityCalculatedDates
{
    public ActivityId ActivityId { get; set; } = null!;
    public DateTime EarlyStart { get; set; }
    public DateTime EarlyFinish { get; set; }
    public DateTime LateStart { get; set; }
    public DateTime LateFinish { get; set; }
    public double TotalFloat { get; set; }
    public double FreeFloat { get; set; }
    public bool IsCritical { get; set; }
}
