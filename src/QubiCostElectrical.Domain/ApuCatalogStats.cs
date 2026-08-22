namespace QubiCostElectrical;

public sealed record ApuCatalogStats
{
	public int TotalEntries { get; init; }

	public int ActiveEntries { get; init; }

	public int WithApprovedVersion { get; init; }

	public int WithDraftVersion { get; init; }

	public int TotalVersions { get; init; }

	public int TotalSnapshots { get; init; }
}
