using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public class BudgetTotalsCalculatorTests
{
    private static DirectCostResult Direct(
        decimal mat = 0m, decimal mo = 0m, decimal eq = 0m, decimal sc = 0m,
        decimal herr = 0m, decimal transp = 0m, decimal mermas = 0m, decimal oc = 0m)
        => new DirectCostResult
        {
            MaterialesUf = mat,
            ManoObraUf = mo,
            EquiposUf = eq,
            SubcontratosUf = sc,
            HerramientasUf = herr,
            TransporteUf = transp,
            MermasUf = mermas,
            OrdenesCambioUf = oc,
        };

    private static BudgetSettings DefaultSettings(decimal m2 = 0m) => new BudgetSettings
    {
        Markup = new MarkupPolicy(),
        MetrosCuadrados = m2,
    };

    // Test 1: costo directo cero → todos los totales cero
    [Fact]
    public void Build_ZeroCost_AllTotalsZero()
    {
        var result = BudgetTotalsCalculator.Build(Direct(), DefaultSettings());

        Assert.Equal(0m, result.CostoDirectoUf);
        Assert.Equal(0m, result.TotalVentaNetoUf);
        Assert.Equal(0m, result.TotalVentaUf);
        Assert.Equal(0m, result.MargenRealPct);
        Assert.Equal(0m, result.UfM2);
    }

    // Test 2: markup por defecto (GG=14%, Utilidad=18% sobre-venta, sin riesgo, sin descuento)
    [Fact]
    public void Build_DefaultMarkup_CascadeCorrect()
    {
        var direct = Direct(mat: 100m);
        var result = BudgetTotalsCalculator.Build(direct, DefaultSettings());

        // subtotal = 114; sobre-venta: bruto = 114 / (1 - 0.18) = 114 / 0.82
        decimal expectedBruto = 114m / 0.82m;
        Assert.Equal(100m, result.CostoDirectoUf);
        Assert.Equal(0m, result.RiesgoUf);
        Assert.Equal(100m, result.BaseCostoUf);
        Assert.Equal(14m, result.GastosGeneralesUf);
        Assert.Equal(114m, result.SubtotalUf);
        Assert.Equal(expectedBruto - 114m, result.UtilidadUf);
        Assert.Equal(0m, result.DescuentoUf);
        Assert.Equal(expectedBruto, result.TotalVentaNetoUf);
    }

    // Test 3: riesgo se aplica antes de GG (baseCosto = directo + riesgo)
    [Fact]
    public void Build_WithRiesgo_BaseCostoIncludesRiesgo()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { RiesgoPct = 10m },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(10m, result.RiesgoUf);
        Assert.Equal(110m, result.BaseCostoUf);
    }

    // Test 4: descuento reduce el neto (se aplica sobre bruto = subtotal + utilidad)
    [Fact]
    public void Build_WithDescuento_NetoReduced()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = 0m, UtilidadPct = 0m, DescuentoPct = 10m },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(10m, result.DescuentoUf);
        Assert.Equal(90m, result.TotalVentaNetoUf);
    }

    // Test 5: IncluirIvaEnTotal = false → TotalVentaUf = neto (sin IVA)
    [Fact]
    public void Build_IvaNotIncluded_TotalVentaEqualsNeto()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = 0m, UtilidadPct = 0m, IvaPct = 19m, IncluirIvaEnTotal = false },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(19m, result.IvaUf);
        Assert.Equal(119m, result.TotalVentaConIvaUf);
        Assert.Equal(100m, result.TotalVentaUf);
    }

    // Test 6: IncluirIvaEnTotal = true → TotalVentaUf = neto + IVA
    [Fact]
    public void Build_IvaIncluded_TotalVentaEqualsConIva()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = 0m, UtilidadPct = 0m, IvaPct = 19m, IncluirIvaEnTotal = true },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(119m, result.TotalVentaUf);
    }

    // Test 7: UF/m² se calcula cuando MetrosCuadrados > 0
    [Fact]
    public void Build_WithM2_UfM2Calculated()
    {
        var direct = Direct(mat: 200m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = 0m, UtilidadPct = 0m, IvaPct = 0m, IncluirIvaEnTotal = false },
            MetrosCuadrados = 100m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(2m, result.UfM2);
    }

    // Test 8: MetrosCuadrados = 0 → UfM2 = 0 (sin división por cero)
    [Fact]
    public void Build_ZeroM2_UfM2IsZero()
    {
        var direct = Direct(mat: 100m);
        var result = BudgetTotalsCalculator.Build(direct, DefaultSettings(m2: 0m));

        Assert.Equal(0m, result.UfM2);
    }

    // Test 9: ClampPct clampea negativos a 0 → porcentajes negativos se ignoran
    [Fact]
    public void Build_NegativeMarkupPct_ClampsToZero()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = -5m, UtilidadPct = -10m },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(0m, result.GastosGeneralesUf);
        Assert.Equal(0m, result.UtilidadUf);
        Assert.Equal(100m, result.TotalVentaNetoUf);
    }

    // Test 10: con sobre-venta, MargenRealPct coincide exactamente con UtilidadPct
    [Fact]
    public void Build_MargenRealPct_SobreVentaMatchesUtilidadPct()
    {
        var direct = Direct(mat: 100m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy { GastosGeneralesPct = 0m, UtilidadPct = 25m, MarkupMethod = MarkupMethod.SobreVenta, IvaPct = 0m, IncluirIvaEnTotal = false },
            MetrosCuadrados = 0m,
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        // sobre-venta: venta = 100/0.75; margen = (venta-100)/venta*100 = 25%
        Assert.Equal(25m, result.MargenRealPct, precision: 4);
    }

    [Fact]
    public void Build_ValorAgregadoEquipos_AddsToSubtotalBeforeUtilidad()
    {
        var direct = Direct(mat: 70m, mo: 20m, eq: 10m);
        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy
            {
                GastosGeneralesPct = 0m,
                UtilidadPct = 0m,
                ValorAgregadoEquiposPct = 3m,
                IvaPct = 0m,
                IncluirIvaEnTotal = false,
                MarkupMethod = MarkupMethod.SobreCosto
            }
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(0.3m, result.ValorAgregadoEquiposUf);
        Assert.Equal(100.3m, result.SubtotalUf);
        Assert.Equal(100.3m, result.TotalVentaUf);
    }

    [Fact]
    public void Build_ExcelCostos_GgUtOnMatMoGo_VaOnEquipos_DiferenciaFactores()
    {
        var direct = Direct(mat: 300m, mo: 200m, eq: 100m);
        direct.SumVentaClp = 720m; // presentación con factores
        direct.HasRowLevelMarkups = true;

        var settings = new BudgetSettings
        {
            GastosOperacionalesClp = 50m,
            ManoObraDotacionClp = 0m, // usa MO del motor
            Markup = new MarkupPolicy
            {
                PoliticaCostosExcel = true,
                GastosGeneralesPct = 5m,
                UtilidadPct = 8m,
                ValorAgregadoEquiposPct = 3m,
                IvaPct = 0m,
                IncluirIvaEnTotal = false,
                RiesgoPct = 0m,
                DescuentoPct = 0m,
            }
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        // ejecución = 600 + 50 GO = 650
        Assert.Equal(650m, result.CostoDirectoUf);
        Assert.Equal(50m, result.GastosOperacionalesUf);
        // base GG/UT = Mat 300 + MO 200 + GO 50 = 550
        Assert.Equal(550m, result.BaseGgUtUf);
        Assert.Equal(27.5m, result.GastosGeneralesUf);   // 5%
        Assert.Equal(44m, result.UtilidadAditivaUf);      // 8%
        Assert.Equal(3m, result.ValorAgregadoEquiposUf);  // 3% de 100
        // costo proyecto = 650 + 27.5 + 44 + 3 = 724.5
        Assert.Equal(724.5m, result.CostoProyectoUf);
        // venta = SumVenta + GO = 720 + 50 = 770
        Assert.Equal(770m, result.TotalVentaUf);
        Assert.Equal(770m - 724.5m, result.DiferenciaFactoresUf);
    }

    /// <summary>
    /// Regresión numérica contra PEL029A2026 hoja -=COSTOS=- (CLP, tolerancia 1).
    /// Fuente: docs/analisis-factores-presentacion-PEL029A2026.md
    /// </summary>
    [Fact]
    public void Build_ExcelCostos_Pel029_RegressionTotals()
    {
        const decimal mat = 292_927_299m;
        const decimal eq = 167_929_393m;
        const decimal mo = 236_857_500m;
        const decimal go = 48_062_181m;
        const decimal ventaExcel = 870_514_944m;
        const decimal ejecucionExcel = 745_776_373m;
        const decimal costoProyectoExcel = 825_934_362m;
        const decimal diferenciaExcel = 44_580_582m;

        var direct = Direct(mat: mat, mo: mo, eq: eq);
        direct.SumVentaClp = ventaExcel - go; // DETALLE sin GO; el motor suma GO
        direct.HasRowLevelMarkups = true;

        var settings = new BudgetSettings
        {
            GastosOperacionalesClp = go,
            ManoObraDotacionClp = mo,
            Markup = new MarkupPolicy
            {
                PoliticaCostosExcel = true,
                GastosGeneralesPct = 5m,
                UtilidadPct = 8m,
                ValorAgregadoEquiposPct = 3m,
                IvaPct = 0m,
                IncluirIvaEnTotal = false,
                RiesgoPct = 0m,
                DescuentoPct = 0m,
            }
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        Assert.Equal(ejecucionExcel, result.CostoDirectoUf);
        Assert.Equal(mat + mo + go, result.BaseGgUtUf);
        Assert.Equal(28_892_349m, result.GastosGeneralesUf);
        Assert.Equal(46_227_758.4m, result.UtilidadAditivaUf);
        Assert.Equal(5_037_881.79m, result.ValorAgregadoEquiposUf);
        Assert.Equal(costoProyectoExcel, Math.Round(result.CostoProyectoUf, MidpointRounding.AwayFromZero));
        Assert.Equal(ventaExcel, result.TotalVentaUf);
        Assert.Equal(diferenciaExcel, Math.Round(result.DiferenciaFactoresUf, MidpointRounding.AwayFromZero));
        // Margen observado Excel ≈ 14,33 %
        Assert.InRange(result.MargenRealPct, 14.32m, 14.34m);
    }
}
