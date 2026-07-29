using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Agregados de compromiso presupuestario de compras (Fase 9E).</summary>
public sealed class ProcurementCommitmentSummary
{
    public string ProjectPath { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "CLP";
    public DateTime AsOfUtc { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal QuotedAmount { get; set; }
    public decimal AwardedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal BalanceAmount => PrecisionPolicy.RoundImporte(BudgetAmount - CommittedAmount);
    public decimal ForecastAmount { get; set; }
    public List<ProcurementCommitmentByWbs> ByWbs { get; set; } = new();
    public List<ProcurementCommitmentByResource> ByResource { get; set; } = new();
    public List<SupplierExposureRow> SupplierExposure { get; set; } = new();
    public List<CurrencyExposureRow> CurrencyExposure { get; set; } = new();
    public List<ProcurementAlert> Alerts { get; set; } = new();
    public string SnapshotHash { get; set; } = string.Empty;
}

public sealed class ProcurementCommitmentByWbs
{
    public string WbsCode { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal Committed { get; set; }
    public decimal Received { get; set; }
    public decimal Deviation => PrecisionPolicy.RoundImporte(Committed - Budget);
}

public sealed class ProcurementCommitmentByResource
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal Committed { get; set; }
    public decimal Received { get; set; }
    public decimal Deviation => PrecisionPolicy.RoundImporte(Committed - Budget);
}

public sealed class SupplierExposureRow
{
    public SupplierId SupplierId { get; init; } = null!;
    public decimal CommittedBase { get; set; }
    public decimal OpenOrders { get; set; }
    public int LateDeliveries { get; set; }
    public decimal PerformanceScore { get; set; }
}

public sealed class CurrencyExposureRow
{
    public string CurrencyCode { get; set; } = "CLP";
    public decimal AmountOriginal { get; set; }
    public decimal AmountBase { get; set; }
}

public sealed class ProcurementAlert
{
    public string AlertId { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
}

public static class ProcurementCommitmentEngine
{
    public static ProcurementCommitmentSummary Build(
        string projectPath,
        decimal budgetAmount,
        IReadOnlyList<PurchaseRequisition> requisitions,
        IReadOnlyList<SupplierOffer> offers,
        IReadOnlyList<AwardDecision> awards,
        IReadOnlyList<PurchaseOrder> orders,
        IReadOnlyList<GoodsReceipt> receipts,
        DateTime asOfUtc)
    {
        var summary = new ProcurementCommitmentSummary
        {
            ProjectPath = projectPath,
            AsOfUtc = asOfUtc,
            BudgetAmount = PrecisionPolicy.RoundImporte(budgetAmount),
            RequestedAmount = PrecisionPolicy.RoundImporte(requisitions.Sum(r => r.Items.Sum(i => i.BudgetedUnitPrice * i.RequiredQuantity))),
            QuotedAmount = PrecisionPolicy.RoundImporte(offers.Where(o => o.Status is SupplierOfferStatus.Submitted or SupplierOfferStatus.Compared or SupplierOfferStatus.Awarded or SupplierOfferStatus.PartiallyAwarded)
                .Sum(o => o.Lines.Sum(l => l.LineTotalBase != 0 ? l.LineTotalBase : l.UnitPriceOriginal * l.Quantity))),
            AwardedAmount = PrecisionPolicy.RoundImporte(awards.Where(a => a.Status == AwardStatus.Approved).Sum(a => a.BudgetImpactBase)),
            CommittedAmount = PrecisionPolicy.RoundImporte(orders.Where(o => o.Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.Issued or PurchaseOrderStatus.ConfirmedBySupplier or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
                .Sum(o => o.CommittedCostBase)),
            ReceivedAmount = EstimateReceived(orders, receipts)
        };

        summary.ForecastAmount = PrecisionPolicy.RoundImporte(summary.CommittedAmount + Math.Max(0m, summary.AwardedAmount - summary.CommittedAmount));

        foreach (var group in orders.SelectMany(o => o.Lines.Select(l => (o, l))).GroupBy(x => string.IsNullOrWhiteSpace(x.l.WbsCode) ? "(sin WBS)" : x.l.WbsCode))
        {
            var committed = group.Where(x => IsCommitted(x.o)).Sum(x => x.l.LineTotalBase);
            summary.ByWbs.Add(new ProcurementCommitmentByWbs { WbsCode = group.Key, Committed = PrecisionPolicy.RoundImporte(committed) });
        }

        foreach (var group in orders.SelectMany(o => o.Lines.Select(l => (o, l))).GroupBy(x => x.l.Code, StringComparer.OrdinalIgnoreCase))
        {
            var committed = group.Where(x => IsCommitted(x.o)).Sum(x => x.l.LineTotalBase);
            var first = group.First().l;
            summary.ByResource.Add(new ProcurementCommitmentByResource
            {
                Code = group.Key,
                Description = first.Description,
                Committed = PrecisionPolicy.RoundImporte(committed)
            });
        }

        foreach (var group in orders.Where(IsCommitted).GroupBy(o => o.SupplierId.Value))
        {
            var list = group.ToList();
            var late = list.Count(o => o.CommittedDeliveryDate is not null && o.CommittedDeliveryDate.Value.Date < asOfUtc.Date
                && o.Status is not PurchaseOrderStatus.Received and not PurchaseOrderStatus.Closed and not PurchaseOrderStatus.Cancelled);
            summary.SupplierExposure.Add(new SupplierExposureRow
            {
                SupplierId = SupplierId.From(group.Key),
                CommittedBase = PrecisionPolicy.RoundImporte(list.Sum(o => o.CommittedCostBase)),
                OpenOrders = list.Count(o => o.Status is not PurchaseOrderStatus.Closed and not PurchaseOrderStatus.Cancelled),
                LateDeliveries = late,
                PerformanceScore = PrecisionPolicy.RoundPorcentaje(100m - late * 10m)
            });
        }

        foreach (var group in orders.Where(IsCommitted).GroupBy(o => o.CurrencyCode, StringComparer.OrdinalIgnoreCase))
        {
            summary.CurrencyExposure.Add(new CurrencyExposureRow
            {
                CurrencyCode = group.Key,
                AmountOriginal = PrecisionPolicy.RoundImporte(group.Sum(o => o.CommittedCostOriginal)),
                AmountBase = PrecisionPolicy.RoundImporte(group.Sum(o => o.CommittedCostBase))
            });
        }

        BuildAlerts(summary, orders, asOfUtc);
        summary.SnapshotHash = ComputeHash(summary);
        return summary;
    }

    private static bool IsCommitted(PurchaseOrder o) =>
        o.Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.Issued or PurchaseOrderStatus.ConfirmedBySupplier
            or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed;

    private static decimal EstimateReceived(IReadOnlyList<PurchaseOrder> orders, IReadOnlyList<GoodsReceipt> receipts)
    {
        var byPoLine = orders.SelectMany(o => o.Lines.Select(l => (o, l)))
            .ToDictionary(x => x.l.LineId, x => x.l.UnitPriceBase, StringComparer.Ordinal);
        decimal total = 0m;
        foreach (var receipt in receipts.Where(r => r.Status is GoodsReceiptStatus.Accepted or GoodsReceiptStatus.PartiallyAccepted))
        {
            foreach (var line in receipt.Lines)
            {
                var price = byPoLine.TryGetValue(line.PurchaseOrderLineId, out var p) ? p : 0m;
                total += price * line.QuantityAccepted;
            }
        }
        return PrecisionPolicy.RoundImporte(total);
    }

    private static void BuildAlerts(ProcurementCommitmentSummary summary, IReadOnlyList<PurchaseOrder> orders, DateTime asOfUtc)
    {
        if (summary.CommittedAmount > summary.BudgetAmount && summary.BudgetAmount > 0m)
            summary.Alerts.Add(new ProcurementAlert { AlertId = Guid.NewGuid().ToString("N"), Code = "OverBudget", Severity = "High", Message = "Comprometido supera presupuesto." });
        foreach (var o in orders.Where(o => o.CommittedDeliveryDate is not null && o.CommittedDeliveryDate.Value.Date < asOfUtc.Date
                     && o.Status is PurchaseOrderStatus.Issued or PurchaseOrderStatus.ConfirmedBySupplier or PurchaseOrderStatus.PartiallyReceived))
            summary.Alerts.Add(new ProcurementAlert
            {
                AlertId = Guid.NewGuid().ToString("N"), Code = "LateDelivery", Severity = "Warning",
                Message = $"Entrega atrasada OC {o.Number}", EntityType = "PurchaseOrder", EntityId = o.Id.Value
            });
        foreach (var row in summary.ByResource.Where(r => r.Committed > 0m).OrderByDescending(r => r.Committed).Take(3))
            summary.Alerts.Add(new ProcurementAlert
            {
                AlertId = Guid.NewGuid().ToString("N"), Code = "CriticalMaterial", Severity = "Info",
                Message = $"Material crítico por compromiso: {row.Code}"
            });
    }

    private static string ComputeHash(ProcurementCommitmentSummary s)
    {
        var payload = $"{s.BudgetAmount:0.00}|{s.RequestedAmount:0.00}|{s.QuotedAmount:0.00}|{s.AwardedAmount:0.00}|{s.CommittedAmount:0.00}|{s.ReceivedAmount:0.00}|{s.ForecastAmount:0.00}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
