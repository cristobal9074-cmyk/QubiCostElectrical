namespace QubiCostElectrical;

public sealed record ApuVersionComponent
{
    public string ReferenceId { get; init; } = "";

    public string Description { get; init; } = "";

    public string ComponentType { get; init; } = "Resource";

    public decimal Yield { get; init; } = 1m;

    public decimal WastePercent { get; init; }

    public decimal AdjustmentFactor { get; init; } = 1m;

    public string Unit { get; init; } = "UN";

    public int SortOrder { get; init; }
}
