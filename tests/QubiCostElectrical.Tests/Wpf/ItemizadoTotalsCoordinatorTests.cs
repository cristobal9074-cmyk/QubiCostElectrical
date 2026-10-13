using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ItemizadoTotalsCoordinatorTests
{
    [Fact]
    public void Calculate_WithEmptyRows_ReturnsZeros()
    {
        var snapshot = ItemizadoTotalsCoordinator.Calculate(
            Array.Empty<ItemizadoRowVm>(),
            Array.Empty<ItemizadoRowVm>(),
            38_500m,
            false,
            "");

        Assert.Equal(0m, snapshot.TotalBudgetUf);
        Assert.Equal(0m, snapshot.TotalBudgetClp);
        Assert.Equal("0,000 UF", snapshot.TotalBudgetUfText);
        Assert.Equal("$ 0", snapshot.TotalBudgetClpText);
        Assert.Equal(0, snapshot.TotalPartidasCount);
        Assert.Equal(0, snapshot.VisiblePartidasCount);
        Assert.False(snapshot.HasActiveFilter);
    }

    [Fact]
    public void Calculate_WithPartidas_ComputesTotalUfAndClpAndIgnoresTitles()
    {
        var chapter = new ItemizadoRowVm(new ItemizadoRow
        {
            Id = "1.0",
            Item = "1",
            Descripcion = "CAPÍTULO",
            EsTitulo = true
        });

        var p1 = new ItemizadoRowVm(new ItemizadoRow
        {
            Id = "p1",
            Item = "1.1",
            Descripcion = "Partida 1",
            Cantidad = 10m
        });
        p1.ApplyPresentation(new ItemizadoRowPresentation
        {
            SourceIndex = 1,
            Id = "p1",
            ParentId = "1.0",
            Item = "1.1",
            Descripcion = "Partida 1",
            Unidad = "UN",
            Cantidad = 10m,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 2.5m,
            TotalUf = 25m,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        });

        var p2 = new ItemizadoRowVm(new ItemizadoRow
        {
            Id = "p2",
            Item = "1.2",
            Descripcion = "Partida 2",
            Cantidad = 5m
        });
        p2.ApplyPresentation(new ItemizadoRowPresentation
        {
            SourceIndex = 2,
            Id = "p2",
            ParentId = "1.0",
            Item = "1.2",
            Descripcion = "Partida 2",
            Unidad = "UN",
            Cantidad = 5m,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 10m,
            TotalUf = 50m,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        });

        var allRows = new List<ItemizadoRowVm> { chapter, p1, p2 };
        var snapshot = ItemizadoTotalsCoordinator.Calculate(allRows, allRows, 40_000m, false, "");

        Assert.Equal(75m, snapshot.TotalBudgetUf);
        Assert.Equal(3_000_000m, snapshot.TotalBudgetClp);
        Assert.Equal("75,000 UF", snapshot.TotalBudgetUfText);
        Assert.Equal("$ 3.000.000", snapshot.TotalBudgetClpText);
        Assert.Equal(2, snapshot.TotalPartidasCount);
        Assert.Equal(2, snapshot.VisiblePartidasCount);
        Assert.Equal("2 partidas", snapshot.FooterPartidasText);
    }

    [Fact]
    public void Calculate_WithFilter_ComputesVisibleSeparatelyFromTotal()
    {
        var p1 = new ItemizadoRowVm(new ItemizadoRow { Id = "p1", Item = "1.1", Descripcion = "Partida 1" });
        p1.ApplyPresentation(new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "p1",
            ParentId = "",
            Item = "1.1",
            Descripcion = "Partida 1",
            Unidad = "UN",
            Cantidad = 1m,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 20m,
            TotalUf = 20m,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        });

        var p2 = new ItemizadoRowVm(new ItemizadoRow { Id = "p2", Item = "1.2", Descripcion = "Partida 2" });
        p2.ApplyPresentation(new ItemizadoRowPresentation
        {
            SourceIndex = 1,
            Id = "p2",
            ParentId = "",
            Item = "1.2",
            Descripcion = "Partida 2",
            Unidad = "UN",
            Cantidad = 1m,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 0,
            UnitPriceUf = 0m,
            TotalUf = 0m,
            HasPrice = false,
            StatusLabel = "Sin cubicar",
            StatusKind = "info"
        });

        var all = new List<ItemizadoRowVm> { p1, p2 };
        var visible = new List<ItemizadoRowVm> { p1 }; // Solo la completa visible

        var snapshot = ItemizadoTotalsCoordinator.Calculate(all, visible, 38_000m, true, "");

        Assert.Equal(20m, snapshot.TotalBudgetUf);
        Assert.Equal(2, snapshot.TotalPartidasCount);
        Assert.Equal(1, snapshot.VisiblePartidasCount);
        Assert.Equal(20m, snapshot.VisibleTotalUf);
        Assert.Equal("Mostrando 1 de 2 partidas", snapshot.FooterPartidasText);
        Assert.True(snapshot.HasActiveFilter);
    }

    [Fact]
    public void Calculate_InClp_HidesUfSecondary()
    {
        var p1 = BuildPartidaVm("p1", 75m);
        var allRows = new List<ItemizadoRowVm> { p1 };
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.CLP, 40_000m, 950m, 1050m);

        var snapshot = ItemizadoTotalsCoordinator.Calculate(allRows, allRows, ctx, false, "");

        // Política de moneda: CLP ⇒ sólo pesos, nunca UF.
        Assert.StartsWith("$", snapshot.TotalBudgetUfText);
        Assert.DoesNotContain("UF", snapshot.TotalBudgetUfText);
        Assert.Equal("", snapshot.TotalBudgetClpText);
    }

    [Fact]
    public void Calculate_InUf_ShowsUfPrimaryAndClpSecondary()
    {
        var p1 = BuildPartidaVm("p1", 75m);
        var allRows = new List<ItemizadoRowVm> { p1 };
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.UF, 40_000m, 950m, 1050m);

        var snapshot = ItemizadoTotalsCoordinator.Calculate(allRows, allRows, ctx, false, "");

        Assert.Contains("UF", snapshot.TotalBudgetUfText);
        Assert.StartsWith("$", snapshot.TotalBudgetClpText);
    }

    private static ItemizadoRowVm BuildPartidaVm(string id, decimal totalUf)
    {
        var vm = new ItemizadoRowVm(new ItemizadoRow { Id = id, Item = "1.1", Descripcion = "Partida" });
        vm.ApplyPresentation(new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = id,
            ParentId = "",
            Item = "1.1",
            Descripcion = "Partida",
            Unidad = "UN",
            Cantidad = 1m,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = totalUf,
            TotalUf = totalUf,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        });
        return vm;
    }
}
