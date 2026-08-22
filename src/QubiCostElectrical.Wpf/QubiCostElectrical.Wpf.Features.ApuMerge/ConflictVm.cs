namespace QubiCostElectrical.Wpf.Features.ApuMerge;

public sealed record ConflictVm
{
	public string Dimension { get; init; } = "";

	public string LocalValue { get; init; } = "";

	public string CatalogValue { get; init; } = "";

	public string BaseValue { get; init; } = "";

	public string Resolution { get; set; } = "KeepLocal";
}
