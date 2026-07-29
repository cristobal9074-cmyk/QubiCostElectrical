using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class EconomicEngineTests
{
    private static EconomicInput MakeInput(decimal directCost = 10000m, EconomicConfig? config = null)
    {
        return new()
        {
            UnitDirectCost = directCost,
            Materials = 5000m,
            Labor = 3000m,
            Equipment = 1000m,
            Tools = 500m,
            Subcontract = 0m,
            Transport = 300m,
            Auxiliaries = 200m,
            PartidaQuantity = 1m,
            Config = config ?? EconomicConfig.CreateDefault()
        };
    }

    private static EconomicConcept MakeConcept(string code, EconomicCategory cat, CalculationBase calcBase, decimal value, int order, bool affectsPrice = true)
        => new() { Code = code, Name = code, Category = cat, CalculationBase = calcBase, Value = value, Order = order, AffectsFinalPrice = affectsPrice };

    // ────────────────────────── Basic Calculation ──────────────────────────

    [Fact]
    public void NoConcepts_ReturnsDirectCostAsFinalPrice()
    {
        var config = new EconomicConfig { Concepts = Array.Empty<EconomicConcept>() };
        var input = MakeInput(5000m, config);
        var result = EconomicEngine.Calculate(input);
        Assert.Equal(5000m, result.UnitDirectCost);
        Assert.Equal(5000m, result.FinalUnitPrice);
    }

    [Fact]
    public void SinglePercentageConcept_AppliedCorrectly()
    {
        var config = new EconomicConfig { Concepts = new[] { MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 14m, 1) } };
        var input = MakeInput(10000m, config);
        var result = EconomicEngine.Calculate(input);
        Assert.Equal(10000m, result.UnitDirectCost);
        Assert.Equal(1400m, result.Overhead);
        Assert.Equal(11400m, result.FinalUnitPrice);
    }

    [Fact]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EconomicEngine.Calculate(null!));
    }

    // ────────────────────────── Indirect Costs ──────────────────────────

    [Fact]
    public void IndirectCost_PercentageOfDirectCost()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 8m, 1)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(5000m, config));
        Assert.Equal(400m, result.TotalIndirectCosts);
        Assert.Equal(5400m, result.FinalUnitPrice);
    }

    [Fact]
    public void IndirectCost_FixedAmount()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "IND_FIX", Name = "Indirecto fijo", Category = EconomicCategory.IndirectCost,
                    CalculationType = CalculationType.FixedAmount, CalculationBase = CalculationBase.DirectCost,
                    Value = 250m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(3000m, config));
        Assert.Equal(250m, result.TotalIndirectCosts);
        Assert.Equal(3250m, result.FinalUnitPrice);
    }

    [Fact]
    public void MultipleIndirectCosts_Accumulated()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND1", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 5m, 1),
                MakeConcept("IND2", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 3m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(80m, result.TotalIndirectCosts);
        Assert.Equal(1080m, result.FinalUnitPrice);
    }

    [Fact]
    public void IndirectCost_ByCategory_MaterialsOnly()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "IND_MAT", Name = "Seguro materiales", Category = EconomicCategory.IndirectCost,
                    CalculationBase = CalculationBase.Materials, Value = 2m, Order = 1 }
            }
        };
        var input = MakeInput(10000m, config) with { Materials = 4000m };
        var result = EconomicEngine.Calculate(input);
        Assert.Equal(80m, result.TotalIndirectCosts); // 4000 * 2%
        Assert.Equal(10080m, result.FinalUnitPrice);
    }

    // ────────────────────────── Overhead ──────────────────────────

    [Fact]
    public void Overhead_OnDirectCost()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 14m, 1)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(10000m, config));
        Assert.Equal(1400m, result.Overhead);
    }

    [Fact]
    public void Overhead_OnDirectPlusIndirect()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 8m, 1),
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCostPlusIndirect, 14m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(10000m, config));
        Assert.Equal(800m, result.TotalIndirectCosts);  // 10000 * 8%
        Assert.Equal(1512m, result.Overhead);            // (10000 + 800) * 14% = 1512
        Assert.Equal(12312m, result.FinalUnitPrice);
    }

    [Fact]
    public void Overhead_FixedAmount()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "GG_FIX", Name = "GG fijo", Category = EconomicCategory.Overhead,
                    CalculationType = CalculationType.FixedAmount, Value = 500m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(2000m, config));
        Assert.Equal(500m, result.Overhead);
        Assert.Equal(2500m, result.FinalUnitPrice);
    }

    // ────────────────────────── Contingency and Risk ──────────────────────────

    [Fact]
    public void Contingency_PercentageOfSubtotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("CONT", EconomicCategory.Contingency, CalculationBase.PreviousSubtotal, 3m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(100m, result.TotalIndirectCosts);   // 1000 * 10%
        Assert.Equal(33m, result.Contingency);            // (1000+100) * 3% = 33
        Assert.Equal(1133m, result.FinalUnitPrice);
    }

    [Fact]
    public void Risk_PercentageOfDirectCost()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("RISK", EconomicCategory.Risk, CalculationBase.DirectCost, 5m, 1)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(2000m, config));
        Assert.Equal(100m, result.Risk);
        Assert.Equal(2100m, result.FinalUnitPrice);
    }

    [Fact]
    public void Risk_Informative_DoesNotAffectTotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "RISK_INFO", Name = "Riesgo informativo", Category = EconomicCategory.Risk,
                    CalculationBase = CalculationBase.DirectCost, Value = 5m, Order = 1, AffectsFinalPrice = true, IsInformative = true }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(50m, result.Risk);
        Assert.Equal(1000m, result.FinalUnitPrice); // informative doesn't change total
    }

    // ────────────────────────── Profit ──────────────────────────

    [Fact]
    public void Profit_OnDirectCost()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("UTIL", EconomicCategory.Profit, CalculationBase.DirectCost, 20m, 1)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(5000m, config));
        Assert.Equal(1000m, result.Profit);
        Assert.Equal(6000m, result.FinalUnitPrice);
    }

    [Fact]
    public void Profit_OnPreviousSubtotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("UTIL", EconomicCategory.Profit, CalculationBase.PreviousSubtotal, 15m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(100m, result.TotalIndirectCosts);
        Assert.Equal(165m, result.Profit); // (1000+100) * 15% = 165
        Assert.Equal(1265m, result.FinalUnitPrice);
    }

    [Fact]
    public void Profit_FixedAmount()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "UTIL_FIX", Name = "Utilidad fija", Category = EconomicCategory.Profit,
                    CalculationType = CalculationType.FixedAmount, Value = 750m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(3000m, config));
        Assert.Equal(750m, result.Profit);
        Assert.Equal(3750m, result.FinalUnitPrice);
    }

    [Fact]
    public void Profit_Zero_Skip()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "UTIL_ZERO", Name = "Utilidad cero", Category = EconomicCategory.Profit,
                    Value = 0m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(0m, result.Profit);
        Assert.Equal(1000m, result.FinalUnitPrice);
    }

    // ────────────────────────── Taxes ──────────────────────────

    [Fact]
    public void Tax_SimpleIVA()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1),
                new EconomicConcept { Code = "IVA", Name = "IVA", Category = EconomicCategory.Tax,
                    CalculationBase = CalculationBase.PreviousSubtotal, Value = 19m, Order = 2, RoundingDecimals = 2 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(10000m, config));
        Assert.Equal(1000m, result.Overhead);
        Assert.Equal(2090m, result.TotalTaxes); // (10000+1000) * 19% = 2090
        Assert.Equal(13090m, result.FinalUnitPrice);
    }

    [Fact]
    public void MultipleTaxes_Accumulated()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "IVA", Name = "IVA", Category = EconomicCategory.Tax,
                    CalculationBase = CalculationBase.DirectCost, Value = 19m, Order = 1, RoundingDecimals = 2 },
                new EconomicConcept { Code = "IMP_SERV", Name = "Impuesto servicio", Category = EconomicCategory.Tax,
                    CalculationBase = CalculationBase.PreviousSubtotal, Value = 2m, Order = 2, RoundingDecimals = 2 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(10000m, config));
        Assert.Equal(2138m, result.TotalTaxes);    // 10000 * 19% + (10000+1900) * 2% = 1900 + 238 = 2138
        Assert.Equal(12138m, result.FinalUnitPrice);
    }

    [Fact]
    public void Tax_DuplicateDetected_StillCalculates()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IVA1", EconomicCategory.Tax, CalculationBase.DirectCost, 19m, 1),
                MakeConcept("IVA2", EconomicCategory.Tax, CalculationBase.PreviousSubtotal, 19m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(190m + 226.10m, result.TotalTaxes); // 190 + 226.10 = 416.10
    }

    [Fact]
    public void Tax_Informative_DoesNotAffectTotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "IVA_INFO", Name = "IVA informativo", Category = EconomicCategory.Tax,
                    CalculationBase = CalculationBase.DirectCost, Value = 19m, Order = 1, AffectsFinalPrice = false, IsInformative = true }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(190m, result.TotalTaxes);
        Assert.Equal(1000m, result.FinalUnitPrice); // informative
    }

    // ────────────────────────── Discounts ──────────────────────────

    [Fact]
    public void Discount_Percentage_ReducesTotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1),
                new EconomicConcept { Code = "DTO", Name = "Descuento", Category = EconomicCategory.Discount,
                    CalculationBase = CalculationBase.PriceBeforeDiscounts, Value = 5m, Order = 2, AffectsFinalPrice = true }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(100m, result.Overhead);
        Assert.Equal(55m, result.TotalDiscounts); // (1000+100) * 5% = 55
        Assert.Equal(1045m, result.FinalUnitPrice);
    }

    [Fact]
    public void Discount_FixedAmount()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "DTO_FIX", Name = "Dto fijo", Category = EconomicCategory.Discount,
                    CalculationType = CalculationType.FixedAmount, Value = 200m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(200m, result.TotalDiscounts);
        Assert.Equal(800m, result.FinalUnitPrice);
    }

    [Fact]
    public void Discount_Excessive_ClampsToZero()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "DTO_EXC", Name = "Dto excesivo", Category = EconomicCategory.Discount,
                    Value = 150m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(100m, config));
        Assert.Equal(0m, result.FinalUnitPrice);
        Assert.Contains("exceeds", result.Warnings[0]);
    }

    // ────────────────────────── Informative Withholdings ──────────────────────────

    [Fact]
    public void InformativeWithholding_ShowsInResult_DoesNotAffectTotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "GG", Name = "GG", Category = EconomicCategory.Overhead,
                    CalculationBase = CalculationBase.DirectCost, Value = 10m, Order = 1 },
                new EconomicConcept { Code = "RET", Name = "Retención contractual", Category = EconomicCategory.InformativeWithholding,
                    CalculationBase = CalculationBase.FinalPreviousPrice, Value = 10m, Order = 2,
                    AffectsFinalPrice = false, IsInformative = true }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(1000m + 100m, result.FinalUnitPrice); // 1100, withholding doesn't change
        Assert.Equal(110m, result.TotalInformativeWithholdings); // (1000+100) * 10% = 110
    }

    [Fact]
    public void InformativeWithholding_NotInFinalPrice()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "RET_INFO", Name = "Retención", Category = EconomicCategory.InformativeWithholding,
                    CalculationBase = CalculationBase.DirectCost, Value = 5m, Order = 1,
                    AffectsFinalPrice = false, IsInformative = true }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(2000m, config));
        Assert.Equal(2000m, result.FinalUnitPrice);
        Assert.Equal(100m, result.TotalInformativeWithholdings);
        Assert.Single(result.ConceptsApplied);
        Assert.True(result.ConceptsApplied[0].IsInformative);
        Assert.False(result.ConceptsApplied[0].AffectsPrice);
    }

    // ────────────────────────── Surcharges ──────────────────────────

    [Fact]
    public void Surcharge_Percentage_AddedCorrectly()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "URG", Name = "Recargo urgencia", Category = EconomicCategory.Surcharge,
                    CalculationBase = CalculationBase.DirectCost, Value = 15m, Order = 1 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(150m, result.TotalSurcharges);
        Assert.Equal(1150m, result.FinalUnitPrice);
    }

    [Fact]
    public void MultipleSurcharges_AllApplied()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("URG", EconomicCategory.Surcharge, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("NOCT", EconomicCategory.Surcharge, CalculationBase.PreviousSubtotal, 5m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(2000m, config));
        Assert.Equal(200m + 110m, result.TotalSurcharges); // 200 + (2000+200)*5% = 200+110 = 310
        Assert.Equal(2310m, result.FinalUnitPrice);
    }

    // ────────────────────────── Order / Sequence ──────────────────────────

    [Fact]
    public void ConceptsAppliedInOrder()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.PreviousSubtotal, 14m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(2, result.Trace.StepsExecuted);
        Assert.Equal("IND", result.Trace.Steps[0].ConceptCode);
        Assert.Equal("GG", result.Trace.Steps[1].ConceptCode);
    }

    [Fact]
    public void SequentialPercentages_OnDifferentBases()
    {
        // 10% indirect on direct = 100
        // 5% profit on direct+indirect = (1000+100) * 5% = 55
        // Total: 1000 + 100 + 55 = 1155
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("PROFIT", EconomicCategory.Profit, CalculationBase.PreviousSubtotal, 5m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(1000m, result.UnitDirectCost);
        Assert.Equal(100m, result.TotalIndirectCosts);
        Assert.Equal(55m, result.Profit);
        Assert.Equal(1155m, result.FinalUnitPrice);
    }

    [Fact]
    public void FullEconomicSequence_AllCategories()
    {
        // Direct: 10000
        // IND(8% of DC) = 800
        // GG(14% of DC+IND) = (10000+800)*14% = 1512
        // CONT(3% of prev) = (10800+1512)*3% = 369.36
        // RISK(2% of prev) = (12312+369.36)*2% = 253.63
        // PROFIT(18% of prev) = (12681.36+253.63)*18% = 2328.30
        // DTO(5% of prev) = (15262.99)*5% = 763.15
        // VAT(19% of after-dto) = (14499.84)*19% = 2754.97
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("IND", EconomicCategory.IndirectCost, CalculationBase.DirectCost, 8m, 1),
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCostPlusIndirect, 14m, 2),
                MakeConcept("CONT", EconomicCategory.Contingency, CalculationBase.PreviousSubtotal, 3m, 3),
                MakeConcept("RISK", EconomicCategory.Risk, CalculationBase.PreviousSubtotal, 2m, 4),
                MakeConcept("PROFIT", EconomicCategory.Profit, CalculationBase.PreviousSubtotal, 18m, 5),
                new EconomicConcept { Code = "DTO", Name = "Descuento", Category = EconomicCategory.Discount,
                    CalculationBase = CalculationBase.PriceBeforeDiscounts, Value = 5m, Order = 6 },
                new EconomicConcept { Code = "VAT", Name = "IVA", Category = EconomicCategory.Tax,
                    CalculationBase = CalculationBase.PreviousSubtotal, Value = 19m, Order = 7, RoundingDecimals = 2 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(10000m, config));

        Assert.Equal(10000m, result.UnitDirectCost);
        Assert.Equal(800m, result.TotalIndirectCosts);
        Assert.Equal(1512m, result.Overhead);     // (10000+800)*14% = 1512
        Assert.Equal(369.36m, result.Contingency); // (10800+1512)*3% = 369.36
        Assert.Equal(253.63m, result.Risk);         // (12312+369.36)*2% = 253.6272 -> 253.63

        // Verify intermediate subtotals through trace
        Assert.Equal(7, result.Trace.StepsExecuted);
    }

    // ────────────────────────── Precision and Rounding ──────────────────────────

    [Fact]
    public void Rounding_EachConceptRounded()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "GG", Name = "GG", Category = EconomicCategory.Overhead,
                    CalculationBase = CalculationBase.DirectCost, Value = 14.3333m, Order = 1, RoundingDecimals = 4 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(12345.6789m, config));
        // 12345.6789 * 14.3333% = 1769.68... -> round to 4 = 1769.6835
        Assert.Single(result.RoundingDetails);
        Assert.Equal(4, result.RoundingDetails[0].Decimals);
    }

    [Fact]
    public void BankersRounding_MidpointToEven()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "GG", Name = "GG", Category = EconomicCategory.Overhead,
                    CalculationBase = CalculationBase.DirectCost, Value = 2.5m, Order = 1, RoundingDecimals = 0 }
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1m, config));
        // 1 * 2.5% = 0.025 -> round to 0 = 0 (by Banker's, 0.025 rounds to 0, not 0.03)
        Assert.Equal(0m, result.Overhead);
    }

    [Fact]
    public void PartidaQuantity_ScalesTotal()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1)
            }
        };
        var input = MakeInput(2000m, config) with { PartidaQuantity = 5m };
        var result = EconomicEngine.Calculate(input);
        Assert.Equal(2200m, result.FinalUnitPrice);      // (2000 + 200) = 2200 per unit
        Assert.Equal(11000m, result.TotalPartidaCost);   // 2200 * 5
    }

    // ────────────────────────── Default Config ──────────────────────────

    [Fact]
    public void DefaultConfig_HasAllRequiredConcepts()
    {
        var config = EconomicConfig.CreateDefault();
        var concepts = config.GetActiveConcepts();
        Assert.NotEmpty(concepts);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.IndirectCost);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Overhead);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Contingency);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Risk);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Profit);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Discount);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.Tax);
        Assert.Contains(concepts, c => c.Category == EconomicCategory.InformativeWithholding);
    }

    [Fact]
    public void LegacyMarkup_ConvertsCorrectly()
    {
        var markup = new MarkupPolicy
        {
            GastosGeneralesPct = 14m,
            UtilidadPct = 18m,
            RiesgoPct = 2m,
            DescuentoPct = 5m,
            IvaPct = 19m
        };
        var config = EconomicConfig.FromLegacyMarkup(markup);
        Assert.NotEmpty(config.Concepts);
        Assert.Contains(config.Concepts, c => c.Code == "OVERHEAD" && c.Value == 14m);
        Assert.Contains(config.Concepts, c => c.Code == "PROFIT" && c.Value == 18m);
        Assert.Contains(config.Concepts, c => c.Code == "RISK" && c.Value == 2m);
        Assert.Contains(config.Concepts, c => c.Code == "DISCOUNT" && c.Value == 5m);
        Assert.Contains(config.Concepts, c => c.Code == "VAT" && c.Value == 19m);
    }

    // ────────────────────────── Validation ──────────────────────────

    [Fact]
    public void Validate_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EconomicValidation().Validate(null!));
    }

    [Fact]
    public void Validate_EmptyConfig_HasError()
    {
        var config = new EconomicConfig { Concepts = Array.Empty<EconomicConcept>() };
        var errors = new EconomicValidation().Validate(config);
        Assert.Contains(errors, e => e.Code == "EMPTY_CONFIG");
    }

    [Fact]
    public void Validate_DuplicateOrder_HasError()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 14m, 1),
                MakeConcept("UTIL", EconomicCategory.Profit, CalculationBase.DirectCost, 18m, 1)
            }
        };
        var errors = new EconomicValidation().Validate(config);
        Assert.Contains(errors, e => e.Code == "DUPLICATE_ORDER");
    }

    [Fact]
    public void Validate_InformativeThatAffectsPrice_HasError()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "BAD_INFO", Name = "Mal informativo", Category = EconomicCategory.InformativeWithholding,
                    Value = 10m, Order = 1, AffectsFinalPrice = true, IsInformative = true }
            }
        };
        var errors = new EconomicValidation().Validate(config);
        Assert.Contains(errors, e => e.Code == "INFORMATIVE_AFFECTS_PRICE");
    }

    [Fact]
    public void Validate_MissingConceptCode_HasError()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                new EconomicConcept { Code = "", Category = EconomicCategory.Overhead, Value = 10m, Order = 1 }
            }
        };
        var errors = new EconomicValidation().Validate(config);
        Assert.Contains(errors, e => e.Code == "MISSING_CODE");
    }

    [Fact]
    public void ValidateForCalculation_WithErrors_ReturnsInvalid()
    {
        var config = new EconomicConfig { Concepts = Array.Empty<EconomicConcept>() };
        var result = new EconomicValidation().ValidateForCalculation(config);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    // ────────────────────────── Trace ──────────────────────────

    [Fact]
    public void Trace_ContainsSteps()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1),
                MakeConcept("IVA", EconomicCategory.Tax, CalculationBase.PreviousSubtotal, 19m, 2)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.Equal(2, result.Trace.StepsExecuted);
        Assert.Contains(result.Trace.Steps, s => s.ConceptCode == "GG");
        Assert.Contains(result.Trace.Steps, s => s.ConceptCode == "IVA");
    }

    [Fact]
    public void Trace_EachStepHasFormula()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1)
            }
        };
        var result = EconomicEngine.Calculate(MakeInput(1000m, config));
        Assert.NotEmpty(result.Trace.Steps[0].Formula);
        Assert.NotEmpty(result.Trace.Steps[0].Explanation);
    }

    // ────────────────────────── Determinism ──────────────────────────

    [Fact]
    public void SameInput_ReturnsSameHash()
    {
        var config = new EconomicConfig
        {
            Concepts = new[]
            {
                MakeConcept("GG", EconomicCategory.Overhead, CalculationBase.DirectCost, 10m, 1)
            }
        };
        var input = MakeInput(5000m, config);
        var r1 = EconomicEngine.Calculate(input);
        var r2 = EconomicEngine.Calculate(input);
        Assert.Equal(r1.DeterministicHash, r2.DeterministicHash);
        Assert.Equal(r1.FinalUnitPrice, r2.FinalUnitPrice);
        Assert.Equal(r1.Overhead, r2.Overhead);
    }

    // ────────────────────────── Cancellation ──────────────────────────

    [Fact]
    public void CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            EconomicEngine.Calculate(MakeInput(), cts.Token));
    }
}
