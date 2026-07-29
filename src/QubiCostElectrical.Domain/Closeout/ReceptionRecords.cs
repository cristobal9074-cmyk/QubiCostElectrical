namespace QubiCostElectrical;

public sealed class ReceptionRecord
{
    public ReceptionRecordId Id { get; init; } = null!;
    public ProjectReceptionId ReceptionId { get; set; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public ReceptionType ReceptionType { get; set; }
    public string RecordDate { get; set; } = string.Empty;
    public string Parties { get; set; } = string.Empty;
    public string DeliveredScope { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string Reservations { get; set; } = string.Empty;
    public string AcceptedPendingItems { get; set; } = string.Empty;
    public string GuaranteeRefs { get; set; } = string.Empty;
    public string DocumentationRefs { get; set; } = string.Empty;
    public string Approvers { get; set; } = string.Empty;
    public string Signatures { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string HashSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsImmutable { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class HandoverRecord
{
    public HandoverRecordId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string HandoverDate { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Contractor { get; set; } = string.Empty;
    public string DeliveredScope { get; set; } = string.Empty;
    public string PendingItems { get; set; } = string.Empty;
    public string DocumentationRefs { get; set; } = string.Empty;
    public string CertificateRefs { get; set; } = string.Empty;
    public string TrainingRefs { get; set; } = string.Empty;
    public string WarrantyRefs { get; set; } = string.Empty;
    public string Approvers { get; set; } = string.Empty;
    public string Signatures { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string HashSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class AcceptanceCertificate
{
    public AcceptanceCertificateId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public ReceptionType ReceptionType { get; set; }
    public string CertificateDate { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public string AcceptedBy { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public string HashSha256 { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}
