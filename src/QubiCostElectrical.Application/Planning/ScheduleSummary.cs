using System;

namespace QubiCostElectrical;

public sealed record ScheduleSummary
{
    public ScheduleId Id { get; init; } = null!;
    public string Name { get; init; } = string.Empty;
    public ScheduleStatus Status { get; init; }
    public int TotalWbsNodes { get; init; }
    public int TotalActivities { get; init; }
    public int TotalMilestones { get; init; }
    public int TotalCalendars { get; init; }
    public int TotalConstraints { get; init; }
    public DateTime? ProjectStartDate { get; init; }
    public DateTime? TargetEndDate { get; init; }
}
