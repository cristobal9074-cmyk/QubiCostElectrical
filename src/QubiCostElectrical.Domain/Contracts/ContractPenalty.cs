using System;

namespace QubiCostElectrical;

public sealed class ContractPenalty
{
    public PenaltyId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public int VisibleNumber { get; set; }
    public PenaltyType PenaltyType { get; set; }
    public string Cause { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public PenaltyStatus Status { get; set; }
    public string Observations { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
}
