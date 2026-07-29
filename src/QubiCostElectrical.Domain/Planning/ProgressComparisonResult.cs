using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ProgressComparisonResult
{
    public string LeftLabel { get; init; } = string.Empty;
    public string RightLabel { get; init; } = string.Empty;
    public DateTime? LeftStatusDate { get; init; }
    public DateTime? RightStatusDate { get; init; }
    public IReadOnlyList<ProgressActivityDiff> ActivityDiffs { get; init; } = Array.Empty<ProgressActivityDiff>();
    public IReadOnlyList<string> NewlyStartedActivityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NewlyCompletedActivityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NewlyDelayedActivityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NewlyAheadActivityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CriticalPathChangedActivityIds { get; init; } = Array.Empty<string>();
}

public sealed class ProgressActivityDiff
{
    public ActivityId ActivityId { get; init; } = null!;
    public string ActivityCode { get; init; } = string.Empty;
    public decimal? PercentCompleteDelta { get; init; }
    public decimal? PhysicalProgressDelta { get; init; }
    public decimal? FinancialProgressDelta { get; init; }
    public decimal? QuantityDelta { get; init; }
    public decimal? ActualCostDelta { get; init; }
    public double? StartVarianceDaysDelta { get; init; }
    public double? FinishVarianceDaysDelta { get; init; }
    public double? DurationVarianceDaysDelta { get; init; }
    public ProgressTrackingStatus? LeftStatus { get; init; }
    public ProgressTrackingStatus? RightStatus { get; init; }
    public bool CriticalChanged { get; init; }
}
