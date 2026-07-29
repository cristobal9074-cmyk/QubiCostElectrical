namespace QubiCostElectrical;

public enum ApuComponentType
{
    Resource,
    AuxiliaryApu
}

public sealed record ApuComponent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ApuComponentType ComponentType { get; init; } = ApuComponentType.Resource;
    public string ReferenceId { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Yield { get; init; } = 1m;
    public decimal WastePercent { get; init; }
    public decimal AdjustmentFactor { get; init; } = 1m;
    public string Unit { get; init; } = "UN";
    public int SortOrder { get; init; }
}
