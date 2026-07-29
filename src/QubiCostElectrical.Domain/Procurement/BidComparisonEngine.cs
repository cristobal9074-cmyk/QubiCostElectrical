using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class OfferCurrencyNormalizer
{
    public static decimal ToBase(decimal amountOriginal, string offerCurrency, string baseCurrency, decimal exchangeRate)
    {
        if (string.Equals(offerCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            return PrecisionPolicy.RoundImporte(amountOriginal);
        if (exchangeRate <= 0m) throw new InvalidOperationException("InvalidExchangeRate");
        return PrecisionPolicy.RoundImporte(amountOriginal * exchangeRate);
    }

    public static void NormalizeOffer(SupplierOffer offer, string baseCurrency, decimal exchangeRate)
    {
        ArgumentNullException.ThrowIfNull(offer);
        foreach (var line in offer.Lines)
        {
            line.UnitPriceBase = ToBase(line.UnitPriceOriginal, offer.CurrencyCode, baseCurrency, exchangeRate);
            line.LineTotalOriginal = PrecisionPolicy.RoundImporte(line.UnitPriceOriginal * line.Quantity);
            line.LineTotalBase = PrecisionPolicy.RoundImporte(line.UnitPriceBase * line.Quantity);
            foreach (var alt in line.Alternatives)
                alt.UnitPriceBase = ToBase(alt.UnitPriceOriginal, offer.CurrencyCode, baseCurrency, exchangeRate);
        }
    }
}

public static class AcquisitionCostEngine
{
    public static decimal ComputeLineAcquisitionCost(
        decimal lineTotalBase,
        decimal taxPercent,
        decimal discountPercent,
        decimal freightShareBase,
        decimal insuranceShareBase)
    {
        var afterDiscount = lineTotalBase * (1m - discountPercent / 100m);
        var withTax = afterDiscount * (1m + taxPercent / 100m);
        return PrecisionPolicy.RoundImporte(withTax + freightShareBase + insuranceShareBase);
    }

    public static decimal AllocateShare(decimal totalShare, decimal lineTotalBase, decimal offerLinesTotalBase)
    {
        if (offerLinesTotalBase <= 0m) return 0m;
        return PrecisionPolicy.RoundImporte(totalShare * (lineTotalBase / offerLinesTotalBase));
    }
}

public static class BidComparisonEngine
{
    public static BidComparison Build(
        RequestForQuotation rfq,
        IReadOnlyList<SupplierOffer> offers,
        IReadOnlyList<TechnicalEvaluation> technical,
        IReadOnlyList<CommercialEvaluation> commercial,
        string baseCurrency,
        decimal exchangeRate,
        DateTime exchangeRateDate,
        string actor,
        IReadOnlyDictionary<string, decimal>? budgetUnitPrices = null)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        ArgumentNullException.ThrowIfNull(offers);
        budgetUnitPrices ??= new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var comparison = new BidComparison
        {
            Id = BidComparisonId.New(),
            RfqId = rfq.Id,
            ProjectPath = rfq.ProjectPath,
            BaseCurrency = baseCurrency,
            ExchangeRate = exchangeRate,
            ExchangeRateDate = exchangeRateDate,
            Status = BidComparisonStatus.Draft,
            CreatedBy = actor,
            CreatedUtc = DateTime.UtcNow
        };

        foreach (var offer in offers)
            OfferCurrencyNormalizer.NormalizeOffer(offer, baseCurrency, exchangeRate);

        foreach (var rfqItem in rfq.Items)
        {
            var row = new BidComparisonRow
            {
                RowId = Guid.NewGuid().ToString("N"),
                RfqItemId = rfqItem.ItemId,
                Code = rfqItem.Code,
                Description = rfqItem.Description,
                Unit = rfqItem.Unit,
                RequestedQuantity = rfqItem.Quantity,
                BudgetUnitPriceBase = budgetUnitPrices.TryGetValue(rfqItem.Code, out var bp) ? bp : 0m
            };

            foreach (var offer in offers.Where(o => o.RfqId.Value == rfq.Id.Value && o.Status is not SupplierOfferStatus.Rejected and not SupplierOfferStatus.Archived))
            {
                var line = offer.Lines.FirstOrDefault(l => string.Equals(l.RfqItemId, rfqItem.ItemId, StringComparison.Ordinal)
                    || string.Equals(l.Code, rfqItem.Code, StringComparison.OrdinalIgnoreCase));
                if (line is null) continue;

                var linesTotal = offer.Lines.Sum(l => l.LineTotalBase);
                var freightShare = AcquisitionCostEngine.AllocateShare(
                    OfferCurrencyNormalizer.ToBase(offer.FreightAmount, offer.CurrencyCode, baseCurrency, exchangeRate),
                    line.LineTotalBase, linesTotal);
                var insuranceShare = AcquisitionCostEngine.AllocateShare(
                    OfferCurrencyNormalizer.ToBase(offer.InsuranceAmount, offer.CurrencyCode, baseCurrency, exchangeRate),
                    line.LineTotalBase, linesTotal);
                var discountAmt = PrecisionPolicy.RoundImporte(line.LineTotalBase * (offer.DiscountPercent / 100m));
                var taxAmt = PrecisionPolicy.RoundImporte((line.LineTotalBase - discountAmt) * (offer.TaxPercent / 100m));
                var tac = AcquisitionCostEngine.ComputeLineAcquisitionCost(
                    line.LineTotalBase, offer.TaxPercent, offer.DiscountPercent, freightShare, insuranceShare);
                var tech = technical.FirstOrDefault(t => t.OfferId.Value == offer.Id.Value && (t.OfferLineId is null || t.OfferLineId == line.LineId));
                var com = commercial.FirstOrDefault(c => c.OfferId.Value == offer.Id.Value);
                var budgetDev = row.BudgetUnitPriceBase <= 0m ? 0m
                    : PrecisionPolicy.RoundPorcentaje((line.UnitPriceBase - row.BudgetUnitPriceBase) / row.BudgetUnitPriceBase * 100m);

                row.Cells.Add(new BidOfferCell
                {
                    OfferId = offer.Id,
                    SupplierId = offer.SupplierId,
                    OfferLineId = line.LineId,
                    CurrencyOriginal = offer.CurrencyCode,
                    UnitPriceOriginal = line.UnitPriceOriginal,
                    UnitPriceBase = line.UnitPriceBase,
                    LineTotalBase = line.LineTotalBase,
                    TaxAmountBase = taxAmt,
                    DiscountAmountBase = discountAmt,
                    FreightShareBase = freightShare,
                    InsuranceShareBase = insuranceShare,
                    TotalAcquisitionCostBase = tac,
                    DeliveryLeadDays = offer.DeliveryLeadDays,
                    EstimatedDeliveryDate = DateTime.Today.AddDays(offer.DeliveryLeadDays),
                    TechnicalCompliance = tech?.Compliance ?? (line.MeetsSpecification ? TechnicalComplianceLevel.Compliant : TechnicalComplianceLevel.Partial),
                    DocumentComplete = tech?.DocumentComplete ?? offer.Documents.Count > 0,
                    ValidityUtc = offer.ValidityUtc,
                    PaymentTerms = offer.PaymentTerms,
                    Warranty = offer.Warranty,
                    Risk = com?.Risk ?? CommercialRiskLevel.Medium,
                    BudgetDeviationPercent = budgetDev,
                    RankScore = 0m
                });
            }

            RankCells(row);
            comparison.Rows.Add(row);
        }

        comparison.Recommendation = AwardRecommendationEngine.BuildDeterministic(comparison, actor);
        comparison.Status = BidComparisonStatus.RecommendationIssued;
        return comparison;
    }

    private static void RankCells(BidComparisonRow row)
    {
        foreach (var cell in row.Cells)
        {
            var techScore = cell.TechnicalCompliance switch
            {
                TechnicalComplianceLevel.Exceeds => 30m,
                TechnicalComplianceLevel.Compliant => 25m,
                TechnicalComplianceLevel.Partial => 10m,
                _ => 0m
            };
            var riskPenalty = cell.Risk switch
            {
                CommercialRiskLevel.Low => 0m,
                CommercialRiskLevel.Medium => 5m,
                CommercialRiskLevel.High => 15m,
                _ => 25m
            };
            var costScore = cell.TotalAcquisitionCostBase <= 0m ? 0m : Math.Max(0m, 50m - (cell.TotalAcquisitionCostBase / Math.Max(1m, row.Cells.Min(c => c.TotalAcquisitionCostBase)) * 10m));
            var leadScore = Math.Max(0m, 20m - cell.DeliveryLeadDays);
            cell.RankScore = PrecisionPolicy.RoundPorcentaje(techScore + costScore + leadScore - riskPenalty + (cell.DocumentComplete ? 5m : 0m));
        }

        var best = row.Cells.OrderByDescending(c => c.RankScore).ThenBy(c => c.TotalAcquisitionCostBase).FirstOrDefault();
        if (best is not null) best.IsRecommended = true;
    }
}

public static class AwardRecommendationEngine
{
    public static AwardRecommendation BuildDeterministic(BidComparison comparison, string actor)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var lines = new List<RecommendedAwardLine>();
        foreach (var row in comparison.Rows)
        {
            var best = row.Cells.FirstOrDefault(c => c.IsRecommended)
                ?? row.Cells.OrderByDescending(c => c.RankScore).FirstOrDefault();
            if (best is null) continue;
            lines.Add(new RecommendedAwardLine
            {
                RfqItemId = row.RfqItemId,
                Code = row.Code,
                OfferId = best.OfferId,
                SupplierId = best.SupplierId,
                Quantity = row.RequestedQuantity,
                UnitPriceBase = best.UnitPriceBase,
                Reason = $"Mayor puntaje ({best.RankScore}) y TAC {best.TotalAcquisitionCostBase}"
            });
        }

        var suppliers = lines.Select(l => l.SupplierId.Value).Distinct(StringComparer.Ordinal).Count();
        return new AwardRecommendation
        {
            RecommendationId = Guid.NewGuid().ToString("N"),
            SuggestedKind = suppliers > 1 ? AwardKind.MultiSupplier : AwardKind.Total,
            Summary = suppliers > 1
                ? $"Recomendación multi-proveedor ({suppliers} proveedores)."
                : "Recomendación de adjudicación total al mejor puntaje por ítem.",
            Lines = lines,
            EstimatedTotalBase = PrecisionPolicy.RoundImporte(lines.Sum(l => l.Quantity * l.UnitPriceBase)),
            IssuedUtc = DateTime.UtcNow,
            IssuedBy = actor,
            RequiresConfirmation = true
        };
    }
}
