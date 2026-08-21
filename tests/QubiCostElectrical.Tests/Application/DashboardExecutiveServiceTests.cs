using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class DashboardExecutiveServiceTests
{
    private readonly DashboardExecutiveService _service = new();

    [Fact]
    public void BuildViewModel_ReturnsFourExecutiveKpis()
    {
        var cost = new CostBreakdown { TotalVentaUf = 100m, MaterialesUf = 40m, CostoDirectoUf = 80m, UfM2 = 2m };
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { MonedaPresupuesto = "UF", MetrosCuadrados = 50m };

        var vm = _service.BuildViewModel(cost, review, new List<ItemizadoRow>(), project, "UF");

        Assert.Equal(4, vm.Kpis.Count);
        Assert.Equal("TOTAL VENTA", vm.Kpis[0].Title);
        Assert.True(vm.Kpis[0].IsPrimary);
        Assert.Equal(DashboardExecutiveKpiValueKind.Money, vm.Kpis[0].ValueKind);
    }

    [Fact]
    public void RiskLabel_HighWhenCriticalIssues()
    {
        var review = new FinalReviewSummary
        {
            Issues = new[] { Issue(FinalReviewSeverity.Critical) }
        };
        Assert.Equal("ALTO", _service.RiskLabel(review, 0m));
    }

    [Fact]
    public void RiskLabel_MediumWhenWarnings()
    {
        var review = new FinalReviewSummary
        {
            Issues = new[] { Issue(FinalReviewSeverity.Warning) }
        };
        Assert.Equal("MEDIO", _service.RiskLabel(review, 0m));
    }

    private static FinalReviewIssue Issue(FinalReviewSeverity severity)
        => new("T1", severity, FinalReviewCategory.EmptyPartida, "", "", "", "", "", 1, "Problema", "Acción");

    [Fact]
    public void CalculateMaterialSensitivityTotal_ScalesMaterialsOnly()
    {
        var cost = new CostBreakdown
        {
            TotalVentaUf = 120m,
            MaterialesUf = 40m,
            CostoDirectoUf = 80m
        };
        var project = new ProjectInfoRow
        {
            RiesgoPct = 0m,
            GastosGeneralesPct = 0m,
            UtilidadPct = 0m,
            DescuentoPct = 0m,
            IncluirIvaEnTotal = false
        };

        decimal baseline = _service.CalculateMaterialSensitivityTotal(cost, project, 0m);
        decimal plus10 = _service.CalculateMaterialSensitivityTotal(cost, project, 10m);

        Assert.Equal(80m, baseline);
        Assert.Equal(84m, plus10);
    }

    [Fact]
    public void RankPartidas_GroupsByItemId()
    {
        var cost = new CostBreakdown
        {
            Origenes =
            {
                new CostOriginBreakdown { ItemId = "P1", Descripcion = "Cable", MaterialesUf = 30m },
                new CostOriginBreakdown { ItemId = "P1", Descripcion = "Cable", MaterialesUf = 20m },
                new CostOriginBreakdown { ItemId = "P2", Descripcion = "Tablero", MaterialesUf = 10m }
            }
        };
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1.1", Descripcion = "Instalación cable", EsTitulo = false },
            new() { Id = "P2", Item = "1.2", Descripcion = "Tablero general", EsTitulo = false }
        };

        var rows = _service.RankPartidas(cost, itemizado);

        Assert.Equal(2, rows.Count);
        Assert.Equal("1.1", rows[0].Item);
        Assert.Equal(50m, rows[0].TotalUf);
    }

    [Fact]
    public void BuildSensitivityCurve_ReturnsRequestedPoints()
    {
        var cost = new CostBreakdown { MaterialesUf = 10m, CostoDirectoUf = 10m, TotalVentaUf = 10m };
        var project = new ProjectInfoRow();

        var curve = _service.BuildSensitivityCurve(cost, project, new[] { -10m, 0m, 10m });

        Assert.Equal(3, curve.Count);
        Assert.Contains(curve, p => p.MaterialVariationPct == 0m);
    }

    // Regresión: el accent de "PARTIDAS LISTAS" se calcula acá desde los números crudos
    // (listas/totalPartidas), no re-derivado por el WPF parseando el texto ya formateado
    // ("12/15"). Esto verifica que el semáforo (verde/ámbar/rojo) siga siendo correcto.
    [Theory]
    [InlineData(0, 0, DashboardExecutiveKpiAccent.Orange)]   // sin partidas: no hay nada que medir
    [InlineData(10, 10, DashboardExecutiveKpiAccent.RiskLow)]   // 100% listas
    [InlineData(9, 10, DashboardExecutiveKpiAccent.RiskMedium)] // 90% listas
    [InlineData(5, 10, DashboardExecutiveKpiAccent.RiskHigh)]   // 50% listas
    public void BuildViewModel_PartidasListasAccent_ReflectsCompletionRatio(int listasEsperadas, int totalPartidas, DashboardExecutiveKpiAccent expectedAccent)
    {
        var itemizado = new List<ItemizadoRow>();
        for (int i = 0; i < totalPartidas; i++)
            itemizado.Add(new ItemizadoRow { Id = $"P{i}", Item = $"{i}", Descripcion = $"Partida {i}", EsTitulo = false });

        int pendientes = totalPartidas - listasEsperadas;
        var review = new FinalReviewSummary
        {
            Issues = Enumerable.Range(0, pendientes)
                .Select(i => new FinalReviewIssue($"E{i}", FinalReviewSeverity.Warning, FinalReviewCategory.EmptyPartida,
                    $"P{i}", $"{i}", "", "", "", 1, "Partida vacía", "Completar cubicación"))
                .ToArray()
        };

        var cost = new CostBreakdown { TotalVentaUf = 100m };
        var project = new ProjectInfoRow { MonedaPresupuesto = "UF" };

        var vm = _service.BuildViewModel(cost, review, itemizado, project, "UF");

        var partidasListas = vm.Kpis.Single(k => k.Title == "PARTIDAS LISTAS");
        Assert.Equal(expectedAccent, partidasListas.Accent);
    }
}
