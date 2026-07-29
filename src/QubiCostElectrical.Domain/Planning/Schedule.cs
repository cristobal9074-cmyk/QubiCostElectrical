using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class Schedule
{
    public ScheduleId Id { get; private set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public CalendarId DefaultCalendarId { get; set; } = null!;
    public DateTime? ProjectStartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public ScheduleStatus Status { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string CompatibilityMetadata { get; set; } = string.Empty;

    public Schedule(ScheduleId id, string name, string timeZoneId, CalendarId defaultCalendarId, string createdBy)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TimeZoneId = timeZoneId ?? throw new ArgumentNullException(nameof(timeZoneId));
        DefaultCalendarId = defaultCalendarId ?? throw new ArgumentNullException(nameof(defaultCalendarId));
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        ModifiedBy = createdBy;
        Description = string.Empty;
        Status = ScheduleStatus.Draft;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = CreatedAt;
        RowVersion = Guid.NewGuid().ToString("N");
        Remarks = string.Empty;
        CompatibilityMetadata = string.Empty;
    }

    public Schedule() { }
}
