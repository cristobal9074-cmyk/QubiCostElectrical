using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record EconomicConfig
{
	public IReadOnlyList<EconomicConcept> Concepts { get; init; } = Array.Empty<EconomicConcept>();

	public bool EnableCumulativePercentageRounding { get; init; } = true;

	public string EngineVersion { get; init; } = "1.0.0";

	public IReadOnlyList<EconomicConcept> GetActiveConcepts()
	{
		return (from c in Concepts
			where c.IsActive
			orderby c.Order
			select c).ToList().AsReadOnly();
	}

	public IReadOnlyList<EconomicConcept> GetByCategory(EconomicCategory category)
	{
		return (from c in Concepts
			where c.Category == category && c.IsActive
			orderby c.Order
			select c).ToList().AsReadOnly();
	}

	public static EconomicConfig CreateDefault()
	{
		List<EconomicConcept> list = new List<EconomicConcept>();
		int num = 1;
		list.Add(new EconomicConcept
		{
			Code = "INDIRECT",
			Name = "Costos indirectos",
			Category = EconomicCategory.IndirectCost,
			CalculationBase = CalculationBase.DirectCost,
			Value = 8m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "OVERHEAD",
			Name = "Gastos generales",
			Category = EconomicCategory.Overhead,
			CalculationBase = CalculationBase.DirectCost,
			Value = 14m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "CONTINGENCY",
			Name = "Contingencia",
			Category = EconomicCategory.Contingency,
			CalculationBase = CalculationBase.DirectCostPlusIndirect,
			Value = 3m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "RISK",
			Name = "Riesgo",
			Category = EconomicCategory.Risk,
			CalculationBase = CalculationBase.PreviousSubtotal,
			Value = 2m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "PROFIT",
			Name = "Utilidad",
			Category = EconomicCategory.Profit,
			CalculationBase = CalculationBase.SubtotalBeforeProfit,
			Value = 18m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "DISCOUNT",
			Name = "Descuento",
			Category = EconomicCategory.Discount,
			CalculationBase = CalculationBase.PriceBeforeDiscounts,
			Value = 0m,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "VAT",
			Name = "IVA",
			Category = EconomicCategory.Tax,
			CalculationBase = CalculationBase.PreviousSubtotal,
			Value = 19m,
			Order = num++,
			AffectsFinalPrice = true,
			RoundingDecimals = 2
		});
		list.Add(new EconomicConcept
		{
			Code = "WITHHOLDING",
			Name = "Retención contractual",
			Category = EconomicCategory.InformativeWithholding,
			CalculationBase = CalculationBase.FinalPreviousPrice,
			Value = 10m,
			Order = num++,
			AffectsFinalPrice = false,
			IsInformative = true
		});
		return new EconomicConfig
		{
			Concepts = list.AsReadOnly()
		};
	}

	public static EconomicConfig FromLegacyMarkup(MarkupPolicy markup)
	{
		if (markup == null)
		{
			return CreateDefault();
		}
		List<EconomicConcept> list = new List<EconomicConcept>();
		int num = 1;
		if (markup.RiesgoPct > 0m)
		{
			list.Add(new EconomicConcept
			{
				Code = "RISK",
				Name = "Riesgo",
				Category = EconomicCategory.Risk,
				CalculationBase = CalculationBase.DirectCost,
				Value = markup.RiesgoPct,
				Order = num++,
				AffectsFinalPrice = true
			});
		}
		list.Add(new EconomicConcept
		{
			Code = "OVERHEAD",
			Name = "Gastos generales",
			Category = EconomicCategory.Overhead,
			CalculationBase = ((!markup.PoliticaCostosExcel) ? CalculationBase.PreviousSubtotal : CalculationBase.DirectCost),
			Value = markup.GastosGeneralesPct,
			Order = num++,
			AffectsFinalPrice = true
		});
		if (markup.ValorAgregadoEquiposPct > 0m)
		{
			list.Add(new EconomicConcept
			{
				Code = "VA_EQUIP",
				Name = "VA equipos",
				Category = EconomicCategory.Surcharge,
				CalculationBase = CalculationBase.Equipment,
				Value = markup.ValorAgregadoEquiposPct,
				Order = num++,
				AffectsFinalPrice = true
			});
		}
		list.Add(new EconomicConcept
		{
			Code = "PROFIT",
			Name = "Utilidad",
			Category = EconomicCategory.Profit,
			CalculationBase = CalculationBase.PreviousSubtotal,
			Value = markup.UtilidadPct,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "DISCOUNT",
			Name = "Descuento",
			Category = EconomicCategory.Discount,
			CalculationBase = CalculationBase.PriceBeforeDiscounts,
			Value = markup.DescuentoPct,
			Order = num++,
			AffectsFinalPrice = true
		});
		list.Add(new EconomicConcept
		{
			Code = "VAT",
			Name = "IVA",
			Category = EconomicCategory.Tax,
			CalculationBase = CalculationBase.PreviousSubtotal,
			Value = markup.IvaPct,
			Order = num++,
			AffectsFinalPrice = true,
			RoundingDecimals = 2
		});
		return new EconomicConfig
		{
			Concepts = list.AsReadOnly(),
			EngineVersion = "1.0.0"
		};
	}
}
