using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class LessonLearned
{
    public LessonLearnedId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LessonCategory Category { get; set; }
    public string EventDescription { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Applicability { get; set; } = string.Empty;
    public string WbsCode { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Reviewer { get; set; } = string.Empty;
    public string LessonDate { get; set; } = string.Empty;
    public string EvidenceIds { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public LessonStatus Status { get; set; } = LessonStatus.Draft;
    public int Version { get; set; } = 1;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class LessonEvidence
{
    public LessonEvidenceId Id { get; init; } = null!;
    public LessonLearnedId LessonId { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string UploadedUtc { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}
