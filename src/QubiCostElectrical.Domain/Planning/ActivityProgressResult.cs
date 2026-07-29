using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Resultado estructurado de seguimiento por actividad.</summary>
public sealed class ActivityProgressResult
{
    public ActivityId ActivityId { get; init; } = null!;
    public string ActivityCode { get; init; } = string.Empty;
    public string ActivityName { get; init; } = string.Empty;
    public ProgressTrackingStatus Status { get; init; }
    public DateTime StatusDate { get; init; }
    public DateTime? BaselineStart { get; init; }
    public DateTime? BaselineFinish { get; init; }
    public DateTime? PlannedStart { get; init; }
    public DateTime? PlannedFinish { get; init; }
    public DateTime? ActualStart { get; init; }
    public DateTime? ActualFinish { get; init; }
    public DateTime? ForecastStart { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public decimal PercentComplete { get; init; }
    public decimal PhysicalProgress { get; init; }
    public decimal FinancialProgress { get; init; }
    public double? StartVarianceDays { get; init; }
    public double? FinishVarianceDays { get; init; }
    public double? DurationVarianceDays { get; init; }
    public PlanningDuration? RemainingDuration { get; init; }
    public decimal? ExecutedQuantity { get; init; }
    public decimal? RemainingQuantity { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? CommittedCost { get; init; }
    public decimal? Productivity { get; init; }
    public bool IsCritical { get; init; }
    public IReadOnlyList<DelayReasonCode> DelayReasons { get; init; } = Array.Empty<DelayReasonCode>();
    public IReadOnlyList<OutOfSequenceWarning> Warnings { get; init; } = Array.Empty<OutOfSequenceWarning>();
    public IReadOnlyList<ProgressValidationIssue> Errors { get; init; } = Array.Empty<ProgressValidationIssue>();
    public string Explanation { get; init; } = string.Empty;
}
