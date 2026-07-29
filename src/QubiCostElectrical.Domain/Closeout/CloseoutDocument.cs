namespace QubiCostElectrical;

public sealed class CloseoutDocumentRequirement
{
    public DocumentRequirementId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public bool IsBlocking { get; set; }
    public int MinVersion { get; set; } = 1;
    public string Responsible { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Required;
    public string LinkedDocumentId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovedDate { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CloseoutDocument
{
    public CloseoutDocumentId Id { get; init; } = null!;
    public string RequirementId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedUtc { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.PendingSubmission;
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovedDate { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CloseoutDocumentVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CloseoutDocumentId DocumentId { get; set; } = null!;
    public int VersionNumber { get; set; } = 1;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string UploadedUtc { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed class CloseoutDocumentApproval
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CloseoutDocumentId DocumentId { get; set; } = null!;
    public bool IsApproved { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovedDate { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
