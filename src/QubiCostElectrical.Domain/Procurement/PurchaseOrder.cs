using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class PurchaseOrder
{
    public PurchaseOrderId Id { get; init; } = null!;
    public string Number { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public AwardId? AwardId { get; set; }
    public SupplierId SupplierId { get; init; } = null!;
    public RequestForQuotationId? RfqId { get; set; }
    public int Version { get; set; } = 1;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string CurrencyCode { get; set; } = "CLP";
    public decimal ExchangeRate { get; set; } = 1m;
    public DateTime ExchangeRateDate { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Warranty { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public DateTime? CommittedDeliveryDate { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateTime? ConfirmedUtc { get; set; }
    public string? SupplierConfirmationRef { get; set; }
    public decimal CommittedCostOriginal { get; set; }
    public decimal CommittedCostBase { get; set; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string? Approver { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public string? CancellationReason { get; set; }
    public string? CloseNotes { get; set; }
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public List<PurchaseOrderLine> Lines { get; set; } = new();
    public List<PurchaseOrderCondition> CommercialConditions { get; set; } = new();
    public List<PurchaseOrderVersionSnapshot> Versions { get; set; } = new();
    public List<ProcurementDocumentRef> Documents { get; set; } = new();
}

public sealed class PurchaseOrderLine
{
    public string LineId { get; init; } = null!;
    public PurchaseOrderId PurchaseOrderId { get; init; } = null!;
    public string? AwardedItemId { get; set; }
    public string? RfqItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "UN";
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPriceOriginal { get; set; }
    public decimal UnitPriceBase { get; set; }
    public decimal LineTotalOriginal { get; set; }
    public decimal LineTotalBase { get; set; }
    public DateTime? CommittedDate { get; set; }
    public string WbsCode { get; set; } = string.Empty;
}

public sealed class PurchaseOrderCondition
{
    public string ConditionId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class PurchaseOrderVersionSnapshot
{
    public string VersionId { get; init; } = null!;
    public int VersionNumber { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string JsonSnapshot { get; set; } = string.Empty;
}

public static class PurchaseOrderCommittedCostEngine
{
    public static (decimal original, decimal baseCost) Compute(PurchaseOrder po, string baseCurrency = "CLP")
    {
        ArgumentNullException.ThrowIfNull(po);
        var linesOriginal = po.Lines.Sum(l => PrecisionPolicy.RoundImporte(l.UnitPriceOriginal * l.QuantityOrdered));
        var afterDisc = linesOriginal * (1m - po.DiscountPercent / 100m);
        var withTax = afterDisc * (1m + po.TaxPercent / 100m);
        var original = PrecisionPolicy.RoundImporte(withTax + po.FreightAmount + po.InsuranceAmount);
        var rate = po.ExchangeRate <= 0m ? 1m : po.ExchangeRate;
        var baseCost = OfferCurrencyNormalizer.ToBase(original, po.CurrencyCode, baseCurrency, rate);
        return (original, baseCost);
    }

    public static void RecalculateLinesAndTotals(PurchaseOrder po, string baseCurrency, decimal rate)
    {
        ArgumentNullException.ThrowIfNull(po);
        po.ExchangeRate = rate <= 0m ? 1m : rate;
        foreach (var line in po.Lines)
        {
            line.UnitPriceBase = OfferCurrencyNormalizer.ToBase(line.UnitPriceOriginal, po.CurrencyCode, baseCurrency, po.ExchangeRate);
            line.LineTotalOriginal = PrecisionPolicy.RoundImporte(line.UnitPriceOriginal * line.QuantityOrdered);
            line.LineTotalBase = PrecisionPolicy.RoundImporte(line.UnitPriceBase * line.QuantityOrdered);
        }
        var (o, b) = Compute(po, baseCurrency);
        po.CommittedCostOriginal = o;
        po.CommittedCostBase = b;
    }
}

public static class PurchaseOrderValidation
{
    public static List<ProcurementValidationIssue> Validate(PurchaseOrder po)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (po.Lines.Count == 0)
            issues.Add(Err("PoNoLines", "OC sin ítems.", po.Id.Value));
        if (po.ExchangeRate <= 0m)
            issues.Add(Err("InvalidExchangeRate", "Tipo de cambio inválido.", po.Id.Value));
        if (po.TaxPercent < 0m || po.DiscountPercent < 0m || po.DiscountPercent > 100m)
            issues.Add(Err("InvalidTaxDiscount", "Impuesto/descuento inválido.", po.Id.Value));
        foreach (var line in po.Lines.Where(l => l.QuantityOrdered <= 0m))
            issues.Add(Err("InvalidRequiredQuantity", "Cantidad ordenada inválida.", line.LineId));
        foreach (var line in po.Lines.Where(l => l.UnitPriceOriginal < 0m))
            issues.Add(Err("InvalidUnitPrice", "Precio unitario negativo.", line.LineId));
        return issues;
    }

    private static ProcurementValidationIssue Err(string code, string msg, string? id) =>
        new() { Code = code, Message = msg, Severity = ProcurementValidationSeverity.Error, EntityType = "PurchaseOrder", EntityId = id };
}
