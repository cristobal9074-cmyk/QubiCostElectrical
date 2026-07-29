using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ScheduleComparisonResult
{
    public List<ActivityDiff> ActivityDifferences { get; set; } = new();
    public List<DependencyDiff> DependencyDifferences { get; set; } = new();
    public int AddedActivitiesCount { get; set; }
    public int DeletedActivitiesCount { get; set; }
    public int ModifiedActivitiesCount { get; set; }
}

public sealed class ActivityDiff
{
    public ActivityId ActivityId { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiffType { get; set; } = string.Empty; // "Added", "Deleted", "Modified"
    
    public double? DurationDiff { get; set; }
    public double? StartOffsetDaysDiff { get; set; }
    public double? FinishOffsetDaysDiff { get; set; }
    
    public string CriticalDiff { get; set; } = string.Empty; // "No Change", "Became Critical", "No Longer Critical"
    public string ConstraintDiff { get; set; } = string.Empty;
    public string DeadlineDiff { get; set; } = string.Empty;
}

public sealed class DependencyDiff
{
    public string DependencyId { get; set; } = string.Empty;
    public string DiffType { get; set; } = string.Empty; // "Added", "Deleted", "Modified"
    public string PredecessorCodeName { get; set; } = string.Empty;
    public string SuccessorCodeName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
