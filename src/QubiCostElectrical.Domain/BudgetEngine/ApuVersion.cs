using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ApuVersionStatus
{
    Draft,
    PendingReview,
    InReview,
    Approved,
    Rejected,
    Obsolete,
    Archived
}

public sealed record ApuVersion
{
    public string VersionId { get; init; } = Guid.NewGuid().ToString("N");
    public string ApuId { get; init; } = Guid.NewGuid().ToString("N");
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string Unit { get; init; } = "UN";
    public ApuVersionStatus Status { get; init; } = ApuVersionStatus.Draft;
    public int VersionNumber { get; init; } = 1;
    public string PreviousVersionId { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Author { get; init; } = "";
    public string ChangeReason { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Tags { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public string CompatMetadata { get; init; } = "";

    public IReadOnlyList<ApuVersionComponent> Components { get; init; } = Array.Empty<ApuVersionComponent>();
    public IReadOnlyList<ApuVersionResourceRef> ResourceRefs { get; init; } = Array.Empty<ApuVersionResourceRef>();
    public IReadOnlyList<ApuVersionEconomicConcept> EconomicConcepts { get; init; } = Array.Empty<ApuVersionEconomicConcept>();
    public IReadOnlyList<ApuVersionScenario> Scenarios { get; init; } = Array.Empty<ApuVersionScenario>();

    public string ApuEngineVersion { get; init; } = "1.0.0";
    public string EconomicEngineVersion { get; init; } = "1.0.0";

    public decimal CalculatedDirectCost { get; init; }
    public decimal CalculatedFinalPrice { get; init; }
    public string CalculatedCurrency { get; init; } = "CLP";
    public DateTime EffectiveDate { get; init; } = DateTime.UtcNow;

    public string IntegrityHash { get; init; } = "";
    public string Notes { get; init; } = "";
    public string AuditRef { get; init; } = "";
}

public sealed record ApuVersionComponent
{
    public string ReferenceId { get; init; } = "";
    public string Description { get; init; } = "";
    public string ComponentType { get; init; } = "Resource";
    public decimal Yield { get; init; } = 1m;
    public decimal WastePercent { get; init; }
    public decimal AdjustmentFactor { get; init; } = 1m;
    public string Unit { get; init; } = "UN";
    public int SortOrder { get; init; }
}

public sealed record ApuVersionResourceRef
{
    public string ResourceId { get; init; } = "";
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public string ResourceType { get; init; } = "Material";
    public decimal Price { get; init; }
    public string Currency { get; init; } = "CLP";
    public decimal? ExchangeRate { get; init; }
}

public sealed record ApuVersionEconomicConcept
{
    public string Code { get; init; } = "";
    public string Category { get; init; } = "";
    public string CalculationBase { get; init; } = "";
    public decimal Value { get; init; }
    public int Order { get; init; }
    public int RoundingDecimals { get; init; } = 4;
}

public sealed record ApuVersionScenario
{
    public string ScenarioId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "Custom";
    public decimal OverrideCount { get; init; }
}
