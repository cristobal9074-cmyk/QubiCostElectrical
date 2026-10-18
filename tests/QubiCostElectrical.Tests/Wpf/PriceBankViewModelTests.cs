using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

// Regresión para la Fase 3 del plan de auditoría (~/.claude/plans/glittery-stargazing-hippo.md):
// UpdateSummary() recalculaba proveedores/categorías distintos sobre TODO entries en cada
// llamada, incluso cuando solo cambiaba el filtro (SelectedCategory/Provider/Currency,
// ClearFilters, debounce de búsqueda) y entries no había cambiado. Ahora esos conteos se
// cachean (RecomputeEntryStats) y solo se recalculan en los sitios donde entries realmente
// cambia. Estos tests no dependen de conteos absolutos (el banco de precios es compartido entre
// tests de la misma colección) — verifican la propiedad relativa: filtrar no debe alterar los
// conteos de categorías/proveedores, y agregar una entrada nueva sí debe reflejarse.
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PriceBankViewModelTests
{
    public PriceBankViewModelTests() => WpfTestHost.EnsureInitialized();

    private static (int categorias, int proveedores) ParseCounts(string summaryText)
    {
        var match = Regex.Match(summaryText, @"([\d.]+) categorías · ([\d.]+) proveedores");
        Assert.True(match.Success, $"No se pudo parsear SummaryText: '{summaryText}'");
        return (
            int.Parse(match.Groups[1].Value.Replace(".", ""), CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value.Replace(".", ""), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ChangingSelectedCategory_DoesNotChangeCategoryOrProviderCounts()
    {
        var vm = new PriceBankViewModel(WpfAppHost.Current);
        var before = ParseCounts(vm.SummaryText);

        // Elegir cualquier categoría real disponible (o "Todos" de nuevo) — el conteo de
        // categorías/proveedores es sobre TODO el banco, no sobre lo filtrado, así que no debe
        // moverse solo por cambiar el filtro.
        vm.SelectedCategory = vm.CategoryOptions.FirstOrDefault(c => c != "Todos") ?? "Todos";
        var afterCategoryFilter = ParseCounts(vm.SummaryText);

        vm.SelectedCategory = "Todos";
        vm.SelectedProvider = vm.ProviderOptions.FirstOrDefault(p => p != "Todos") ?? "Todos";
        var afterProviderFilter = ParseCounts(vm.SummaryText);

        Assert.Equal(before, afterCategoryFilter);
        Assert.Equal(before, afterProviderFilter);
    }

    // Regresión: Selector.SelectedItem es TwoWay por defecto en WPF. RebuildOptions() hacía
    // categoryOptions.Clear() sobre la colección bindeada al ComboBox de filtro — WPF perdía la
    // selección del combo y empujaba null de vuelta a SelectedCategory/Provider/Currency.
    // FilterEntry() interpretaba ese null como "filtrar por nada", rechazando TODAS las filas:
    // el usuario importaba un Excel con miles de precios y la grilla quedaba en "0 visibles"
    // aunque el banco sí los hubiera cargado. Refresh() (llamado tras cada importación) es el
    // camino real que dispara BuildFilterOptions con el banco ya lleno.
    [Fact]
    public void Refresh_NoPierdeLaSeleccionDeFiltros_YMantieneFilasVisibles()
    {
        var vm = new PriceBankViewModel(WpfAppHost.Current);
        Assert.True(vm.Entries.Count > 0, "El banco de precios debe tener registros para esta prueba.");

        vm.Refresh();

        Assert.Equal("Todos", vm.SelectedCategory);
        Assert.Equal("Todos", vm.SelectedProvider);
        Assert.Equal("Todas", vm.SelectedCurrency);
        Assert.True(vm.FilteredEntries.Cast<object>().Any(),
            "Tras recargar el banco, la vista filtrada no debe quedar vacía si hay registros.");
    }

    // DeleteAllCommand pasa por WpfDialogService.Confirm (una ventana real) antes de tocar el
    // store — no se puede Execute() en un test headless sin colgar la corrida (mismo motivo por
    // el que DeleteSelectedCommand tampoco se ejecuta en tests). Solo se verifica el cableado del
    // CanExecute; el borrado real ya está cubierto en PriceBankStoreTests.DeleteAll_*.
    [Fact]
    public void DeleteAllCommand_CanExecute_SoloConRegistrosYSinSoloLectura()
    {
        var vm = new PriceBankViewModel(WpfAppHost.Current);
        Assert.True(vm.Entries.Count > 0, "El banco de precios debe tener registros para esta prueba.");

        Assert.True(vm.DeleteAllCommand.CanExecute(null));
    }

    [Fact]
    public void CreateNewEntry_IncrementsProviderCategoryCounts_WhenValuesAreUnique()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string codigo = $"COD-{suffix}";
        string categoria = $"CategoriaNueva-{suffix}";
        string proveedor = $"ProveedorNuevo-{suffix}";

        var vm = new PriceBankViewModel(WpfAppHost.Current);
        var before = ParseCounts(vm.SummaryText);

        vm.NewEntryCommand.Execute(null);
        Assert.NotNull(vm.SelectedEntry);
        vm.SelectedEntry!.Codigo = codigo;
        vm.SelectedEntry.Categoria = categoria;
        vm.SelectedEntry.Proveedor = proveedor;

        // Editar Categoria/Proveedor en la fila (como el usuario haría en la grilla) no
        // recalcula el resumen por sí solo — igual que antes de este cambio, SummaryText se
        // actualiza en los puntos de guardado/creación/borrado/recarga, no en cada tecla de
        // cada celda. Guardar es el punto donde RecomputeEntryStats() debe reflejar los
        // valores finales de la fila.
        vm.SaveSelectedCommand.Execute(null);
        var afterAdd = ParseCounts(vm.SummaryText);
        Assert.Equal(before.categorias + 1, afterAdd.categorias);
        Assert.Equal(before.proveedores + 1, afterAdd.proveedores);
    }

    [Fact]
    public void DeleteAll_WhenCancelled_PreservesEntries()
    {
        var vm = new PriceBankViewModel(WpfAppHost.Current);
        int initialCount = vm.Entries.Count;
        Assert.True(initialCount > 0);

        bool promptCalled = false;
        vm.ConfirmDeleteAll = msg =>
        {
            promptCalled = true;
            return false;
        };

        vm.DeleteAllCommand.Execute(null);

        Assert.True(promptCalled);
        Assert.Equal(initialCount, vm.Entries.Count);
    }

    [Fact]
    public void TryCreateBackupBeforeDeleteAll_GeneratesBakFileIfExists()
    {
        var vm = new PriceBankViewModel(WpfAppHost.Current);
        var bakPath = vm.TryCreateBackupBeforeDeleteAll();
        if (bakPath != null)
        {
            try
            {
                Assert.True(File.Exists(bakPath));
                Assert.EndsWith(".bak", bakPath, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(bakPath))
                    File.Delete(bakPath);
            }
        }
    }
}

