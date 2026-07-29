using System;

namespace QubiCostElectrical;

public sealed record SupplierId(string Value)
{
    public static SupplierId New() => new(Guid.NewGuid().ToString("N"));
    public static SupplierId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PurchaseRequisitionId(string Value)
{
    public static PurchaseRequisitionId New() => new(Guid.NewGuid().ToString("N"));
    public static PurchaseRequisitionId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record RequestForQuotationId(string Value)
{
    public static RequestForQuotationId New() => new(Guid.NewGuid().ToString("N"));
    public static RequestForQuotationId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record SupplierOfferId(string Value)
{
    public static SupplierOfferId New() => new(Guid.NewGuid().ToString("N"));
    public static SupplierOfferId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record BidComparisonId(string Value)
{
    public static BidComparisonId New() => new(Guid.NewGuid().ToString("N"));
    public static BidComparisonId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record AwardId(string Value)
{
    public static AwardId New() => new(Guid.NewGuid().ToString("N"));
    public static AwardId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PurchaseOrderId(string Value)
{
    public static PurchaseOrderId New() => new(Guid.NewGuid().ToString("N"));
    public static PurchaseOrderId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record GoodsReceiptId(string Value)
{
    public static GoodsReceiptId New() => new(Guid.NewGuid().ToString("N"));
    public static GoodsReceiptId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record GoodsReturnId(string Value)
{
    public static GoodsReturnId New() => new(Guid.NewGuid().ToString("N"));
    public static GoodsReturnId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record InventoryItemId(string Value)
{
    public static InventoryItemId New() => new(Guid.NewGuid().ToString("N"));
    public static InventoryItemId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}
