using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record EconomicConfig
{
    public IReadOnlyList<EconomicConcept> Concepts { get; init; } = Array.Empty<EconomicConcept>();
    public bool EnableCumulativePercentageRounding { get; init; } = true;
    public string EngineVersion { get; init; } = "1.0.0";

    public IReadOnlyList<EconomicConcept> GetActiveConcepts() =>
        Concepts.Where(c => c.IsActive).OrderBy(c => c.Order).ToList().AsReadOnly();

    public IReadOnlyList<EconomicConcept> GetByCategory(EconomicCategory category) =>
        Concepts.Where(c => c.Category == category && c.IsActive).OrderBy(c => c.Order).ToList().AsReadOnly();

    public static EconomicConfig CreateDefault()
    {
        var concepts = new List<EconomicConcept>();
        int order = 1;

        concepts.Add(new() { Code = "INDIRECT", Name = "Costos indirectos", Category = EconomicCategory.IndirectCost, CalculationBase = CalculationBase.DirectCost, Value = 8m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "OVERHEAD", Name = "Gastos generales", Category = EconomicCategory.Overhead, CalculationBase = CalculationBase.DirectCost, Value = 14m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "CONTINGENCY", Name = "Contingencia", Category = EconomicCategory.Contingency, CalculationBase = CalculationBase.DirectCostPlusIndirect, Value = 3m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "RISK", Name = "Riesgo", Category = EconomicCategory.Risk, CalculationBase = CalculationBase.PreviousSubtotal, Value = 2m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "PROFIT", Name = "Utilidad", Category = EconomicCategory.Profit, CalculationBase = CalculationBase.SubtotalBeforeProfit, Value = 18m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "DISCOUNT", Name = "Descuento", Category = EconomicCategory.Discount, CalculationBase = CalculationBase.PriceBeforeDiscounts, Value = 0m, Order = order++, AffectsFinalPrice = true });
        concepts.Add(new() { Code = "VAT", Name = "IVA", Category = EconomicCategory.Tax, CalculationBase = CalculationBase.PreviousSubtotal, Value = 19m, Order = order++, AffectsFinalPrice = true, RoundingDecimals = 2 });
        concepts.Add(new() { Code = "WITHHOLDING", Name = "Retención contractual", Category = EconomicCategory.InformativeWithholding, CalculationBase = CalculationBase.FinalPreviousPrice, Value = 10m, Order = order++, AffectsFinalPrice = false, IsInformative = true });

        return new EconomicConfig { Concepts = concepts.AsReadOnly() };
    }

    public static EconomicConfig FromLegacyMarkup(MarkupPolicy markup)
    {
        if (markup == null) return CreateDefault();

        var concepts = new List<EconomicConcept>();
        int order = 1;

        if (markup.RiesgoPct > 0m)
            concepts.Add(new() { Code = "RISK", Name = "Riesgo", Category = EconomicCategory.Risk, CalculationBase = CalculationBase.DirectCost, Value = markup.RiesgoPct, Order = order++, AffectsFinalPrice = true });

        concepts.Add(new() { Code = "OVERHEAD", Name = "Gastos generales", Category = EconomicCategory.Overhead, CalculationBase = markup.PoliticaCostosExcel ? CalculationBase.DirectCost : CalculationBase.PreviousSubtotal, Value = markup.GastosGeneralesPct, Order = order++, AffectsFinalPrice = true });

        if (markup.ValorAgregadoEquiposPct > 0m)
            concepts.Add(new() { Code = "VA_EQUIP", Name = "VA equipos", Category = EconomicCategory.Surcharge, CalculationBase = CalculationBase.Equipment, Value = markup.ValorAgregadoEquiposPct, Order = order++, AffectsFinalPrice = true });

        concepts.Add(new() { Code = "PROFIT", Name = "Utilidad", Category = EconomicCategory.Profit, CalculationBase = CalculationBase.PreviousSubtotal, Value = markup.UtilidadPct, Order = order++, AffectsFinalPrice = true });

        concepts.Add(new() { Code = "DISCOUNT", Name = "Descuento", Category = EconomicCategory.Discount, CalculationBase = CalculationBase.PriceBeforeDiscounts, Value = markup.DescuentoPct, Order = order++, AffectsFinalPrice = true });

        concepts.Add(new() { Code = "VAT", Name = "IVA", Category = EconomicCategory.Tax, CalculationBase = CalculationBase.PreviousSubtotal, Value = markup.IvaPct, Order = order++, AffectsFinalPrice = true, RoundingDecimals = 2 });

        return new EconomicConfig
        {
            Concepts = concepts.AsReadOnly(),
            EngineVersion = "1.0.0"
        };
    }
}
