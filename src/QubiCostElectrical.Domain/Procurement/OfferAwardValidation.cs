using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class OfferAwardValidation
{
    public static List<ProcurementValidationIssue> ValidateOffer(SupplierOffer offer)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (offer.Lines.Count == 0)
            issues.Add(Err("InvalidQuotationResponse", "Oferta sin ítems.", "Offer", "Lines", offer.Id.Value));
        if (offer.ValidityUtc is not null && offer.ValidityUtc < offer.OfferDateUtc)
            issues.Add(Err("InvalidRfqDeadline", "Vigencia anterior a fecha de oferta.", "Offer", "ValidityUtc", offer.Id.Value));
        foreach (var line in offer.Lines)
        {
            if (line.Quantity <= 0m)
                issues.Add(Err("InvalidRequiredQuantity", "Cantidad ofertada inválida.", "OfferLine", "Quantity", line.LineId));
            if (line.UnitPriceOriginal < 0m)
                issues.Add(Err("InvalidQuotationResponse", "Precio unitario negativo.", "OfferLine", "UnitPriceOriginal", line.LineId));
        }
        if (offer.TaxPercent < 0m || offer.DiscountPercent < 0m || offer.DiscountPercent > 100m)
            issues.Add(Err("InvalidQuotationResponse", "Impuesto/descuento inválido.", "Offer", "TaxPercent", offer.Id.Value));
        return issues;
    }

    public static List<ProcurementValidationIssue> ValidateAward(AwardDecision award)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (award.Items.Count == 0)
            issues.Add(Err("RfqNotFound", "Adjudicación sin ítems.", "Award", "Items", award.Id.Value));
        foreach (var group in award.Items.GroupBy(i => i.RfqItemId ?? i.Code))
        {
            if (group.Count() > 1 && award.Kind is AwardKind.Total)
                issues.Add(Err("ConsolidationConflict", "Adjudicación total con ítem duplicado.", "Award", "Items", group.Key));
        }
        foreach (var item in award.Items.Where(i => i.Quantity <= 0m))
            issues.Add(Err("InvalidRequiredQuantity", "Cantidad adjudicada inválida.", "AwardedItem", "Quantity", item.AwardedItemId));
        return issues;
    }

    private static ProcurementValidationIssue Err(string code, string msg, string entity, string field, string? id) =>
        new() { Code = code, Message = msg, Severity = ProcurementValidationSeverity.Error, EntityType = entity, Field = field, EntityId = id };
}
