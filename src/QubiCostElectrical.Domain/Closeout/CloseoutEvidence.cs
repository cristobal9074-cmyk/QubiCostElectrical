namespace QubiCostElectrical;

public sealed class CloseoutEvidence
{
    public EvidenceId Id { get; init; } = null!;
    public string EvidenceType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string UploadedUtc { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public EvidenceStatus Status { get; set; } = EvidenceStatus.Pending;
    public string Metadata { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
