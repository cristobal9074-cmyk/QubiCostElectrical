using System;

namespace QubiCostElectrical;

public sealed record EconomicInput
{
	public decimal UnitDirectCost { get; init; }

	public decimal Materials { get; init; }

	public decimal Labor { get; init; }

	public decimal Equipment { get; init; }

	public decimal Tools { get; init; }

	public decimal Subcontract { get; init; }

	public decimal Transport { get; init; }

	public decimal Auxiliaries { get; init; }

	public decimal PartidaQuantity { get; init; } = 1m;

	public string Currency { get; init; } = "CLP";

	public DateTime EffectiveDate { get; init; } = DateTime.UtcNow;

	public EconomicConfig Config { get; init; } = EconomicConfig.CreateDefault();

	public string EngineVersion { get; init; } = "1.0.0";
}
