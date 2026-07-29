using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ContractGuarantee
{
    public GuaranteeId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public int VisibleNumber { get; set; }
    public GuaranteeType GuaranteeType { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public GuaranteeStatus Status { get; set; }
    public List<GuaranteeRenewal> Renewals { get; set; } = new();
    public string SupportingDocuments { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
}

public sealed class GuaranteeRenewal
{
    public string Id { get; set; } = string.Empty;
    public string NewNumber { get; set; } = string.Empty;
    public DateTime NewExpiryDate { get; set; }
    public decimal NewAmount { get; set; }
    public DateTime RenewalDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RenewedBy { get; set; } = string.Empty;
}
