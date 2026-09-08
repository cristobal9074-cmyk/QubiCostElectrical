using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// La política de cierre elegida en el proyecto MANDA sobre el precio de venta a través del
/// punto único <see cref="BudgetTotalsCalculator.Build"/>: para cada política, el total final
/// se arma como neto(política) → descuento → IVA de forma uniforme.
/// </summary>
public sealed class ClosingPolicyPipelineTests
{
    private static DirectCostResult Direct()
    {
        var d = new DirectCostResult
        {
            MaterialesUf = 1_000_000m,
            ManoObraUf = 300_000m,
            EquiposUf = 50_000m,
            SumVentaClp = 1_800_000m,
            HasRowLevelMarkups = true,
        };
        d.Origenes.Add(new CostOriginBreakdown { Origen = "Cubicacion", MaterialesUf = 1_000_000m, ManoObraUf = 300_000m, EquiposUf = 50_000m, VentaClp = 1_800_000m, HasRowLevelMarkup = true });
        return d;
    }

    private static BudgetSettings Settings(ClosingPolicyType policy, decimal descuento, bool incluirIva)
        => new()
        {
            ClosingPolicy = new ClosingPolicySelection
            {
                Policy = policy,
                TargetPriceClp = 2_500_000m,
                FeeClp = 400_000m,
                TargetCostClp = 1_500_000m,
                LumpSumMinRiskPct = 20m,
            },
            Markup = new MarkupPolicy
            {
                GastosGeneralesPct = 10m,
                UtilidadPct = 12m,
                RiesgoPct = 3m,
                DescuentoPct = descuento,
                IvaPct = 19m,
                IncluirIvaEnTotal = incluirIva,
            },
        };

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var policy in new[]
                 {
                     ClosingPolicyType.CostPlusMarkup, ClosingPolicyType.TargetPrice, ClosingPolicyType.LumpSumEpc,
                     ClosingPolicyType.CostPlusFee, ClosingPolicyType.UnitPricePure, ClosingPolicyType.GainsharePainshare,
                 })
            foreach (var descuento in new[] { 0m, 8m })
                foreach (var incluirIva in new[] { false, true })
                    yield return new object[] { policy, descuento, incluirIva };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Total_IsPolicyNetoThenDescuentoThenIva(ClosingPolicyType policy, decimal descuento, bool incluirIva)
    {
        var result = BudgetTotalsCalculator.Build(Direct(), Settings(policy, descuento, incluirIva));

        Assert.NotNull(result.ClosingPolicyResult);
        Assert.Equal(policy, result.ClosingPolicy);

        decimal neto0 = result.ClosingPolicyResult!.VentaFinalClp;
        Assert.True(neto0 > 0m);

        // descuento e IVA se aplican sobre el neto de la política, uniformemente.
        Assert.Equal(neto0 * descuento / 100m, result.DescuentoUf, 2);
        Assert.Equal(neto0 - result.DescuentoUf, result.TotalVentaNetoUf, 2);
        Assert.Equal(result.TotalVentaNetoUf * 0.19m, result.IvaUf, 2);
        Assert.Equal(result.TotalVentaNetoUf + result.IvaUf, result.TotalVentaConIvaUf, 2);
        Assert.Equal(incluirIva ? result.TotalVentaConIvaUf : result.TotalVentaNetoUf, result.TotalVentaUf, 2);
    }

    [Fact]
    public void DefaultPolicy_MatchesNoPolicy_Total()
    {
        var withDefault = BudgetTotalsCalculator.Build(Direct(), Settings(ClosingPolicyType.CostPlusMarkup, 5m, true));
        var noPolicy = Settings(ClosingPolicyType.CostPlusMarkup, 5m, true);
        noPolicy.ClosingPolicy = null;
        var without = BudgetTotalsCalculator.Build(Direct(), noPolicy);

        Assert.Equal(without.TotalVentaUf, withDefault.TotalVentaUf);
        Assert.Equal(without.TotalVentaNetoUf, withDefault.TotalVentaNetoUf);
    }

    [Fact]
    public void PolicyBelowPiso_EmitsBlockingUtilidadNegativa()
    {
        // Precio objetivo muy bajo -> neto bajo el piso comercial -> inconsistencia bloqueante.
        var settings = Settings(ClosingPolicyType.TargetPrice, 0m, false);
        settings.ClosingPolicy!.TargetPriceClp = 100_000m;
        var result = BudgetTotalsCalculator.Build(Direct(), settings);

        Assert.Contains(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
    }

    // H-1: el bloqueo UtilidadNegativa es policy-aware. costoEjecucion (Direct) = 1.350.000;
    // piso comercial ≈ 1.713.096 (subtotal + utilidad mínima 12%).

    [Fact]
    public void CostPlusFee_WithRealisticFee_DoesNotBlock()
    {
        // Costo+Fee con fee 10% -> neto = 1.485.000 (bajo el piso, pero es un modelo
        // costo-transparente que NO carga GG/utilidad): no debe bloquear.
        var settings = Settings(ClosingPolicyType.CostPlusFee, 0m, false);
        settings.ClosingPolicy!.FeeClp = 0m;
        settings.ClosingPolicy!.FeePct = 10m;
        var result = BudgetTotalsCalculator.Build(Direct(), settings);

        Assert.DoesNotContain(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
        Assert.Equal(0m, result.DiferenciaFactoresUf);
    }

    [Fact]
    public void TargetPrice_BelowPisoButAboveCost_WarnsNotBlocks()
    {
        var settings = Settings(ClosingPolicyType.TargetPrice, 0m, false);
        settings.ClosingPolicy!.TargetPriceClp = 1_500_000m; // > costo 1.350.000, < piso 1.713.096
        var result = BudgetTotalsCalculator.Build(Direct(), settings);

        Assert.DoesNotContain(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
        Assert.Contains(result.ClosingPolicyResult!.Advertencias, a => a.Contains("margen reducido"));
    }

    [Fact]
    public void TargetPrice_BelowExecutionCost_Blocks()
    {
        var settings = Settings(ClosingPolicyType.TargetPrice, 0m, false);
        settings.ClosingPolicy!.TargetPriceClp = 1_000_000m; // < costo de ejecución 1.350.000
        var result = BudgetTotalsCalculator.Build(Direct(), settings);

        Assert.Contains(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
    }
}
