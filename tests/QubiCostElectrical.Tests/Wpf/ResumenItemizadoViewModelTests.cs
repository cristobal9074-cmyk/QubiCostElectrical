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

    [Fact]
    public void Constructor_WithInitialId_MarksCurrentPartidaAura()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");

        var current = items.Find(x => x.Id == "part-1");
        var other = items.Find(x => x.Id == "part-2");

        Assert.NotNull(current);
        Assert.NotNull(other);
        Assert.True(current!.IsCurrentPartida);
        Assert.False(other!.IsCurrentPartida);
    }

    [Fact]
    public void AddPartidaCommand_InsertsNewPartida_SelectsIt_AndSetsHasMutations()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");
        int countBefore = vm.TotalPartidasCount;

        Assert.False(vm.HasMutations);
        Assert.True(vm.AddPartidaCommand.CanExecute(null));
        vm.AddPartidaCommand.Execute(null);

        Assert.True(vm.HasMutations);
        Assert.Equal(countBefore + 1, vm.TotalPartidasCount);
        Assert.NotNull(vm.SelectedRow);
        Assert.Equal("Nueva partida", vm.SelectedRow!.Descripcion);
        Assert.Equal("GL", vm.SelectedRow.Unidad);
        Assert.Equal(1m, vm.SelectedRow.Cantidad);
    }

    [Fact]
    public void DeletePartidaCommand_RemovesSelectedPartida_AndSetsHasMutations()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");
        int countBefore = vm.TotalPartidasCount;

        Assert.True(vm.DeletePartidaCommand.CanExecute(null));
        vm.DeletePartidaCommand.Execute(null);

        Assert.True(vm.HasMutations);
        Assert.Equal(countBefore - 1, vm.TotalPartidasCount);
        Assert.DoesNotContain(vm.FlatRows, x => x.Id == "part-1");
    }

    [Fact]
    public void EditingPartidaProperties_SetsHasMutations()
    {
        var items = CreateSampleItems();
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");

        Assert.False(vm.HasMutations);
        Assert.NotNull(vm.SelectedRow);

        vm.SelectedRow!.Descripcion = "Alimentador Actualizado TDF-1 Mod";
        Assert.True(vm.HasMutations);

        vm.SelectedRow.Cantidad = 99m;
        Assert.Equal("99", vm.SelectedRow.CantidadDisplay);

        vm.SelectedRow.ItemCode = "01.99";
        Assert.Equal("01.99", vm.SelectedRow.ItemCode);
    }

    [Theory]
    [InlineData("2,2", "2,3")]
    [InlineData("2.2", "2.3")]
    [InlineData("01.02", "01.03")]
    [InlineData("2,0", "2,1")]
    [InlineData("3", "4")]
    public void RecommendNextItemCode_GeneratesExpectedCorrelative(string reference, string expected)
    {
        var existing = System.Array.Empty<string>();
        var result = ResumenItemizadoViewModel.RecommendNextItemCode(reference, existing);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RecommendNextItemCode_SkipsExistingCollisions()
    {
        var existing = new[] { "2,1", "2,2", "2,3" };
        var result = ResumenItemizadoViewModel.RecommendNextItemCode("2,2", existing);
        Assert.Equal("2,4", result);
    }

    [Fact]
    public void AddPartida_SetsRecommendedCodeBasedOnSelection_AndAllowsEditing()
    {
        var items = CreateSampleItems();
        // Select part-1 (code "01.1"); "01.2" already exists in sample items, so next free is "01.3"
        var vm = new ResumenItemizadoViewModel(items, initialId: "part-1");

        vm.AddPartidaCommand.Execute(null);

        Assert.NotNull(vm.SelectedRow);
        Assert.Equal("01.3", vm.SelectedRow!.ItemCode);

        // User edits the code to custom format
        vm.SelectedRow.ItemCode = "01.1-BIS";
        Assert.Equal("01.1-BIS", vm.SelectedRow.ItemCode);
        Assert.True(vm.HasMutations);
    }
}
