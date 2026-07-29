using System;

namespace QubiCostElectrical;

/// <summary>Intervalo real de trabajo (soporte de pausa/reanudación / split).</summary>
public sealed class ProgressInterval
{
    public string IntervalId { get; init; } = null!;
    public string ProgressUpdateId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public ActivityId ActivityId { get; init; } = null!;
    public DateTime Start { get; init; }
    public DateTime? Finish { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal? Hours { get; init; }
    public decimal? Production { get; init; }
    public string Observation { get; init; } = string.Empty;
    public bool IsSuspension { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
