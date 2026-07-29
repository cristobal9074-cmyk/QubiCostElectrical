using System;

namespace QubiCostElectrical;

public sealed class PaymentCertificateLine
{
    public PaymentCertificateLineId Id { get; set; } = null!;
    public PaymentCertificateId PaymentCertificateId { get; set; } = null!;
    public string PartidaContractual { get; set; } = string.Empty;
    public string WbsCode { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string? ApuId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CantidadContractual { get; set; }
    public decimal PrecioUnitarioContractual { get; set; }
    public decimal MontoContractual => CantidadContractual * PrecioUnitarioContractual;
    
    public decimal CantidadAnterior { get; set; }
    public decimal CantidadPeriodo { get; set; }
    public decimal CantidadAcumulada { get; set; }
    public decimal SaldoCantidad { get; set; }
    
    public decimal MontoAnterior { get; set; }
    public decimal MontoPeriodo { get; set; }
    public decimal MontoAcumulado { get; set; }
    public decimal SaldoMonetario { get; set; }
    
    public string OrigenAvance { get; set; } = string.Empty; // Planificacion, Manual, ControlObra
    public string Evidencia { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Observed, Approved
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
}
