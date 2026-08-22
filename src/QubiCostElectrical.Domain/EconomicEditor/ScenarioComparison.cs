using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

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
