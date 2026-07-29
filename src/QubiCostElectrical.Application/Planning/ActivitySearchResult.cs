using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ActivitySearchResult
{
    public IReadOnlyList<Activity> Activities { get; init; } = Array.Empty<Activity>();
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
}
