using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ScenarioType
{
    Base,
    Conservative,
    Optimistic,
    Pessimistic,
    Custom
}

public enum ScenarioStatus
{
    Draft,
    Calculated,
    Applied,
    Archived
}

public sealed record Scenario
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public ScenarioType Type { get; init; } = ScenarioType.Custom;
    public string BaseScenarioId { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Author { get; init; } = "";
    public IReadOnlyList<ScenarioOverride> Overrides { get; init; } = Array.Empty<ScenarioOverride>();
    public ScenarioStatus Status { get; init; } = ScenarioStatus.Draft;
    public string Currency { get; init; } = "CLP";
    public string Comment { get; init; } = "";
    public string Fingerprint { get; init; } = "";
    public DateTime? CalculationDate { get; init; }
    public EconomicResult? Snapshot { get; init; }
}

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

public enum OverrideTargetType
{
    ResourcePrice,
    ExchangeRate,
    ResourceYield,
    ResourceWaste,
    ResourceFactor,
    EconomicConcept,
    ConceptRate,
    ConceptAmount,
    ConceptToggle
}

public enum OverrideField
{
    Value,
    Rate,
    IsActive,
    Yield,
    Waste,
    Factor,
    BasePrice,
    ExchangeRateValue
}

public sealed record ScenarioComparison
{
    public string BaseScenarioId { get; init; } = "";
    public string ComparedScenarioId { get; init; } = "";
    public string BaseScenarioName { get; init; } = "";
    public string ComparedScenarioName { get; init; } = "";

    public decimal DirectCostDifference { get; init; }
    public decimal IndirectDifference { get; init; }
    public decimal ProfitDifference { get; init; }
    public decimal TaxDifference { get; init; }
    public decimal UnitPriceDifference { get; init; }
    public decimal TotalDifference { get; init; }
    public decimal VariationPercent { get; init; }

    public IReadOnlyList<ConceptDifference> ConceptDifferences { get; init; } = Array.Empty<ConceptDifference>();
    public IReadOnlyList<ResourceImpactRow> TopImpactResources { get; init; } = Array.Empty<ResourceImpactRow>();
    public IReadOnlyList<string> ModifiedConcepts { get; init; } = Array.Empty<string>();
}

public sealed record ConceptDifference
{
    public string Code { get; init; } = "";
    public decimal BaseValue { get; init; }
    public decimal ComparedValue { get; init; }
    public decimal Difference { get; init; }
}

public sealed record ResourceImpactRow
{
    public string ResourceId { get; init; } = "";
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal BaseTotal { get; init; }
    public decimal ComparedTotal { get; init; }
    public decimal Impact { get; init; }
}
