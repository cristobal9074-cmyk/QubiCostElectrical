namespace QubiCostElectrical;

public sealed record ApuDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public string Unit { get; init; } = "UN";
    public decimal Quantity { get; init; } = 1m;
    public List<ApuComponent> Components { get; init; } = new();
    public bool IsActive { get; init; } = true;
    public string Notes { get; init; } = "";
    public string SourceProjectId { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = "";
    public string ModifiedBy { get; init; } = "";
}
