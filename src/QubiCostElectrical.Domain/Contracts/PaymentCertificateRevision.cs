using System;

namespace QubiCostElectrical;

public sealed class PaymentCertificateRevision
{
    public PaymentCertificateRevisionId Id { get; init; } = null!;
    public PaymentCertificateId PaymentCertificateId { get; init; } = null!;
    public int RevisionNumber { get; set; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
