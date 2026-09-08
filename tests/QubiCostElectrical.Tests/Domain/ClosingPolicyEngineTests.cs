using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Contrato NETO: <see cref="ClosingPolicyEngine.ApplyNeto"/> devuelve la venta post-política
/// PRE-descuento y PRE-IVA. El descuento y el IVA los aplica <c>BudgetTotalsCalculator</c>
/// de forma uniforme y NO deben estar dentro de <see cref="ClosingPolicyResult.VentaFinalClp"/>.
/// </summary>
public sealed class ClosingPolicyEngineTests
{
    // Venta base neta 1.350.000, costo ejecución 1.000.000, piso comercial 1.200.000, riesgo 5%.
    private static ClosingPolicyInput SampleInput() => new()
    {
        VentaBaseNetaClp = 1_350_000m,
        CostoEjecucionClp = 1_000_000m,
        PisoComercialClp = 1_200_000m,
        RiesgoImplicitoPct = 5m,
        Origenes = new List<CostOriginBreakdown>
        {
            new() { VentaClp = 700_000m },
            new() { VentaClp = 650_000m }
        }
    };

    [Fact]
    public void CostPlusMarkup_PassesThroughVentaBaseNeta()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection { Policy = ClosingPolicyType.CostPlusMarkup });
        Assert.Equal(1_350_000m, result.VentaFinalClp);
    }

    [Fact]
    public void TargetPrice_UsesFixedPriceWhenSet()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.TargetPrice,
            TargetPriceClp = 1_500_000m
        });

        Assert.Equal(1_500_000m, result.VentaFinalClp);
        Assert.Empty(result.Advertencias);
    }

    [Fact]
    public void TargetPrice_FallsBackToVentaBaseNetaAndWarnsWhenUnset()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection { Policy = ClosingPolicyType.TargetPrice });

        Assert.Equal(1_350_000m, result.VentaFinalClp);
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void LumpSumEpc_AddsOnlyTheRiskShortfall()
    {
        // Riesgo implícito 5%. Mínimo pedido 15% -> falta 10% de 1.000.000 = 100.000 sobre el piso.
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.LumpSumEpc,
            LumpSumMinRiskPct = 15m
        });

        Assert.Equal(1_300_000m, result.VentaFinalClp); // 1.200.000 + 100.000
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void LumpSumEpc_NoExtraWhenImpliedRiskAlreadyMeetsMinimum()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.LumpSumEpc,
            LumpSumMinRiskPct = 5m
        });

        Assert.Equal(1_200_000m, result.VentaFinalClp);
        Assert.Empty(result.Advertencias);
    }

    [Fact]
    public void CostPlusFee_UsesFixedFeeOverPercentage()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.CostPlusFee,
            FeeClp = 80_000m,
            FeePct = 50m
        });

        Assert.Equal(1_080_000m, result.VentaFinalClp); // 1.000.000 + 80.000
    }

    [Fact]
    public void CostPlusFee_UsesPercentageWhenNoFixedFee()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.CostPlusFee,
            FeeClp = 0m,
            FeePct = 10m
        });

        Assert.Equal(1_100_000m, result.VentaFinalClp); // 1.000.000 + 10%
    }

    [Fact]
    public void UnitPricePure_SumsRowLevelVenta()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection { Policy = ClosingPolicyType.UnitPricePure });
        Assert.Equal(1_350_000m, result.VentaFinalClp); // 700.000 + 650.000
    }

    [Fact]
    public void UnitPricePure_FallsBackToCostForOriginsWithoutOwnUnitPrice()
    {
        var input = new ClosingPolicyInput
        {
            VentaBaseNetaClp = 999_999m, // no debe usarse: hay orígenes reales
            Origenes = new List<CostOriginBreakdown>
            {
                new() { VentaClp = 700_000m },
                new() { VentaClp = 0m, MermasUf = 30_000m, IsGlobalAutoCharge = true },
                new() { VentaClp = 0m, OrdenesCambioUf = 200_000m }
            }
        };

        var result = ClosingPolicyEngine.ApplyNeto(input, new ClosingPolicySelection { Policy = ClosingPolicyType.UnitPricePure });

        Assert.Equal(930_000m, result.VentaFinalClp); // 700.000 + 30.000 + 200.000
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void Gainshare_SharesSavingsWhenUnderTargetCost()
    {
        // Costo ejecución 1.000.000 vs objetivo 1.100.000 -> ahorro 100.000; 50% compartido = 50.000
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.GainsharePainshare,
            TargetCostClp = 1_100_000m,
            GainsharePct = 50m
        });

        Assert.Equal(1_400_000m, result.VentaFinalClp); // 1.350.000 + 50.000
    }

    [Fact]
    public void Gainshare_RespectsCap()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.GainsharePainshare,
            TargetCostClp = 2_000_000m,
            GainsharePct = 100m,
            GainshareCapClp = 20_000m
        });

        Assert.Equal(1_370_000m, result.VentaFinalClp); // 1.350.000 + tope 20.000
    }

    [Fact]
    public void Gainshare_WarnsWithoutTargetCost()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection { Policy = ClosingPolicyType.GainsharePainshare });

        Assert.Equal(1_350_000m, result.VentaFinalClp);
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void Painshare_DiscountsOvercostFromUtilidad()
    {
        // Costo ejecución 1.000.000 vs objetivo 900.000 -> sobrecosto 100.000; 50% = 50.000 se descuenta.
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.GainsharePainshare,
            TargetCostClp = 900_000m,
            GainsharePct = 50m
        });

        Assert.Equal(1_300_000m, result.VentaFinalClp); // 1.350.000 - 50.000
        Assert.True(result.MargenClp < 350_000m);
    }

    [Fact]
    public void TargetPrice_WarnsWhenBelowCostoEjecucion()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.TargetPrice,
            TargetPriceClp = 800_000m // < costo ejecución 1.000.000
        });

        Assert.Equal(800_000m, result.VentaFinalClp);
        Assert.True(result.MargenClp < 0m);
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void DescuentoAndIva_AreNotInsideVentaFinalClp()
    {
        // El motor completo: con política por defecto, VentaFinalClp (neto) == venta base neta
        // del breakdown, NO el TotalVentaUf (que ya trae descuento e IVA).
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 12m, utilidadPct: 15m, metrosCuadrados: 0m);
        settings.Markup.DescuentoPct = 10m;
        settings.Markup.IvaPct = 19m;
        settings.Markup.IncluirIvaEnTotal = true;
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Tipo = "MAT", Codigo = "M1", Categoria = "Conductores", Cantidad = 100m, MatClp = 2_000m, MoClp = 500m },
        };
        var cost = new BudgetEngine().Calculate(rows, settings, null, new[] { new ItemizadoRow { Id = "P1", Cantidad = 100m } }, null);

        Assert.NotNull(cost.ClosingPolicyResult);
        // neto (pre-descuento, pre-IVA) > neto post-descuento > y muy por debajo del total con IVA.
        Assert.True(cost.ClosingPolicyResult!.VentaFinalClp > cost.TotalVentaNetoUf);
        Assert.True(cost.ClosingPolicyResult.VentaFinalClp < cost.TotalVentaConIvaUf);
        Assert.Equal(cost.ClosingPolicyResult.VentaFinalClp - cost.DescuentoUf, cost.TotalVentaNetoUf);
    }

    [Fact]
    public void EndToEnd_FromEngineBreakdown_EachPolicyProducesCoherentMargin()
    {
        var settings = BudgetSettings.FromValues(gastosGeneralesPct: 12m, utilidadPct: 15m, metrosCuadrados: 0m);
        var rows = new[]
        {
            new CubicacionRow { ItemId = "P1", Tipo = "MAT", Codigo = "M1", Categoria = "Conductores", Cantidad = 100m, MatClp = 2_000m, MoClp = 500m },
        };
        var cost = new BudgetEngine().Calculate(rows, settings, null, new[] { new ItemizadoRow { Id = "P1", Cantidad = 100m } }, null);
        var input = ClosingPolicyInput.FromBreakdown(cost);

        foreach (var policy in new[]
                 {
                     ClosingPolicyType.CostPlusMarkup, ClosingPolicyType.TargetPrice, ClosingPolicyType.LumpSumEpc,
                     ClosingPolicyType.CostPlusFee, ClosingPolicyType.UnitPricePure, ClosingPolicyType.GainsharePainshare,
                 })
        {
            var res = ClosingPolicyEngine.ApplyNeto(input, new ClosingPolicySelection { Policy = policy });
            Assert.Equal(policy, res.Policy);
            Assert.Equal(cost.CostoDirectoUf, res.CostoEjecucionClp);
            Assert.True(res.VentaFinalClp > 0m, $"{policy}: venta {res.VentaFinalClp}");
            Assert.Equal(res.VentaFinalClp - res.CostoEjecucionClp, res.MargenClp);
        }
    }

    [Fact]
    public void MargenPct_IsComputedFromVentaAndCostoEjecucion()
    {
        var result = ClosingPolicyEngine.ApplyNeto(SampleInput(), new ClosingPolicySelection { Policy = ClosingPolicyType.CostPlusMarkup });

        Assert.Equal(350_000m, result.MargenClp);
        Assert.True(result.MargenPct > 25.9m && result.MargenPct < 26m);
    }
}
