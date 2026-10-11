using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Pruebas de paridad matemática y cero descuadre de redondeo intermodular:
/// Garantiza que los montos directos y de venta cuadren con exactitud milimétrica
/// entre Itemizado ↔ Cubicación ↔ APU ↔ Dashboard ↔ Oferta de Cierre.
/// </summary>
public sealed class CrossModuleBudgetMathParityTests
{
    [Fact]
    public void Parity_Cubicacion_Matches_Itemizado_And_BudgetEngine_DirectCosts()
    {
        // 1. Configurar un proyecto con 3 partidas y múltiples recursos de cubicación
        var session = new BudgetProjectSession();
        session.NewProject("Proyecto Paridad 1", "Auditor");
        session.ProjectInfo.ValorUfClp = 38_500m;
        session.ProjectInfo.GastosGeneralesPct = 10m;
        session.ProjectInfo.UtilidadPct = 15m;

        var cap = new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "TABLEROS Y FUERZA", EsTitulo = true };
        var p1 = new ItemizadoRow { Id = "1.0.01", ParentId = "1.0", Item = "1.1", Descripcion = "Tablero TDF", Unidad = "UN", Cantidad = 2m };
        var p2 = new ItemizadoRow { Id = "1.0.02", ParentId = "1.0", Item = "1.2", Descripcion = "Alimentador 3x50mm2", Unidad = "ML", Cantidad = 120m };
        var p3 = new ItemizadoRow { Id = "1.0.03", ParentId = "1.0", Item = "1.3", Descripcion = "Malla de Puesta a Tierra", Unidad = "GL", Cantidad = 1m };

        session.Itemizado.Add(cap);
        session.Itemizado.Add(p1);
        session.Itemizado.Add(p2);
        session.Itemizado.Add(p3);

        // Recursos en Cubicación
        session.Cubicacion.Add(new CubicacionRow { Id = "c1", ItemId = "1.0.01", Tipo = "MAT", Codigo = "GAB-01", Descripcion = "Gabinete 800x600", Unidad = "UN", Cantidad = 2m, MatClp = 150_000m });
        session.Cubicacion.Add(new CubicacionRow { Id = "c2", ItemId = "1.0.01", Tipo = "MO", Codigo = "MO-ARM", Descripcion = "Armado e integración", Unidad = "UN", Cantidad = 2m, MoClp = 80_000m });

        session.Cubicacion.Add(new CubicacionRow { Id = "c3", ItemId = "1.0.02", Tipo = "MAT", Codigo = "CAB-50", Descripcion = "Cable cobre 50mm2", Unidad = "ML", Cantidad = 120m, MatClp = 18_500m });
        session.Cubicacion.Add(new CubicacionRow { Id = "c4", ItemId = "1.0.02", Tipo = "MO", Codigo = "MO-TIR", Descripcion = "Tendido de cable", Unidad = "ML", Cantidad = 120m, MoClp = 6_500m });

        session.Cubicacion.Add(new CubicacionRow { Id = "c5", ItemId = "1.0.03", Tipo = "MAT", Codigo = "BAR-CU", Descripcion = "Barra Copperweld 5/8", Unidad = "UN", Cantidad = 4m, MatClp = 25_000m });
        session.Cubicacion.Add(new CubicacionRow { Id = "c6", ItemId = "1.0.03", Tipo = "MAT", Codigo = "SOLD-EXO", Descripcion = "Soldadura exotérmica", Unidad = "UN", Cantidad = 8m, MatClp = 12_000m });
        session.Cubicacion.Add(new CubicacionRow { Id = "c7", ItemId = "1.0.03", Tipo = "MO", Codigo = "MO-MALLA", Descripcion = "Excavación y montaje malla", Unidad = "GL", Cantidad = 1m, MoClp = 180_000m });

        var services = new AppServicesBuilder().Build();
        var cubicacionServices = new CubicacionPresenterServices(
            new BudgetCalculationService(),
            new CubicacionCompositionService());
        var cubicacionPresenter = new CubicacionPresenter(cubicacionServices, session);

        // Totales individuales por partida en Cubicación
        var tP1 = cubicacionPresenter.CalculatePartidaTotals("1.0.01", session.Cubicacion.Where(c => c.ItemId == "1.0.01").ToList());
        var tP2 = cubicacionPresenter.CalculatePartidaTotals("1.0.02", session.Cubicacion.Where(c => c.ItemId == "1.0.02").ToList());
        var tP3 = cubicacionPresenter.CalculatePartidaTotals("1.0.03", session.Cubicacion.Where(c => c.ItemId == "1.0.03").ToList());

        decimal expectedP1 = (2m * 150_000m) + (2m * 80_000m); // 300.000 + 160.000 = 460.000
        decimal expectedP2 = (120m * 18_500m) + (120m * 6_500m); // 2.220.000 + 780.000 = 3.000.000
        decimal expectedP3 = (4m * 25_000m) + (8m * 12_000m) + (1m * 180_000m); // 100.000 + 96.000 + 180.000 = 376.000

        Assert.Equal(expectedP1, tP1.CostoDirecto);
        Assert.Equal(expectedP2, tP2.CostoDirecto);
        Assert.Equal(expectedP3, tP3.CostoDirecto);

        // Precios unitarios
        Assert.Equal(expectedP1 / 2m, tP1.PrecioUnitario);
        Assert.Equal(expectedP2 / 120m, tP2.PrecioUnitario);
        Assert.Equal(expectedP3 / 1m, tP3.PrecioUnitario);

        // 2. Calcular Itemizado Workspace
        var itemizadoPresenter = ItemizadoPresenterFactory.CreatePresenter(services, session);
        var workspace = new ItemizadoWorkspaceService();
        var pageState = workspace.BuildPageState(itemizadoPresenter, session);

        var rowP1 = pageState.Rows.First(r => r.Id == "1.0.01");
        var rowP2 = pageState.Rows.First(r => r.Id == "1.0.02");
        var rowP3 = pageState.Rows.First(r => r.Id == "1.0.03");

        // Paridad partida por partida: el total del Itemizado coincide exactamente con Cubicación
        Assert.Equal(expectedP1, rowP1.TotalUf);
        Assert.Equal(expectedP2, rowP2.TotalUf);
        Assert.Equal(expectedP3, rowP3.TotalUf);

        Assert.Equal(expectedP1 / 2m, rowP1.UnitPriceUf);
        Assert.Equal(expectedP2 / 120m, rowP2.UnitPriceUf);
        Assert.Equal(expectedP3 / 1m, rowP3.UnitPriceUf);

        // 3. Totales acumulados del presupuesto
        decimal totalDirectoCubicacion = tP1.CostoDirecto + tP2.CostoDirecto + tP3.CostoDirecto;
        decimal totalItemizado = pageState.Rows.Where(r => !r.EsTitulo).Sum(r => r.TotalUf);

        Assert.Equal(totalDirectoCubicacion, totalItemizado);
        Assert.Equal(3_836_000m, totalItemizado);

        // 4. Paridad con el BudgetEngine del Dashboard
        var dashboardPresenter = new DashboardPresenter(new DashboardPresenterServices(
            services.BudgetCalculation,
            services.PartidaCompletion,
            services.FinalReview,
            new DashboardExecutiveService()), session);

        var costBreakdown = dashboardPresenter.BuildCost(session.Data);

        Assert.Equal(totalItemizado, costBreakdown.CostoDirectoUf);
        Assert.Equal(totalItemizado, costBreakdown.CostoDirectoClp);
    }

    [Fact]
    public void CurrencyConversion_ZeroRoundingDrift_Between_Uf_And_Clp()
    {
        // Verificar que con tasas decimales complejas (ej. UF con centavos),
        // la conversión no genere pérdidas de centavos por truncamientos
        decimal valorUf = 38_745.85m;
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.UF, valorUf, 945.50m, 1025.20m);

        var rows = new List<ItemizadoRowVm>();
        for (int i = 1; i <= 20; i++)
        {
            var r = new ItemizadoRowVm(new ItemizadoRow
            {
                Id = $"p{i}",
                Item = $"1.{i}",
                Descripcion = $"Partida {i}",
                Cantidad = 1m
            });
            // Total con decimales variados
            decimal totalUf = 5.123m + (i * 0.055m);
            r.ApplyPresentation(new ItemizadoRowPresentation
            {
                SourceIndex = i,
                Id = $"p{i}",
                ParentId = "",
                Item = $"1.{i}",
                Descripcion = $"Partida {i}",
                Unidad = "UN",
                Cantidad = 1m,
                EsTitulo = false,
                ExcluidoCubicacion = false,
                TotalUf = totalUf,
                UnitPriceUf = totalUf,
                CompletionState = PartidaCompletionState.Completed,
                CompletionPct = 100,
                StatusLabel = "Completado",
                StatusKind = "Completed"
            });
            rows.Add(r);
        }

        var snapshot = ItemizadoTotalsCoordinator.Calculate(rows, rows, ctx, false, "");

        decimal expectedSumUf = rows.Sum(r => r.TotalUf);
        decimal expectedSumClp = expectedSumUf * valorUf;

        Assert.Equal(expectedSumUf, snapshot.TotalBudgetUf);
        Assert.Equal(expectedSumClp, snapshot.TotalBudgetClp);
    }

    [Fact]
    public void CommercialClosingPolicy_MathConsistency_Across_Venta_And_Neto()
    {
        var direct = new DirectCostResult
        {
            MaterialesUf = 10_000_000m,
            ManoObraUf = 5_000_000m,
            EquiposUf = 2_000_000m
        };

        var settings = new BudgetSettings
        {
            Markup = new MarkupPolicy
            {
                PoliticaCostosExcel = true,
                GastosGeneralesPct = 12m,
                UtilidadPct = 16m,
                ValorAgregadoEquiposPct = 4m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true
            }
        };

        var cost = BudgetTotalsCalculator.Build(direct, settings);

        // Costo de ejecución
        Assert.Equal(17_000_000m, cost.CostoDirectoUf);

        // Base GG/UT = Mat 10M + MO 5M = 15M
        Assert.Equal(15_000_000m, cost.BaseGgUtUf);
        Assert.Equal(1_800_000m, cost.GastosGeneralesUf);     // 12% de 15M
        Assert.Equal(2_400_000m, cost.UtilidadAditivaUf);     // 16% de 15M
        Assert.Equal(80_000m, cost.ValorAgregadoEquiposUf);   // 4% de 2M

        // Costo proyecto = 17M + 1.8M + 2.4M + 0.08M = 21.280.000
        Assert.Equal(21_280_000m, cost.CostoProyectoUf);

        // Venta neta = Costo proyecto (sin factores) = 21.280.000
        Assert.Equal(21_280_000m, cost.TotalVentaNetoUf);

        // IVA 19% = 4.043.200
        Assert.Equal(4_043_200m, cost.IvaUf);

        // Total con IVA = 25.323.200
        Assert.Equal(25_323_200m, cost.TotalVentaConIvaUf);
        Assert.Equal(25_323_200m, cost.TotalVentaUf);

        // Cuadratura de la identidad contable fundamental
        Assert.Equal(cost.TotalVentaConIvaUf, cost.TotalVentaNetoUf + cost.IvaUf);
    }
}
