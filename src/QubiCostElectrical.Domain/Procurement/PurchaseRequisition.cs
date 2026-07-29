using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class PurchaseRequisition
{
    public PurchaseRequisitionId Id { get; init; } = null!;
    public string Number { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string? WbsNodeId { get; set; }
    public string? ActivityId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? Responsible { get; set; }
    public DateTime CreatedUtc { get; init; }
    public DateTime RequiredDate { get; set; }
    public PurchasePriority Priority { get; set; } = PurchasePriority.Normal;
    public PurchaseRequisitionStatus Status { get; set; } = PurchaseRequisitionStatus.Draft;
    public string Reason { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "CLP";
    public string Comment { get; set; } = string.Empty;
    public string? Approver { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public string? RejectionReason { get; set; }
    public NeedOriginKind PrimaryOrigin { get; set; } = NeedOriginKind.Manual;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<PurchaseRequisitionItem> Items { get; set; } = new();
}

public sealed class PurchaseRequisitionItem
{
    public string ItemId { get; init; } = null!;
    public PurchaseRequisitionId RequisitionId { get; init; } = null!;
    public string? ResourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SupplyCategoryKind Type { get; set; } = SupplyCategoryKind.Materials;
    public string Unit { get; set; } = "UN";
    public decimal RequiredQuantity { get; set; }
    public decimal CoveredQuantity { get; set; }
    public decimal BudgetedUnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public DateTime RequiredDate { get; set; }
    public string? ActivityId { get; set; }
    public string? WbsNodeId { get; set; }
    public string? BudgetItemId { get; set; }
    public string? ApuId { get; set; }
    public string? SuggestedSupplierId { get; set; }
    public string TechnicalSpec { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public PurchasePriority Criticidad { get; set; } = PurchasePriority.Normal;
    public RequisitionItemStatus Status { get; set; } = RequisitionItemStatus.Open;
    public NeedOrigin Origin { get; set; } = new();
    public decimal PendingQuantity => Math.Max(0m, RequiredQuantity - CoveredQuantity);
}

public sealed class NeedOrigin
{
    public NeedOriginKind Kind { get; set; } = NeedOriginKind.Manual;
    public string OriginId { get; set; } = string.Empty;
    public string OriginVersion { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public string User { get; set; } = string.Empty;
    public DateTime Utc { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public sealed class ProcurementNeedDatePolicy
{
    public int SafetyMarginDays { get; init; } = 5;
    public int DefaultLeadTimeDays { get; init; } = 14;

    public DateTime SuggestRequestDate(DateTime requiredDate, int? supplierLeadTimeDays)
    {
        var lead = supplierLeadTimeDays ?? DefaultLeadTimeDays;
        return requiredDate.Date.AddDays(-(lead + SafetyMarginDays));
    }
}
