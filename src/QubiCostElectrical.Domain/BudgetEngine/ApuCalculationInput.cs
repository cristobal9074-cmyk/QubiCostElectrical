namespace QubiCostElectrical;

public sealed record ApuCalculationInput
{
    public IReadOnlyList<ApuDefinition> ApuDefinitions { get; init; } = Array.Empty<ApuDefinition>();
    public IReadOnlyList<Resource> Resources { get; init; } = Array.Empty<Resource>();
    public IReadOnlyList<ExchangeRate> ExchangeRates { get; init; } = Array.Empty<ExchangeRate>();
    public string? TargetCurrency { get; init; }
    public decimal? OverrideLaborRate { get; init; }
}
