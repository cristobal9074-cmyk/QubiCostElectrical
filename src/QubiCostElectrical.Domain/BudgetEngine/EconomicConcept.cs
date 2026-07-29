namespace QubiCostElectrical;

public enum EconomicCategory
{
    IndirectCost,
    Overhead,
    Administration,
    Supervision,
    Contingency,
    Risk,
    Profit,
    Tax,
    Surcharge,
    Discount,
    InformativeWithholding,
    Other
}

public enum CalculationType
{
    Percentage,
    FixedAmount,
    AmountPerUnit,
    AmountPerQuantity,
    CumulativePercentage,
    PercentageOnSelectedBase
}

public enum CalculationBase
{
    DirectCost,
    Materials,
    Labor,
    Equipment,
    Tools,
    Subcontract,
    Transport,
    Auxiliaries,
    OtherResources,
    DirectCostPlusIndirect,
    PreviousSubtotal,
    SubtotalBeforeProfit,
    SubtotalBeforeTax,
    PriceBeforeDiscounts,
    FinalPreviousPrice,
    Custom
}

public sealed record EconomicConcept
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public EconomicCategory Category { get; init; } = EconomicCategory.Other;
    public CalculationType CalculationType { get; init; } = CalculationType.Percentage;
    public CalculationBase CalculationBase { get; init; } = CalculationBase.DirectCost;
    public decimal Value { get; init; }
    public int Order { get; init; }
    public bool IsActive { get; init; } = true;
    public bool AffectsFinalPrice { get; init; } = true;
    public bool IsInformative { get; init; }
    public int RoundingDecimals { get; init; } = 4;
    public DateTime EffectiveDate { get; init; } = DateTime.UtcNow;
    public string ResponsibleUser { get; init; } = "";
    public string Notes { get; init; } = "";
    public string CompatMetadata { get; init; } = "";
}
