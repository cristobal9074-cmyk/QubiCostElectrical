using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record EconomicResult
{
    public decimal UnitDirectCost { get; init; }
    public decimal TotalIndirectCosts { get; init; }
    public decimal Overhead { get; init; }
    public decimal Administration { get; init; }
    public decimal Supervision { get; init; }
    public decimal Contingency { get; init; }
    public decimal Risk { get; init; }
    public decimal Profit { get; init; }
    public decimal TotalSurcharges { get; init; }
    public decimal TotalDiscounts { get; init; }
    public decimal TotalTaxes { get; init; }
    public decimal TotalInformativeWithholdings { get; init; }
    public decimal FinalUnitPrice { get; init; }
    public decimal PartidaQuantity { get; init; }
    public decimal TotalPartidaCost { get; init; }
    public string Currency { get; init; } = "CLP";
    public DateTime CalculationDate { get; init; } = DateTime.UtcNow;

    public IReadOnlyList<ConceptResult> ConceptsApplied { get; init; } = Array.Empty<ConceptResult>();
    public IReadOnlyList<string> ConceptsOmitted { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RoundingDetail> RoundingDetails { get; init; } = Array.Empty<RoundingDetail>();
    public EconomicCalculationTrace Trace { get; init; } = new();
    public string EngineVersion { get; init; } = "";
    public string DeterministicHash { get; init; } = "";
}

public sealed record ConceptResult
{
    public string ConceptId { get; init; } = "";
    public string Code { get; init; } = "";
    public EconomicCategory Category { get; init; }
    public CalculationBase UsedBase { get; init; }
    public decimal BaseValue { get; init; }
    public CalculationType CalcType { get; init; }
    public decimal RateOrAmount { get; init; }
    public decimal UnroundedValue { get; init; }
    public decimal RoundedValue { get; init; }
    public decimal PreviousSubtotal { get; init; }
    public decimal PostSubtotal { get; init; }
    public bool AffectsPrice { get; init; }
    public bool IsInformative { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string Explanation { get; init; } = "";
}

public sealed record RoundingDetail
{
    public string ConceptCode { get; init; } = "";
    public decimal UnroundedValue { get; init; }
    public decimal RoundedValue { get; init; }
    public int Decimals { get; init; }
    public string RoundingMode { get; init; } = "ToEven";
}

public sealed record EconomicCalculationTrace
{
    public int StepsExecuted { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<EconomicStep> Steps { get; init; } = Array.Empty<EconomicStep>();
}

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
