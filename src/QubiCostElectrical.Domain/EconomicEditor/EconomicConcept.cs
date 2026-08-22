using System;

namespace QubiCostElectrical;

public sealed record EconomicConcept
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	public string Code { get; init; } = "";

	public string Name { get; init; } = "";

	public string Description { get; init; } = "";

	public EconomicCategory Category { get; init; } = EconomicCategory.Other;

	public CalculationType CalculationType { get; init; }

	public CalculationBase CalculationBase { get; init; }

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
