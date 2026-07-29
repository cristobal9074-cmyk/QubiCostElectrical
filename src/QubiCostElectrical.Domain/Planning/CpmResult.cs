using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class CpmResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> CyclePath { get; set; } = new();
    public Dictionary<ActivityId, ActivityCalculatedDates> CalculatedDates { get; set; } = new();
}
