using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

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
