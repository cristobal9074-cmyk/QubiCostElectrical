namespace QubiCostElectrical;

public sealed class CloseoutAlert
{
    public CloseoutAlertId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public CloseoutAlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string RowVersion { get; set; } = string.Empty;
}
