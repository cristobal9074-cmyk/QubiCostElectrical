using System;

namespace QubiCostElectrical;

public sealed class ContractAdvance
{
    public ContractAdvanceId Id { get; set; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public string VisibleNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateTime Date { get; set; }
    public decimal AmortizationPercentage { get; set; }
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal TotalAmortized { get; set; }
    public string Status { get; set; } = "Active"; // Active, Cancelled, FullyAmortized
    public string DocumentReference { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public AdvanceType AdvanceType { get; set; } = AdvanceType.Financiero;
}

public sealed class AdvanceAmortization
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public PaymentCertificateId PaymentCertificateId { get; set; } = null!;
    public ContractAdvanceId ContractAdvanceId { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public decimal PreAmortizationBalance { get; set; }
    public decimal PostAmortizationBalance { get; set; }
}
