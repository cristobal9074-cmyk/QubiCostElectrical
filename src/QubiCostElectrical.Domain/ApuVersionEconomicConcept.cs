namespace QubiCostElectrical;

public sealed record ApuVersionEconomicConcept
{
    public string Code { get; init; } = "";

    public string Category { get; init; } = "";

    public string CalculationBase { get; init; } = "";

    public decimal Value { get; init; }

    public int Order { get; init; }

    public int RoundingDecimals { get; init; } = 4;
}
