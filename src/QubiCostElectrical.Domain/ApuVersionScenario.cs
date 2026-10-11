namespace QubiCostElectrical;

public sealed record ApuVersionScenario
{
    public string ScenarioId { get; init; } = "";

    public string Name { get; init; } = "";

    public string Type { get; init; } = "Custom";

    public decimal OverrideCount { get; init; }
}
