namespace QubiCostElectrical.Wpf.Features.ApuCatalog;

public sealed record ApuCatalogEntryVm
{
	public string ApuId { get; init; } = "";

	public string Code { get; init; } = "";

	public string Name { get; init; } = "";

	public string Category { get; init; } = "";

	public string Unit { get; init; } = "UN";

	public int VersionCount { get; init; }

	public int ActiveVersionNumber { get; init; }

	public string ActiveVersionStatus { get; init; } = "Draft";

	public decimal LastPrice { get; init; }

	public bool IsActive { get; init; } = true;

	public string Owner { get; init; } = "";
}
