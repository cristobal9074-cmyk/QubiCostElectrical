using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ApuComparisonSummary
{
	public string ApuId { get; init; } = "";

	public string LeftVersionId { get; init; } = "";

	public string RightVersionId { get; init; } = "";

	public int LeftVersionNumber { get; init; }

	public int RightVersionNumber { get; init; }

	public DateTime ComparedAt { get; init; } = DateTime.UtcNow;

	public int TotalChanges { get; init; }

	public int BreakingChanges { get; init; }

	public int AddedCount { get; init; }

	public int RemovedCount { get; init; }

	public int ModifiedCount { get; init; }

	public decimal OldDirectCost { get; init; }

	public decimal NewDirectCost { get; init; }

	public decimal DirectCostDelta => NewDirectCost - OldDirectCost;

	public decimal? DirectCostPercentChange
	{
		get
		{
			if (!(OldDirectCost != 0m))
			{
				return null;
			}
			return Math.Round(DirectCostDelta / OldDirectCost * 100m, 2);
		}
	}

	public decimal OldFinalPrice { get; init; }

	public decimal NewFinalPrice { get; init; }

	public decimal PriceDelta => NewFinalPrice - OldFinalPrice;

	public decimal? PricePercentChange
	{
		get
		{
			if (!(OldFinalPrice != 0m))
			{
				return null;
			}
			return Math.Round(PriceDelta / OldFinalPrice * 100m, 2);
		}
	}

	public bool IsIdentical => TotalChanges == 0;

	public IReadOnlyList<ApuVersionDiff> Changes { get; init; } = Array.Empty<ApuVersionDiff>();

	public IReadOnlyList<string> BreakingChangeDescriptions { get; init; } = Array.Empty<string>();
}
