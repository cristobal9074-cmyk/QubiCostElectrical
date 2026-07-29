namespace QubiCostElectrical;

public sealed class CloseoutNonConformance
{
    public NonConformanceId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Correction { get; set; } = string.Empty;
    public string LinkedEvidenceIds { get; set; } = string.Empty;
    public double AssociatedCost { get; set; }
    public double ScheduleImpact { get; set; }
    public NonConformanceStatus Status { get; set; } = NonConformanceStatus.Open;
    public string VerifiedBy { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CorrectiveAction
{
    public CorrectiveActionId Id { get; init; } = null!;
    public NonConformanceId NonConformanceId { get; set; } = null!;
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string CompletedDate { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Comment { get; set; } = string.Empty;
}

public sealed class NonConformanceEvidence
{
    public EvidenceId Id { get; init; } = null!;
    public NonConformanceId NonConformanceId { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string UploadedUtc { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}
