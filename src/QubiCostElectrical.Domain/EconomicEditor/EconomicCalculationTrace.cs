using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record EconomicCalculationTrace
{
    public int StepsExecuted { get; init; }

    public TimeSpan Elapsed { get; init; }

    public IReadOnlyList<EconomicStep> Steps { get; init; } = Array.Empty<EconomicStep>();
}
