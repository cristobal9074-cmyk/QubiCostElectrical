using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ScheduleProgressSummary
{
    public ScheduleId ScheduleId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public int TotalActivities { get; init; }
    public int NotStarted { get; init; }
    public int InProgress { get; init; }
    public int Completed { get; init; }
    public int Delayed { get; init; }
    public int Ahead { get; init; }
    public int Blocked { get; init; }
    public int Suspended { get; init; }
    public int Critical { get; init; }
    public int NegativeFloat { get; init; }
    public decimal GlobalPhysicalProgress { get; init; }
    public decimal GlobalFinancialProgress { get; init; }
    public decimal AccumulatedActualCost { get; init; }
    public decimal AccumulatedCommittedCost { get; init; }
    public PlanningDuration? RemainingDuration { get; init; }
    public DateTime? ForecastProjectFinish { get; init; }
    public GlobalProgressWeighting WeightingUsed { get; init; }
    public IReadOnlyList<OutOfSequenceWarning> OutOfSequenceWarnings { get; init; } = Array.Empty<OutOfSequenceWarning>();
}
