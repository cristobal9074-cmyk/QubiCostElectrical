namespace QubiCostElectrical;

public sealed record RoundingDetail
{
	public string ConceptCode { get; init; } = "";

	public decimal UnroundedValue { get; init; }

	public decimal RoundedValue { get; init; }

	public int Decimals { get; init; }

	public string RoundingMode { get; init; } = "ToEven";
}
