using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Paridad hoja Excel -=COSTOS=- (BudgetTotalsCalculator.BuildExcelCostos):
///   · GG% y utilidad ADITIVA sobre Mat+MO+GO (NO equipos).
///   · VA% solo sobre equipos.
///   · costo proyecto = ejecución + riesgo + GG + utilidad aditiva + VA.
///   · diferencia por factores = venta − costo proyecto.
///   · ManoObraDotacionClp reemplaza la MO del motor en ejecución Y en la venta bruta.
///   · GastosOperacionalesClp entra en ejecución Y en la venta bruta (con factores).
///   · C-1: venta &lt; costo proyecto ⇒ inconsistencia BLOQUEANTE UtilidadNegativa.
/// </summary>
public sealed class BuildExcelCostosTests
{
    private static DirectCostResult Direct(
        decimal mat = 0m, decimal mo = 0m, decimal eq = 0m,
        decimal sumVenta = 0m, bool rowLevel = false)
        => new()
        {
            MaterialesUf = mat,
            ManoObraUf = mo,
            EquiposUf = eq,
            SumVentaClp = sumVenta,
            HasRowLevelMarkups = rowLevel,
        };

    private static BudgetSettings Settings(
        decimal gg = 0m, decimal ut = 0m, decimal va = 0m, decimal riesgo = 0m,
        decimal descuento = 0m, decimal iva = 0m, bool incluirIva = false,
        decimal go = 0m, decimal moDotacion = 0m)
        => new()
        {
            GastosOperacionalesClp = go,
            ManoObraDotacionClp = moDotacion,
            Markup = new MarkupPolicy
            {
                PoliticaCostosExcel = true,
                GastosGeneralesPct = gg,
                UtilidadPct = ut,
                ValorAgregadoEquiposPct = va,
                RiesgoPct = riesgo,
                DescuentoPct = descuento,
                IvaPct = iva,
                IncluirIvaEnTotal = incluirIva,
            },
        };

    [Fact]
    public void GgAndUtilidadAditiva_OnMatMoGo_NotEquipos()
    {
        // Mat 300 + MO 200 + GO 50 = 550. GG 5% = 27.5. UT 8% = 44. Equipos NO entran a la base.
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 300m, mo: 200m, eq: 100_000m),
            Settings(gg: 5m, ut: 8m, go: 50m));

        Assert.Equal(550m, result.BaseGgUtUf);
        Assert.Equal(27.5m, result.GastosGeneralesUf);
        Assert.Equal(44m, result.UtilidadAditivaUf);
        Assert.Equal(44m, result.UtilidadUf); // en Excel, UtilidadUf == aditiva
    }

    [Fact]
    public void ValorAgregado_OnEquiposOnly()
    {
        // Sin Mat/MO/GO: base GG/UT = 0 ⇒ GG y UT = 0. VA 3% de equipos 1000 = 30.
        var result = BudgetTotalsCalculator.Build(
            Direct(eq: 1_000m),
            Settings(gg: 10m, ut: 10m, va: 3m));

        Assert.Equal(0m, result.BaseGgUtUf);
        Assert.Equal(0m, result.GastosGeneralesUf);
        Assert.Equal(0m, result.UtilidadAditivaUf);
        Assert.Equal(30m, result.ValorAgregadoEquiposUf);
        Assert.Equal(1_030m, result.CostoProyectoUf); // ejecución 1000 + VA 30
    }

    [Fact]
    public void CostoProyecto_IsEjecucionPlusRiesgoPlusGgPlusUtPlusVa()
    {
        // ejecución = Mat 300 + MO 200 + GO 50 = 550 (equipos aparte). riesgo 10% de 550 = 55.
        // baseGgUt = 550. GG 5% = 27.5. UT 8% = 44. VA 3% de 100 = 3.
        // costo proyecto = 550 + 55 + 27.5 + 44 + 3 = 679.5
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 300m, mo: 200m, eq: 100m),
            Settings(gg: 5m, ut: 8m, va: 3m, riesgo: 10m, go: 50m));

        Assert.Equal(600m /*Mat+MO*/ - 200m + 200m + 50m, result.CostoDirectoUf); // 650 ejecución (incluye equipos vía direct.TotalUf)
        Assert.Equal(65m, result.RiesgoUf); // 10% de 650
        Assert.Equal(715m, result.BaseCostoUf); // 650 + 65
        Assert.Equal(550m, result.BaseGgUtUf);
        Assert.Equal(27.5m, result.GastosGeneralesUf);
        Assert.Equal(44m, result.UtilidadAditivaUf);
        Assert.Equal(3m, result.ValorAgregadoEquiposUf);
        Assert.Equal(715m + 27.5m + 44m + 3m, result.CostoProyectoUf); // 789.5
    }

    [Fact]
    public void WithoutFactors_VentaEqualsCostoProyecto_DiferenciaZero()
    {
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 300m, mo: 200m, eq: 100m),
            Settings(gg: 5m, ut: 8m, va: 3m, go: 50m));

        Assert.Equal(result.CostoProyectoUf, result.TotalVentaUf);
        Assert.Equal(0m, result.DiferenciaFactoresUf);
    }

    [Fact]
    public void WithFactors_DiferenciaFactores_IsVentaMinusCostoProyecto()
    {
        // bruto = venta row-level 900 + GO 50 = 950.
        // ejecución = (Mat 300 + MO 200 + EQ 100) - 200 + 200 + 50 = 650; baseGgUt = 300+200+50 = 550.
        // GG 5% = 27.5; UT 8% = 44; VA 3% de 100 = 3. costo proyecto = 650 + 27.5 + 44 + 3 = 724.5.
        // diferencia = 950 - 724.5 = 225.5
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 300m, mo: 200m, eq: 100m, sumVenta: 900m, rowLevel: true),
            Settings(gg: 5m, ut: 8m, va: 3m, go: 50m));

        Assert.Equal(950m, result.TotalVentaUf);
        Assert.Equal(724.5m, result.CostoProyectoUf);
        Assert.Equal(225.5m, result.DiferenciaFactoresUf);
    }

    [Fact]
    public void ManoObraDotacion_ReplacesEngineMo_InEjecucionAndBruto()
    {
        // engine MO 200, dotación 260. ejecución = (300+200+100) - 200 + 260 + 50 = 710.
        // baseGgUt = 300 + 260 + 50 = 610. GG 5% = 30.5. UT 8% = 48.8. VA 3% de 100 = 3.
        // costo proyecto = 710 + 30.5 + 48.8 + 3 = 792.3
        // bruto = SumVenta 900 + GO 50 = 950; ajuste MO: 950 - 200 + 260 = 1010.
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 300m, mo: 200m, eq: 100m, sumVenta: 900m, rowLevel: true),
            Settings(gg: 5m, ut: 8m, va: 3m, go: 50m, moDotacion: 260m));

        Assert.Equal(260m, result.ManoObraUf);
        Assert.Equal(710m, result.CostoDirectoUf);
        Assert.Equal(610m, result.BaseGgUtUf);
        Assert.Equal(792.3m, result.CostoProyectoUf);
        Assert.Equal(1_010m, result.TotalVentaUf);
        Assert.Equal(1_010m - 792.3m, result.DiferenciaFactoresUf);
    }

    [Fact]
    public void GastosOperacionales_FlowIntoEjecucionAndBruto()
    {
        // Mat 100, sin MO/EQ. GO 80. row-level venta 500.
        // ejecución = 100 - 0 + 0 + 80 = 180. baseGgUt = 100 + 0 + 80 = 180.
        // bruto = 500 + 80 = 580.
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 100m, sumVenta: 500m, rowLevel: true),
            Settings(go: 80m));

        Assert.Equal(80m, result.GastosOperacionalesUf);
        Assert.Equal(180m, result.CostoDirectoUf);
        Assert.Equal(180m, result.BaseGgUtUf);
        Assert.Equal(580m, result.TotalVentaUf);
    }

    [Fact]
    public void IvaAndDescuento_ApplyOverBruto()
    {
        // bruto = 1000 (no factores ⇒ = costo proyecto). descuento 10% ⇒ neto 900. IVA 19% ⇒ 1071.
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 1_000m),
            Settings(descuento: 10m, iva: 19m, incluirIva: true));

        Assert.Equal(1_000m, result.CostoProyectoUf);
        Assert.Equal(100m, result.DescuentoUf);
        Assert.Equal(900m, result.TotalVentaNetoUf);
        Assert.Equal(171m, result.IvaUf);
        Assert.Equal(1_071m, result.TotalVentaUf);
    }

    [Fact]
    public void C1_VentaBelowCostoProyecto_EmitsBlockingUtilidadNegativa()
    {
        // Mat 1000, GG 10%, UT 10% ⇒ costo proyecto = 1000 + 100 + 100 = 1200.
        // row-level venta 200 ⇒ 200 < 1200.
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 1_000m, sumVenta: 200m, rowLevel: true),
            Settings(gg: 10m, ut: 10m));

        Assert.Equal(1_200m, result.CostoProyectoUf);
        Assert.Contains(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
    }

    [Fact]
    public void C1_NoInconsistency_WhenVentaCoversCostoProyecto()
    {
        var result = BudgetTotalsCalculator.Build(
            Direct(mat: 1_000m, sumVenta: 5_000m, rowLevel: true),
            Settings(gg: 10m, ut: 10m));

        Assert.DoesNotContain(result.Inconsistencias, i => i.Tipo == "UtilidadNegativa");
    }
}
