namespace QubiCostElectrical;

public sealed class CloseoutAuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CloseoutId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string PerformedUtc { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
