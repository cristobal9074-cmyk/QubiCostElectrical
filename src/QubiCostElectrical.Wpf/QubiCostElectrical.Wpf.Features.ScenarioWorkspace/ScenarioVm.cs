#nullable enable
using System;

namespace QubiCostElectrical.Wpf.Features.ScenarioWorkspace;

public sealed record ScenarioVm
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	public string Name { get; set; } = "";

	public string ScenarioType { get; set; } = "Custom";

	public string Description { get; set; } = "";

	public bool IsActive { get; set; } = true;

	public decimal DirectCost { get; set; }

	public decimal IndirectCost { get; set; }

	public decimal Utility { get; set; }

	public decimal Taxes { get; set; }

	public decimal UnitPrice { get; set; }

	public decimal Total { get; set; }

	public decimal VariationPct { get; set; }

	public bool IsCalculated { get; set; }

	public bool IsBase { get; set; }
}
