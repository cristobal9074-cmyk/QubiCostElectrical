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

    public EconomicCalculationTrace Trace { get; init; } = new EconomicCalculationTrace();

    public string EngineVersion { get; init; } = "";

    public string DeterministicHash { get; init; } = "";
}
