using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ProgressSnapshot
{
    public string SnapshotId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public int Version { get; init; }
    public DateTime StatusDate { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string SHA256 { get; init; } = string.Empty;
    public ScheduleProgressSummary? Summary { get; init; }
    public DateTime? ForecastProjectFinish { get; init; }
    public IReadOnlyList<string> CriticalPathActivityIds { get; init; } = Array.Empty<string>();
}

public sealed class ProgressSnapshotItem
{
    public string SnapshotId { get; init; } = null!;
    public ActivityId ActivityId { get; init; } = null!;
    public ActivityProgressUpdate Progress { get; init; } = null!;
    public DateTime? ForecastStart { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public bool IsCritical { get; init; }
}
