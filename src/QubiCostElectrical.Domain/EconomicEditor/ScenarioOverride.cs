using System;

namespace QubiCostElectrical;

public sealed record ScenarioOverride
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public OverrideTargetType TargetType { get; init; }

    public string TargetId { get; init; } = "";

    public OverrideField Field { get; init; }

    public decimal? NumericValue { get; init; }

    public string? StringValue { get; init; }

    public bool? BoolValue { get; init; }

    public string Source { get; init; } = "";

    public string Description { get; init; } = "";
}
