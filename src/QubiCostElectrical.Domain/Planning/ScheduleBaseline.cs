using System;

namespace QubiCostElectrical;

public sealed class ScheduleBaseline
{
    public string BaselineId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public int Version { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string Status { get; init; } = "Draft"; // "Draft", "Official"
    public string SHA256 { get; init; } = string.Empty;
    public DateTime SnapshotDate { get; init; }
    public bool IsOfficial { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string Metadata { get; init; } = string.Empty; // JSON metadata
}
