namespace QubiCostElectrical;

public sealed class GuaranteeReleaseRequest
{
    public GuaranteeReleaseId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public string GuaranteeRef { get; set; } = string.Empty;
    public string GuaranteeType { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
    public string ReleaseCondition { get; set; } = string.Empty;
    public string DocumentRef { get; set; } = string.Empty;
    public string RenewalRef { get; set; } = string.Empty;
    public GuaranteeReleaseStatus Status { get; set; } = GuaranteeReleaseStatus.Requested;
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestedUtc { get; set; } = string.Empty;
    public decimal ReleasedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string ReleaseReason { get; set; } = string.Empty;
    public string Alert { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class GuaranteeReleaseApproval
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public GuaranteeReleaseId ReleaseId { get; set; } = null!;
    public bool IsApproved { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovalDate { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed class RetentionReleaseRequest
{
    public RetentionReleaseId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public string PaymentCertificateRef { get; set; } = string.Empty;
    public decimal RetentionAmount { get; set; }
    public decimal ReleasedAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string ReleaseCondition { get; set; } = string.Empty;
    public RetentionReleaseStatus Status { get; set; } = RetentionReleaseStatus.Requested;
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestedUtc { get; set; } = string.Empty;
    public bool IsPartial { get; set; }
    public string ApprovalDate { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
