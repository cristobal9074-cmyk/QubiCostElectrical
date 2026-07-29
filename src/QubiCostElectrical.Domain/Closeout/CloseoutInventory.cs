using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class InventoryReconciliation
{
    public InventoryCloseoutId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ReconciliationDate { get; set; } = string.Empty;
    public decimal TotalPurchased { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal TotalConsumed { get; set; }
    public decimal TotalReturned { get; set; }
    public decimal TotalSurplus { get; set; }
    public decimal TotalDifferences { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string DifferencesDetail { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public InventoryCloseoutStatus Status { get; set; } = InventoryCloseoutStatus.Pending;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SurplusDisposal
{
    public SurplusDisposalId Id { get; init; } = null!;
    public InventoryCloseoutId InventoryId { get; set; } = null!;
    public string ResourceCode { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public SurplusDisposalType DisposalType { get; set; }
    public decimal Cost { get; set; }
    public string Responsible { get; set; } = string.Empty;
    public string Authorization { get; set; } = string.Empty;
    public string EvidenceRef { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
