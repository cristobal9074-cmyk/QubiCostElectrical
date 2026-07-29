using System;

namespace QubiCostElectrical;

public sealed class PaymentObservation
{
    public PaymentObservationId Id { get; set; } = null!;
    public PaymentCertificateId PaymentCertificateId { get; set; } = null!;
    public PaymentCertificateLineId? PaymentCertificateLineId { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string Category { get; set; } = string.Empty; // Cantidad, Precio, Evidencia, Otro
    public string Severity { get; set; } = string.Empty; // Info, Advertencia, Critica
    public PaymentObservationStatus Status { get; set; } = PaymentObservationStatus.Abierta;
    public string Comment { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Responder { get; set; } = string.Empty;
    public DateTime? ResolvedUtc { get; set; }
}
