using System;

namespace QubiCostElectrical;

/// <summary>
/// Actualización de avance real de una actividad. Independiente de campos planificados/baseline.
/// </summary>
public sealed class ActivityProgressUpdate
{
    public string ProgressUpdateId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public ActivityId ActivityId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public DateTime? ActualStart { get; init; }
    public DateTime? ActualFinish { get; init; }
    public PlanningDuration? ActualDuration { get; init; }
    public decimal PercentComplete { get; init; }
    public decimal PhysicalProgress { get; init; }
    public decimal FinancialProgress { get; init; }
    public ProgressMethod ProgressMethod { get; init; } = ProgressMethod.Manual;
    public decimal? PlannedQuantity { get; init; }
    public decimal? ExecutedQuantity { get; init; }
    public string QuantityUnit { get; init; } = string.Empty;
    public decimal? ActualHours { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? CommittedCost { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public PlanningDuration? RemainingDuration { get; init; }
    public decimal? RemainingQuantity { get; init; }
    public decimal? Productivity { get; init; }
    public decimal? PlannedProductivity { get; init; }
    public string CrewInfo { get; init; } = string.Empty;
    public ProgressTrackingStatus ProgressStatus { get; init; } = ProgressTrackingStatus.NotStarted;
    public DelayReasonCode? DelayReason { get; init; }
    public string Comment { get; init; } = string.Empty;
    public string EvidenceReference { get; init; } = string.Empty;
    public decimal ManualWeight { get; init; }
    public DateTime? ForecastStart { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; init; }
    public string ModifiedBy { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}
