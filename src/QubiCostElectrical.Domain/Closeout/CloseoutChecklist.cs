namespace QubiCostElectrical;

public sealed class CloseoutChecklistTemplate
{
    public ChecklistTemplateId Id { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsObsolete { get; set; }
    public string ProjectType { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
}

public sealed class CloseoutChecklistCategory
{
    public ChecklistCategoryId Id { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class CloseoutChecklist
{
    public ChecklistId Id { get; init; } = null!;
    public CloseoutId CloseoutId { get; set; } = null!;
    public string TemplateId { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CloseoutChecklistItem
{
    public ChecklistItemId Id { get; init; } = null!;
    public ChecklistId ChecklistId { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsBlocking { get; set; }
    public string Responsible { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public ChecklistItemStatus Status { get; set; } = ChecklistItemStatus.Pending;
    public bool EvidenceRequired { get; set; }
    public string LinkedEvidenceIds { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string VerifiedBy { get; set; } = string.Empty;
    public string VerificationDate { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
