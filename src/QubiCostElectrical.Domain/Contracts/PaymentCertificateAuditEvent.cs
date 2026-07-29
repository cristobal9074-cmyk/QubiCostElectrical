using System;

namespace QubiCostElectrical;

public sealed class PaymentCertificateAuditEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public PaymentCertificateId PaymentCertificateId { get; init; } = null!;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
}
