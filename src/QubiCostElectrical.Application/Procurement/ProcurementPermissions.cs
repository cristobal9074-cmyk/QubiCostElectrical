namespace QubiCostElectrical;

public static class ProcurementPermissions
{
    public const string SupplierView = "Procurement.SupplierView";
    public const string SupplierCreate = "Procurement.SupplierCreate";
    public const string SupplierEdit = "Procurement.SupplierEdit";
    public const string SupplierDisable = "Procurement.SupplierDisable";
    public const string RequisitionView = "Procurement.RequisitionView";
    public const string RequisitionCreate = "Procurement.RequisitionCreate";
    public const string RequisitionEdit = "Procurement.RequisitionEdit";
    public const string RequisitionSubmit = "Procurement.RequisitionSubmit";
    public const string RequisitionApprove = "Procurement.RequisitionApprove";
    public const string RequisitionReject = "Procurement.RequisitionReject";
    public const string RfqView = "Procurement.RfqView";
    public const string RfqCreate = "Procurement.RfqCreate";
    public const string RfqEdit = "Procurement.RfqEdit";
    public const string RfqApprove = "Procurement.RfqApprove";
    public const string RfqIssue = "Procurement.RfqIssue";
    public const string QuotationResponseRegister = "Procurement.QuotationResponseRegister";
    public const string QuotationView = "Procurement.QuotationView";
    public const string QuotationRegister = "Procurement.QuotationRegister";
    public const string QuotationEvaluate = "Procurement.QuotationEvaluate";
    public const string CommercialComparisonView = "Procurement.CommercialComparisonView";
    public const string TechnicalEvaluationManage = "Procurement.TechnicalEvaluationManage";
    public const string AwardCreate = "Procurement.AwardCreate";
    public const string AwardApprove = "Procurement.AwardApprove";
    public const string AwardReject = "Procurement.AwardReject";
    public const string AwardAuditView = "Procurement.AwardAuditView";
    public const string PurchaseOrderView = "Procurement.PurchaseOrderView";
    public const string PurchaseOrderCreate = "Procurement.PurchaseOrderCreate";
    public const string PurchaseOrderEdit = "Procurement.PurchaseOrderEdit";
    public const string PurchaseOrderSubmit = "Procurement.PurchaseOrderSubmit";
    public const string PurchaseOrderApprove = "Procurement.PurchaseOrderApprove";
    public const string PurchaseOrderReject = "Procurement.PurchaseOrderReject";
    public const string PurchaseOrderIssue = "Procurement.PurchaseOrderIssue";
    public const string PurchaseOrderConfirm = "Procurement.PurchaseOrderConfirm";
    public const string PurchaseOrderCancel = "Procurement.PurchaseOrderCancel";
    public const string PurchaseOrderClose = "Procurement.PurchaseOrderClose";
    public const string PurchaseOrderExport = "Procurement.PurchaseOrderExport";
    public const string GoodsReceiptView = "Procurement.GoodsReceiptView";
    public const string GoodsReceiptCreate = "Procurement.GoodsReceiptCreate";
    public const string GoodsReceiptInspect = "Procurement.GoodsReceiptInspect";
    public const string GoodsReceiptAccept = "Procurement.GoodsReceiptAccept";
    public const string GoodsReturnManage = "Procurement.GoodsReturnManage";
    public const string InventoryView = "Procurement.InventoryView";
    public const string InventoryReserve = "Procurement.InventoryReserve";
    public const string InventoryAdjust = "Procurement.InventoryAdjust";
    public const string ProcurementCommitmentView = "Procurement.ProcurementCommitmentView";
    public const string ProcurementDashboardView = "Procurement.ProcurementDashboardView";
    public const string ProcurementAuditView = "Procurement.ProcurementAuditView";
    public const string ProcurementDocumentManage = "Procurement.ProcurementDocumentManage";
}

public interface IProcurementPermissionChecker
{
    bool Has(string permission);
}

public sealed class AllowAllProcurementPermissionChecker : IProcurementPermissionChecker
{
    public bool Has(string permission) => true;
}

public sealed class DenyAllProcurementPermissionChecker : IProcurementPermissionChecker
{
    public bool Has(string permission) => false;
}

public sealed class HashSetProcurementPermissionChecker : IProcurementPermissionChecker
{
    private readonly System.Collections.Generic.HashSet<string> _allowed;
    public HashSetProcurementPermissionChecker(System.Collections.Generic.IEnumerable<string> allowed) =>
        _allowed = new System.Collections.Generic.HashSet<string>(allowed, System.StringComparer.Ordinal);
    public bool Has(string permission) => _allowed.Contains(permission);
}

public sealed class ProcurementSoDPolicy
{
    public bool CreatorCannotApprove { get; init; } = true;
    public bool AwardCreatorCannotApprove { get; init; } = true;
    public bool PoCreatorCannotApprove { get; init; } = true;
}
