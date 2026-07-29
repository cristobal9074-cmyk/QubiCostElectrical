using System;

namespace QubiCostElectrical;

public sealed class ContractRetention
{
    public ContractRetentionId Id { get; set; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public RetentionType RetentionType { get; set; } = RetentionType.Contractual;
    public string BaseType { get; set; } = "Subtotal"; // Subtotal, MontoBruto
    public decimal Percentage { get; set; }
    public decimal FixedAmount { get; set; }
    public decimal LimitAmount { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string ReleaseCondition { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Released, Cancelled
    public string Observation { get; set; } = string.Empty;
}

public sealed class RetentionApplication
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public PaymentCertificateId PaymentCertificateId { get; set; } = null!;
    public ContractRetentionId ContractRetentionId { get; set; } = null!;
    public decimal BaseAmount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal CumulativeApplied { get; set; }
    public decimal ReleasedAmount { get; set; }
    public decimal PendingBalance { get; set; }
}

public sealed class RetentionRelease
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ContractRetentionId ContractRetentionId { get; set; } = null!;
    public PaymentCertificateId? PaymentCertificateId { get; set; }
    public decimal ReleasedAmount { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string AuthorizedBy { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
