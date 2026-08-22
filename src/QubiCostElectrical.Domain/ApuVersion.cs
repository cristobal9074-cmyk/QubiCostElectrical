using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ApuVersion
{
	public string VersionId { get; init; } = Guid.NewGuid().ToString("N");

	public string ApuId { get; init; } = Guid.NewGuid().ToString("N");

	public string Code { get; init; } = "";

	public string Name { get; init; } = "";

	public string Description { get; init; } = "";

	public string Category { get; init; } = "";

	public string Unit { get; init; } = "UN";

	public ApuVersionStatus Status { get; init; }

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
