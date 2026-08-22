using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record EconomicValidationResult
{
	public bool IsValid { get; init; }

	public IReadOnlyList<EconomicValidationError> Errors { get; init; } = Array.Empty<EconomicValidationError>();

	public IReadOnlyList<EconomicValidationError> Warnings { get; init; } = Array.Empty<EconomicValidationError>();
}
