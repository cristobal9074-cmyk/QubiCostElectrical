using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

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

	public ScenarioStatus Status { get; init; }

	public string Currency { get; init; } = "CLP";

	public string Comment { get; init; } = "";

	public string Fingerprint { get; init; } = "";

	public DateTime? CalculationDate { get; init; }

	public EconomicResult? Snapshot { get; init; }
}
