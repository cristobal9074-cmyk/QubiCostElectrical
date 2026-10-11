using System.Collections.Generic;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ResumenItemizadoViewModelTests
{
    private static List<ItemizadoPartidaVm> CreateSampleItems()
    {
        return new List<ItemizadoPartidaVm>
        {
            new() { Id = "sec-1", ItemCode = "01.0", Descripcion = "ALIMENTADORES", EsSeccion = true },
            new() { Id = "part-1", ItemCode = "01.1", Descripcion = "Alimentador TDF-1", EsSeccion = false, Cantidad = 50, MaterialTotal = 100000, ManoObraTotal = 50000, CostoDirecto = 150000 },
            new() { Id = "part-2", ItemCode = "01.2", Descripcion = "Alimentador TDF-2", EsSeccion = false, Cantidad = 30, MaterialTotal = 0, ManoObraTotal = 0, CostoDirecto = 0 },
            new() { Id = "sec-2", ItemCode = "02.0", Descripcion = "TABLEROS", EsSeccion = true },
            new() { Id = "part-3", ItemCode = "02.1", Descripcion = "Tablero General TGA", EsSeccion = false, Cantidad = 1, MaterialTotal = 500000, ManoObraTotal = 120000, CostoDirecto = 620000 },
        };
    }

    [Fact]
    public void Constructor_WithInitialId_SelectsThatItem()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-3");

        Assert.NotNull(vm.SelectedRow);
        Assert.Equal("part-3", vm.SelectedRow!.Id);
        Assert.Equal("02.1", vm.SelectedRow.ItemCode);
    }

    [Fact]
    public void Constructor_WithoutInitialId_DefaultsToFirstNonSectionItem()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: null);

        Assert.NotNull(vm.SelectedRow);
        Assert.Equal("part-1", vm.SelectedRow!.Id);
        Assert.False(vm.SelectedRow.EsSeccion);
    }

    [Fact]
    public void SearchText_FiltersItems_AndKeepsOrSelectsMatchingRow()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");

        // Search for Tablero
        vm.SearchText = "Tablero";

        Assert.Equal(2, vm.FlatRows.Count);
        Assert.Equal("part-3", vm.SelectedRow?.Id);
    }

    [Fact]
    public void FilterByCubicacion_RetainsSelectionIfMatched()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-3");

        // Activa filtro 'Con cubicación'
        vm.ActiveFilter = "Con cubicación";

        Assert.Equal(2, vm.FlatRows.Count); // part-1 and part-3
        Assert.Equal("part-3", vm.SelectedRow?.Id);
    }

    [Fact]
    public void FilterBySinCubicar_SelectsFirstNonSectionMatch()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-3");

        // Activa filtro 'Sin cubicar' (part-3 tiene cubicación, no coincide)
        vm.ActiveFilter = "Sin cubicar";

        Assert.Single(vm.FlatRows);
        Assert.Equal("part-2", vm.FlatRows[0].Id);
        Assert.Equal("part-2", vm.SelectedRow?.Id);
    }
}
