using System;

namespace QubiCostElectrical;

public sealed class ProgressEvidence
{
    public string EvidenceId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public ActivityId? ActivityId { get; init; }
    public string ProgressUpdateId { get; init; } = string.Empty;
    public EvidenceReferenceType Type { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
