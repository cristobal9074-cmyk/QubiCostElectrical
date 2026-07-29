using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class CalendarException
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; }
    public CalendarExceptionType Type { get; set; }
    public List<WorkShift>? SpecialShifts { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
