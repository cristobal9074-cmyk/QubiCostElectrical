using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Golden de regresión: Viáticos Recalc → ResultItems → suma GO → BudgetTotalsCalculator (Excel)
/// → CostoBreakdownLineBuilder. No es golden de exportación Excel.
/// </summary>
public sealed class CostoViaticosGoldenTests
{
    /// <summary>CLP congelados del escenario A (fórmulas Recalc con defaults de tarifas).</summary>
    private const decimal ExpectedTotalClp = 11_653_000m;
    private const decimal ValorUfClp = 38_000m;

    // Escenario B — Mat/MO/Eq fijos + GO del golden A (política Excel).
    private const decimal MatClp = 10_000_000m;
    private const decimal MoClp = 5_000_000m;
    private const decimal EqClp = 2_000_000m;

    // Literales bloqueados tras una corrida del motor Excel (sin factores de fila):
    // ejecución = Mat+MO+Eq+GO = 28_653_000
    // baseGgUt = Mat+MO+GO = 26_653_000
    // GG 5% = 1_332_650; UT 8% = 2_132_240; VA 3% eq = 60_000
    // costo proyecto = venta (sin markups de fila) = 32_177_890
    private const decimal ExpectedEjecucionClp = 28_653_000m;
    private const decimal ExpectedBaseGgUtClp = 26_653_000m;
    private const decimal ExpectedGgClp = 1_332_650m;
    private const decimal ExpectedUtClp = 2_132_240m;
    private const decimal ExpectedVaClp = 60_000m;
    private const decimal ExpectedCostoProyectoClp = 32_177_890m;

    private static ViaticosProfesionalViewModel CreateGoldenVm()
    {
        // ctor sincroniza pico/roles/meses; DotDir parte en 0 → se sobreescribe abajo.
        var vm = new ViaticosProfesionalViewModel(valorUfClp: ValorUfClp, picoDirecta: 0, rolesIndirectos: 0, mesesObra: 2);
        vm.DotDir = 10;
        vm.DotInd = 2;
        vm.Meses = 2;
        vm.DiasMes = 20;
        vm.FactorPct = 70;
        vm.InclArriendo = false;
        vm.EmpresaProveAlim = false;
        // defaults: TarifaPasDir=20000, TarifaPasInd=80000, diaria=17500, DiasVisitaInd=8, InclOtros=5%, ReglaDiaPct=100
        return vm;
    }

    private static decimal SumResultItemsClp(ViaticosProfesionalViewModel vm)
        => vm.ResultItems.Sum(i => i.Cantidad * i.PrecioUnitarioClp);

    private static decimal SumLineasActivasClp(ViaticosProfesionalViewModel vm)
        => vm.Lineas.Where(l => l.Activo).Sum(l => l.Cantidad * l.PrecioUnitarioClp);

    [Fact]
    public void ScenarioA_ViaticosSmallGolden_TotalAndResultItems()
    {
        var vm = CreateGoldenVm();

        Assert.Equal(5, vm.ResultItems.Count);
        Assert.Equal(ExpectedTotalClp, SumResultItemsClp(vm));
        Assert.Equal(ExpectedTotalClp, SumLineasActivasClp(vm));

        var expectedUf = ExpectedTotalClp / ValorUfClp; // 306.65789… → N2
        Assert.Contains(
            expectedUf.ToString("N2", CultureInfo.CurrentCulture),
            vm.FootUfLabel,
            StringComparison.Ordinal);
        Assert.Equal(Math.Round(expectedUf, 2), Math.Round(ExpectedTotalClp / vm.ValorUf, 2));
    }

    [Fact]
    public void ScenarioB_GoSum_ExcelTotals_Stable()
    {
        var vm = CreateGoldenVm();
        var rows = new List<GastoOperacionalRow>
        {
            new()
            {
                Codigo = "3.3",
                Concepto = "Viáticos / movilización",
                Items = vm.ResultItems.Select(CloneItem).ToList(),
            },
        };

        var goClp = ExcelCostosBridge.SumGastosOperacionalesClp(rows);
        Assert.Equal(ExpectedTotalClp, goClp);

        var direct = new DirectCostResult
        {
            MaterialesUf = MatClp,
            ManoObraUf = MoClp,
            EquiposUf = EqClp,
        };

        var settings = new BudgetSettings
        {
            GastosOperacionalesClp = goClp,
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
            },
        };

        var result = BudgetTotalsCalculator.Build(direct, settings);

        // En política Excel los campos *Uf del breakdown llevan CLP (ver CostBreakdown).
        Assert.Equal(goClp, result.GastosOperacionalesUf);
        Assert.Equal(ExpectedEjecucionClp, result.CostoDirectoUf);
        Assert.Equal(ExpectedBaseGgUtClp, result.BaseGgUtUf);
        Assert.Equal(ExpectedGgClp, result.GastosGeneralesUf);
        Assert.Equal(ExpectedUtClp, result.UtilidadAditivaUf);
        Assert.Equal(ExpectedVaClp, result.ValorAgregadoEquiposUf);
        Assert.Equal(ExpectedCostoProyectoClp, result.CostoProyectoUf);
        Assert.Equal(ExpectedCostoProyectoClp, result.TotalVentaUf);

        // Equivalente UF del GO (referencia UI FootUf); el motor Excel guarda CLP en *Uf.
        Assert.Equal(ExpectedTotalClp / ValorUfClp, goClp / ValorUfClp);
    }

    [Fact]
    public void ScenarioC_EmpresaProveAlim_PasajesOnly()
    {
        var vm = CreateGoldenVm();
        vm.EmpresaProveAlim = true;

        // Alim → 0; otros sobre (alim+arr)=0 → omitidos; solo pasajes 5_600_000 + 320_000.
        const decimal expectedPasajesOnly = 5_920_000m;
        Assert.Equal(expectedPasajesOnly, SumResultItemsClp(vm));
        Assert.Equal(2, vm.ResultItems.Count);
        Assert.DoesNotContain(vm.ResultItems, i => i.Concepto.Contains("Alimentación", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vm.ResultItems, i => i.Concepto.Contains("Incidentales", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScenarioB_CostoBreakdownLineBuilder_AmountsMatch()
    {
        var vm = CreateGoldenVm();
        var goClp = ExcelCostosBridge.SumGastosOperacionalesClp(new[]
        {
            new GastoOperacionalRow
            {
                Codigo = "3.3",
                Items = vm.ResultItems.Select(CloneItem).ToList(),
            },
        });

        var direct = new DirectCostResult
        {
            MaterialesUf = MatClp,
            ManoObraUf = MoClp,
            EquiposUf = EqClp,
        };
        var settings = new BudgetSettings
        {
            GastosOperacionalesClp = goClp,
            Markup = new MarkupPolicy
            {
                PoliticaCostosExcel = true,
                GastosGeneralesPct = 5m,
                UtilidadPct = 8m,
                ValorAgregadoEquiposPct = 3m,
                IvaPct = 0m,
                IncluirIvaEnTotal = false,
            },
        };
        var cost = BudgetTotalsCalculator.Build(direct, settings);

        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true,
            GastosGeneralesPct = 5m,
            UtilidadPct = 8m,
            ValorAgregadoEquiposPct = 3m,
            MetrosCuadrados = 0,
        };

        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);

        Assert.Equal(cost.GastosOperacionalesUf, lines.Single(l => l.Label == "Gastos operacionales").AmountUf);
        Assert.Equal(cost.CostoDirectoUf, lines.Single(l => l.Label == "Costo de ejecución").AmountUf);
        Assert.Equal(cost.TotalVentaUf, lines.Single(l => l.Label == "TOTAL VENTA").AmountUf);
        Assert.Equal(ExpectedTotalClp, lines.Single(l => l.Label == "Gastos operacionales").AmountUf);
        Assert.Equal(ExpectedEjecucionClp, lines.Single(l => l.Label == "Costo de ejecución").AmountUf);
        Assert.Equal(ExpectedCostoProyectoClp, lines.Single(l => l.Label == "TOTAL VENTA").AmountUf);
    }

    private static GastoOperacionalItemRow CloneItem(GastoOperacionalItemRow src) => new()
    {
        Concepto = src.Concepto,
        Unidad = src.Unidad,
        Cantidad = src.Cantidad,
        PrecioUnitarioClp = src.PrecioUnitarioClp,
    };
}
