namespace QubiCostElectrical.Wpf.Features.ApuMerge;

public sealed record DiffVm
{
	public string Dimension { get; init; } = "";

	public string LocalValue { get; init; } = "";

	public string CatalogValue { get; init; } = "";

	public string Impact { get; init; } = "";
}
