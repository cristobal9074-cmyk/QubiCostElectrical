using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Entrada tipada al motor EVM (solo lectura; no muta plan/baseline).</summary>
public sealed class EarnedValueInput
{
    public ScheduleId ScheduleId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public decimal Bac { get; init; }
    public decimal Pv { get; init; }
    public decimal Ev { get; init; }
    public decimal Ac { get; init; }
    public decimal? CommittedCost { get; init; }
    public decimal PercentPlanned { get; init; }
    public decimal PercentEarned { get; init; }
    public decimal PercentActual { get; init; }
    public EarnedValueMethod EvMethod { get; init; } = EarnedValueMethod.PercentComplete;
    public EacMethod EacMethod { get; init; } = EacMethod.BacOverCpi;
    public DateTime? BaselineFinish { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public DateTime? PlannedFinish { get; init; }
    public bool HasBaseline { get; init; }
    public bool HasStatusDate { get; init; } = true;
    public bool HasBudget { get; init; }
    public bool HasProgress { get; init; }
    public bool IsProjectFinished { get; init; }
    public bool IsProjectCancelled { get; init; }
    public bool IsProjectSuspended { get; init; }
    public IReadOnlyList<EarnedValueActivityInput> Activities { get; init; } = Array.Empty<EarnedValueActivityInput>();
}

public sealed class EarnedValueActivityInput
{
    public ActivityId ActivityId { get; init; } = null!;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Budget { get; init; }
    public decimal PlannedValueAtStatus { get; init; }
    public decimal EarnedValueAtStatus { get; init; }
    public decimal ActualCostAtStatus { get; init; }
    public decimal PercentComplete { get; init; }
    public decimal PhysicalProgress { get; init; }
    public bool IsCritical { get; init; }
}
