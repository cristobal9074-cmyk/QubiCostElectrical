namespace QubiCostElectrical;

public sealed record EconomicStep
{
    public int StepNumber { get; init; }

    public string ConceptCode { get; init; } = "";

    public string Formula { get; init; } = "";

    public decimal BaseValue { get; init; }

    public decimal AppliedValue { get; init; }

    public int RoundingDecimals { get; init; }

    public decimal SubtotalBefore { get; init; }

    public decimal SubtotalAfter { get; init; }

    public string Explanation { get; init; } = "";
}
