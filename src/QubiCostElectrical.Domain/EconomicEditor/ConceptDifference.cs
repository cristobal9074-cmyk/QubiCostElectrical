namespace QubiCostElectrical;

public sealed record ConceptDifference
{
	public string Code { get; init; } = "";

	public decimal BaseValue { get; init; }

	public decimal ComparedValue { get; init; }

	public decimal Difference { get; init; }
}
