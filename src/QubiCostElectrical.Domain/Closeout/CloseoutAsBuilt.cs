namespace QubiCostElectrical;

public sealed class AsBuiltDeliverable
{
    public AsBuiltDeliverableId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RequiredVersion { get; set; } = string.Empty;
    public string DeliveredVersion { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string DeliveryDate { get; set; } = string.Empty;
    public string ApprovalDate { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public AsBuiltStatus Status { get; set; } = AsBuiltStatus.Required;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class TrainingRecord
{
    public TrainingRecordId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Attendees { get; set; } = string.Empty;
    public string TrainingDate { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string DocumentationRefs { get; set; } = string.Empty;
    public string Evaluation { get; set; } = string.Empty;
    public string EvidenceRefs { get; set; } = string.Empty;
    public string Conformity { get; set; } = string.Empty;
    public string PendingItems { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}
