using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class GoodsReceipt
{
    public GoodsReceiptId Id { get; init; } = null!;
    public PurchaseOrderId PurchaseOrderId { get; init; } = null!;
    public string ProjectPath { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public DateTime ReceiptDateUtc { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<GoodsReceiptLine> Lines { get; set; } = new();
    public List<ProcurementDocumentRef> Documents { get; set; } = new();
}

public sealed class GoodsReceiptLine
{
    public string LineId { get; init; } = null!;
    public GoodsReceiptId ReceiptId { get; init; } = null!;
    public string PurchaseOrderLineId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityAccepted { get; set; }
    public decimal QuantityRejected { get; set; }
    public decimal QuantityDamaged { get; set; }
    public decimal QuantityShortage { get; set; }
    public decimal QuantitySurplus { get; set; }
    public ReceiptLineDisposition Disposition { get; set; } = ReceiptLineDisposition.Accepted;
    public string Lot { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string InspectionNotes { get; set; } = string.Empty;
}

public sealed class GoodsReturn
{
    public GoodsReturnId Id { get; init; } = null!;
    public PurchaseOrderId PurchaseOrderId { get; init; } = null!;
    public GoodsReceiptId? ReceiptId { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public GoodsReturnStatus Status { get; set; } = GoodsReturnStatus.Draft;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<GoodsReturnLine> Lines { get; set; } = new();
}

public sealed class GoodsReturnLine
{
    public string LineId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Lot { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public sealed class ProjectInventoryBalance
{
    public InventoryItemId Id { get; init; } = null!;
    public string ProjectPath { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public string Lot { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyInTransit { get; set; }
    public decimal QtyAvailable => PrecisionPolicy.RoundCantidad(QtyOnHand - QtyReserved);
    public decimal QtyCommitted { get; set; }
    public DateTime ModifiedUtc { get; set; }
}

public sealed class InventoryMovement
{
    public string MovementId { get; init; } = null!;
    public string ProjectPath { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public InventoryMovementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public string Lot { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTime UtcTimestamp { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public static class InventoryEngine
{
    public static void ApplyReceiptAcceptance(ProjectInventoryBalance bal, GoodsReceiptLine line)
    {
        ArgumentNullException.ThrowIfNull(bal);
        ArgumentNullException.ThrowIfNull(line);
        bal.QtyOnHand = PrecisionPolicy.RoundCantidad(bal.QtyOnHand + line.QuantityAccepted);
        bal.ModifiedUtc = DateTime.UtcNow;
    }

    public static void ApplyReturn(ProjectInventoryBalance bal, decimal qty)
    {
        ArgumentNullException.ThrowIfNull(bal);
        if (qty <= 0m) throw new InvalidOperationException("InvalidReturnQty");
        if (bal.QtyAvailable < qty) throw new InvalidOperationException("InsufficientStock");
        bal.QtyOnHand = PrecisionPolicy.RoundCantidad(bal.QtyOnHand - qty);
        bal.ModifiedUtc = DateTime.UtcNow;
    }

    public static void Reserve(ProjectInventoryBalance bal, decimal qty)
    {
        ArgumentNullException.ThrowIfNull(bal);
        if (qty <= 0m) throw new InvalidOperationException("InvalidReserveQty");
        if (bal.QtyAvailable < qty) throw new InvalidOperationException("InsufficientAvailable");
        bal.QtyReserved = PrecisionPolicy.RoundCantidad(bal.QtyReserved + qty);
        bal.ModifiedUtc = DateTime.UtcNow;
    }

    public static void SetInTransit(ProjectInventoryBalance bal, decimal qty)
    {
        ArgumentNullException.ThrowIfNull(bal);
        bal.QtyInTransit = PrecisionPolicy.RoundCantidad(Math.Max(0m, bal.QtyInTransit + qty));
        bal.ModifiedUtc = DateTime.UtcNow;
    }

    public static decimal OrderedVsReceivedVariance(decimal ordered, decimal received) =>
        PrecisionPolicy.RoundCantidad(received - ordered);
}

public static class GoodsReceiptValidation
{
    public static List<ProcurementValidationIssue> Validate(GoodsReceipt receipt)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (receipt.Lines.Count == 0)
            issues.Add(new ProcurementValidationIssue
            {
                Code = "ReceiptNoLines", Message = "Recepción sin líneas.", Severity = ProcurementValidationSeverity.Error,
                EntityType = "GoodsReceipt", EntityId = receipt.Id.Value
            });
        foreach (var line in receipt.Lines)
        {
            if (line.QuantityReceived < 0m)
                issues.Add(new ProcurementValidationIssue
                {
                    Code = "InvalidReceivedQty", Message = "Cantidad recibida inválida.", Severity = ProcurementValidationSeverity.Error,
                    EntityType = "GoodsReceiptLine", EntityId = line.LineId
                });
            var accounted = line.QuantityAccepted + line.QuantityRejected + line.QuantityDamaged;
            if (accounted > line.QuantityReceived + line.QuantitySurplus)
                issues.Add(new ProcurementValidationIssue
                {
                    Code = "ReceiptQtyMismatch", Message = "Disposición supera cantidad recibida.", Severity = ProcurementValidationSeverity.Error,
                    EntityType = "GoodsReceiptLine", EntityId = line.LineId
                });
        }
        return issues;
    }
}
