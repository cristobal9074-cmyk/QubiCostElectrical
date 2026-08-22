namespace QubiCostElectrical;

public sealed record ApuVersionResourceRef
{
	public string ResourceId { get; init; } = "";

	public string Code { get; init; } = "";

	public string Description { get; init; } = "";

	public string ResourceType { get; init; } = "Material";

	public decimal Price { get; init; }

	public string Currency { get; init; } = "CLP";

	public decimal? ExchangeRate { get; init; }
}
