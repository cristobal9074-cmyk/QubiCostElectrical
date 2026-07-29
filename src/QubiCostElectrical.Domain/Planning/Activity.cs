using System;

namespace QubiCostElectrical;

public sealed class Activity
{
    public ActivityId Id { get; private set; } = null!;
    public ScheduleId ScheduleId { get; private set; } = null!;
    public WbsPlanningNodeId WbsNodeId { get; private set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public PlanningDuration PlannedDuration { get; set; } = null!;
    public CalendarId? CalendarId { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public int Priority { get; set; }
    public ActivityStatus Status { get; set; }
    public string? ResponsibleId { get; set; }
    public decimal PlannedProgressPct { get; set; }
    public decimal? PlannedQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public DateTime? EarlyStart { get; set; }
    public DateTime? EarlyFinish { get; set; }
    public DateTime? LateStart { get; set; }
    public DateTime? LateFinish { get; set; }
    public double? TotalFloat { get; set; }
    public double? FreeFloat { get; set; }
    public bool IsCritical { get; set; }
    public DateTime? Deadline { get; set; }
    public ConstraintType ConstraintType { get; set; } = ConstraintType.None;
    public DateTime? ConstraintDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string LastEditor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    public Activity(ActivityId id, ScheduleId scheduleId, WbsPlanningNodeId wbsNodeId, string code, string name, ActivityType type, PlanningDuration plannedDuration, string author)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ScheduleId = scheduleId ?? throw new ArgumentNullException(nameof(scheduleId));
        WbsNodeId = wbsNodeId ?? throw new ArgumentNullException(nameof(wbsNodeId));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        PlannedDuration = plannedDuration;
        Author = author ?? throw new ArgumentNullException(nameof(author));
        LastEditor = author;
        QuantityUnit = string.Empty;
        Tags = string.Empty;
        Remarks = string.Empty;
        Description = string.Empty;
        Status = ActivityStatus.NotStarted;
        Priority = 500;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = CreatedAt;
        RowVersion = Guid.NewGuid().ToString("N");
    }

    public Activity() { }
}
