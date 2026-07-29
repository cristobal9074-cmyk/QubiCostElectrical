using System;

namespace QubiCostElectrical;

public sealed class ActivityDependency
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ScheduleId ScheduleId { get; set; } = null!;
    public ActivityId PredecessorActivityId { get; set; } = null!;
    public ActivityId SuccessorActivityId { get; set; } = null!;
    public DependencyType Type { get; set; }
    public PlanningDuration Lag { get; set; } = null!;
    public bool IsLead { get; set; }
    public string LagCalendarId { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string LastEditor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public string CompatMetadata { get; set; } = string.Empty;

    public ActivityDependency(
        string id,
        ScheduleId scheduleId,
        ActivityId predecessorActivityId,
        ActivityId successorActivityId,
        DependencyType type,
        PlanningDuration lag,
        string author)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ScheduleId = scheduleId ?? throw new ArgumentNullException(nameof(scheduleId));
        PredecessorActivityId = predecessorActivityId ?? throw new ArgumentNullException(nameof(predecessorActivityId));
        SuccessorActivityId = successorActivityId ?? throw new ArgumentNullException(nameof(successorActivityId));
        Type = type;
        Lag = lag ?? throw new ArgumentNullException(nameof(lag));
        Author = author ?? throw new ArgumentNullException(nameof(author));
        LastEditor = author;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = CreatedAt;
        RowVersion = Guid.NewGuid().ToString("N");
    }

    public ActivityDependency() { }
}
