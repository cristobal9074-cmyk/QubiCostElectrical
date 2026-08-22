namespace QubiCostElectrical;

public sealed record MergeConflict
{
	public string Identifier { get; init; } = "";

	public string Property { get; init; } = "";

	public string LeftValue { get; init; } = "";

	public string RightValue { get; init; } = "";

	public string BaseValue { get; init; } = "";

	public MergeConflictResolution Resolution { get; init; } = MergeConflictResolution.Manual;

	public bool IsResolved => Resolution != MergeConflictResolution.Manual;
}
