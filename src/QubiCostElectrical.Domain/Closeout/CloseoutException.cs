namespace QubiCostElectrical;

public sealed class CloseoutException
{
    public CloseoutExceptionId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string BlockerCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string AcceptedRisk { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string RequestDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string LinkedEvidenceIds { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public CloseoutExceptionStatus Status { get; set; } = CloseoutExceptionStatus.Requested;
    public string RowVersion { get; set; } = string.Empty;
}
