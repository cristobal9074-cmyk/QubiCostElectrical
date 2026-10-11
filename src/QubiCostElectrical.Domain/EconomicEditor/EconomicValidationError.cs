namespace QubiCostElectrical;

public sealed record EconomicValidationError
{
    public string Code { get; init; } = "";

    public string Message { get; init; } = "";

    public string Field { get; init; } = "";

    public string ConceptCode { get; init; } = "";

    public string ScenarioId { get; init; } = "";

    public EconomicValidationSeverity Severity { get; init; }

    public string SafeMetadata { get; init; } = "";
}
