namespace QubiCostElectrical;

public sealed record ResourceImpactRow
{
    public string ResourceId { get; init; } = "";

    public string Code { get; init; } = "";

    public string Description { get; init; } = "";

    public decimal BaseTotal { get; init; }

    public decimal ComparedTotal { get; init; }

    public decimal Impact { get; init; }
}
