using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ClosingPolicyEngineTests
{
    private static CostBreakdown SampleBreakdown() => new()
    {
        CostoDirectoUf = 1_000_000m,
        RiesgoUf = 50_000m,          // 5% implícito
        CostoProyectoUf = 1_200_000m,
        SubtotalUf = 1_200_000m,
        TotalVentaUf = 1_350_000m,
        EquiposUf = 100_000m,
        Origenes = new List<CostOriginBreakdown>
        {
            new() { VentaClp = 700_000m },
            new() { VentaClp = 650_000m }
        }
    };

    [Fact]
    public void CostPlusMarkup_PassesThroughEngineVenta()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection { Policy = ClosingPolicyType.CostPlusMarkup });
        Assert.Equal(1_350_000m, result.VentaFinalClp);
    }

    [Fact]
    public void TargetPrice_UsesFixedPriceWhenSet()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.TargetPrice,
            TargetPriceClp = 1_500_000m
        });

        Assert.Equal(1_500_000m, result.VentaFinalClp);
        Assert.Empty(result.Advertencias);
    }

    [Fact]
    public void TargetPrice_FallsBackToEngineVentaAndWarnsWhenUnset()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection { Policy = ClosingPolicyType.TargetPrice });

        Assert.Equal(1_350_000m, result.VentaFinalClp);
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void LumpSumEpc_AddsOnlyTheRiskShortfall()
    {
        // Riesgo implícito = 50_000/1_000_000 = 5%. Mínimo pedido = 15% -> falta 10% de 1_000_000 = 100_000
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.LumpSumEpc,
            LumpSumMinRiskPct = 15m
        });

        Assert.Equal(1_300_000m, result.VentaFinalClp); // 1_200_000 + 100_000
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void LumpSumEpc_NoExtraWhenEngineRiskAlreadyMeetsMinimum()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
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
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.CostPlusFee,
            FeeClp = 80_000m,
            FeePct = 50m // se ignora porque FeeClp > 0
        });

        Assert.Equal(1_080_000m, result.VentaFinalClp); // 1_000_000 + 80_000
    }

    [Fact]
    public void CostPlusFee_UsesPercentageWhenNoFixedFee()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.CostPlusFee,
            FeeClp = 0m,
            FeePct = 10m
        });

        Assert.Equal(1_100_000m, result.VentaFinalClp); // 1_000_000 + 10%
    }

    [Fact]
    public void UnitPricePure_SumsRowLevelVenta()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection { Policy = ClosingPolicyType.UnitPricePure });
        Assert.Equal(1_350_000m, result.VentaFinalClp); // 700_000 + 650_000
    }

    [Fact]
    public void UnitPricePure_FallsBackToCostForOriginsWithoutOwnUnitPrice()
    {
        // Cargos globales (herramientas/mermas) y órdenes de cambio aprobadas guardan VentaClp=0
        // pero sí tienen costo (MermasUf/OrdenesCambioUf) — sin el fallback quedaban fuera de la venta.
        var breakdown = new CostBreakdown
        {
            TotalVentaUf = 999_999m, // no debe usarse: hay orígenes reales
            Origenes = new List<CostOriginBreakdown>
            {
                new() { VentaClp = 700_000m },
                new() { VentaClp = 0m, MermasUf = 30_000m, IsGlobalAutoCharge = true },
                new() { VentaClp = 0m, OrdenesCambioUf = 200_000m }
            }
        };

        var result = ClosingPolicyEngine.Apply(breakdown, new ClosingPolicySelection { Policy = ClosingPolicyType.UnitPricePure });

        Assert.Equal(930_000m, result.VentaFinalClp); // 700_000 + 30_000 + 200_000
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void Gainshare_SharesSavingsWhenUnderTargetCost()
    {
        // Costo real 1_000_000 vs objetivo 1_100_000 -> ahorro 100_000; 50% compartido = 50_000
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.GainsharePainshare,
            TargetCostClp = 1_100_000m,
            GainsharePct = 50m
        });

        Assert.Equal(1_400_000m, result.VentaFinalClp); // 1_350_000 + 50_000
    }

    [Fact]
    public void Gainshare_RespectsCap()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection
        {
            Policy = ClosingPolicyType.GainsharePainshare,
            TargetCostClp = 2_000_000m, // ahorro enorme
            GainsharePct = 100m,
            GainshareCapClp = 20_000m
        });

        Assert.Equal(1_370_000m, result.VentaFinalClp); // 1_350_000 + tope 20_000
    }

    [Fact]
    public void Gainshare_WarnsWithoutTargetCost()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection { Policy = ClosingPolicyType.GainsharePainshare });

        Assert.Equal(1_350_000m, result.VentaFinalClp);
        Assert.NotEmpty(result.Advertencias);
    }

    [Fact]
    public void MargenPct_IsComputedFromVentaAndCostoEjecucion()
    {
        var result = ClosingPolicyEngine.Apply(SampleBreakdown(), new ClosingPolicySelection { Policy = ClosingPolicyType.CostPlusMarkup });

        // Margen = 1_350_000 - 1_000_000 = 350_000; % = 350_000/1_350_000*100
        Assert.Equal(350_000m, result.MargenClp);
        Assert.True(result.MargenPct > 25.9m && result.MargenPct < 26m);
    }
}
