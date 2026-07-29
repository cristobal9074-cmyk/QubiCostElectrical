using System;

namespace QubiCostElectrical;

public sealed class DelayReasonEntry
{
    public string EntryId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public ActivityId ActivityId { get; init; } = null!;
    public string ProgressUpdateId { get; init; } = string.Empty;
    public DelayReasonCode Code { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Responsible { get; init; } = string.Empty;
    public decimal ImpactDays { get; init; }
    public string Comment { get; init; } = string.Empty;
    public string EvidenceReference { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
