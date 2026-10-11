using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Quotes;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class QuotesViewModelTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public QuotesViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    // 1. Construcción — ViewModel se construye sin excepciones
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var ex = Record.Exception(() => new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current));
        Assert.Null(ex);
    }

    // 2. Construcción — colecciones inicializadas
    [Fact]
    public void Constructor_CollectionsAreInitialized()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.NotNull(vm.ProviderSummaries);
        Assert.NotNull(vm.ProviderEntries);
        Assert.NotNull(vm.ComparisonRows);
        Assert.NotNull(vm.CategoryOptions);
    }

    // 3. Construcción — CategoryOptions tiene "Todas" como primera opción
    [Fact]
    public void Constructor_CategoryOptions_HasTodasFirst()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal("Todas", vm.CategoryOptions[0]);
    }

    // 4. ProjectSummary sin proyecto abierto
    [Fact]
    public void Constructor_WithoutProject_ProjectSummaryShowsNoProject()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("dummy");

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Contains("Sin proyecto abierto", vm.ProjectSummary);
    }

    // 5. ProjectSummary con proyecto abierto
    [Fact]
    public async Task Constructor_WithProject_ProjectSummaryShowsProjectName()
    {
        var host = WpfAppHost.Current;
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"quotes-ps-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("quotes-ps-test");
        host.Session.ProjectInfo.Nombre = "Proyecto cotizaciones";
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Contains("Proyecto cotizaciones", vm.ProjectSummary);
    }

    // 6. SearchText vacío por defecto
    [Fact]
    public void Constructor_SearchText_IsEmpty()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal("", vm.SearchText);
    }

    // 7. SelectedCategory por defecto es "Todas"
    [Fact]
    public void Constructor_SelectedCategory_IsAll()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal("Todas", vm.SelectedCategory);
    }

    // 8. Comandos no nulos
    [Fact]
    public void Constructor_Commands_AreNotNull()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.NotNull(vm.RefreshCommand);
        Assert.NotNull(vm.ClearFiltersCommand);
        Assert.NotNull(vm.ImportQuoteCommand);
        Assert.NotNull(vm.ApplyPriceCommand);
    }

    // 9. ImportQuoteCommand siempre habilitado (sin parámetro de guard — depende del diálogo)
    [Fact]
    public void ImportQuoteCommand_CanExecute_True()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ImportQuoteCommand.CanExecute(null));
    }

    // 10. ApplyPriceCommand — deshabilitado sin fila de comparación seleccionada
    [Fact]
    public void ApplyPriceCommand_WithoutRow_CannotExecute()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.False(vm.ApplyPriceCommand.CanExecute(null));
    }

    // 11. ApplyPriceCommand — habilitado con fila de comparación
    [Fact]
    public void ApplyPriceCommand_WithRow_CanExecute()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var row = new QuoteComparisonRowVm("COD1", "Desc", "ProvA", 1.0m, "ProvB", 0.8m, "Cat");
        vm.SelectedComparisonRow = row;

        Assert.True(vm.ApplyPriceCommand.CanExecute(row));
    }

    // 12. ApplyPrice — con precio cero no modifica materiales
    [Fact]
    public void ApplyPrice_WithZeroPrice_DoesNotUpdateMaterial()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("quotes-apply-zero-test");
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "COD1",
            Descripcion = "Material prueba",
            MatUf = 1.5m
        });

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var row = new QuoteComparisonRowVm("COD1", "Desc", "ProvA", 1.0m, "ProvB", 0m, "Cat");
        vm.ApplyPriceCommand.Execute(row);

        var mat = host.Session.Materiales.First(m => m.Codigo == "COD1");
        Assert.Equal(1.5m, mat.MatUf);
    }

    // 13. ApplyPrice — actualiza el material con el mejor precio
    [Fact]
    public void ApplyPrice_WithValidPrice_UpdatesMaterialMatUf()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("quotes-apply-valid-test");
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "CAB-002",
            Descripcion = "Cable de prueba",
            Unidad = "ML",
            MatUf = 0.5m
        });

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var row = new QuoteComparisonRowVm("CAB-002", "Cable de prueba", "ProvA", 0.5m, "ProvB", 0.35m, "Conductores");

        if (row.PrecioMejor > 0m)
        {
            vm.ApplyPriceCommand.Execute(row);
            var mat = host.Session.Materiales.FirstOrDefault(m => m.Codigo == "CAB-002");
            Assert.NotNull(mat);
            Assert.Equal(0.35m, mat!.MatUf);
        }
    }

    // 14. ApplyPrice — material no encontrado no lanza excepción
    [Fact]
    public void ApplyPrice_MaterialNotFound_DoesNotThrow()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("quotes-notfound-test");

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var row = new QuoteComparisonRowVm("INEXISTENTE", "Desc", "ProvA", 1.0m, "ProvB", 0.8m, "Cat");

        var ex = Record.Exception(() => vm.ApplyPriceCommand.Execute(row));
        Assert.Null(ex);
    }

    // 15. SearchText — cambia trigger Refresh del provider view
    [Fact]
    public void SearchText_Set_DoesNotThrow()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.SearchText = "PVC");
        Assert.Null(ex);
        Assert.Equal("PVC", vm.SearchText);
    }

    // 16. SelectedCategory — cambia trigger de filtro
    [Fact]
    public void SelectedCategory_Set_DoesNotThrow()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.SelectedCategory = "Cables");
        Assert.Null(ex);
        Assert.Equal("Cables", vm.SelectedCategory);
    }

    // 17. ClearFilters — resetea búsqueda y categoría
    [Fact]
    public void ClearFiltersCommand_ResetsFilters()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SearchText = "PVC";
        vm.SelectedCategory = "Cables";

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal("", vm.SearchText);
        Assert.Equal("Todas", vm.SelectedCategory);
    }

    // 18. Refresh — no lanza excepción
    [Fact]
    public void RefreshCommand_DoesNotThrow()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        var ex = Record.Exception(() => vm.RefreshCommand.Execute(null));
        Assert.Null(ex);
    }

    // 19. ShowEmptyState — verdadero cuando banco vacío
    [Fact]
    public void ShowEmptyState_WhenBankEmpty_IsTrue()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal(vm.ProviderSummaries.Count == 0 || vm.ProviderEntries.Count == 0, vm.ShowEmptyState);
    }

    // 20. QuoteComparisonRowVm — Delta UF calculado
    [Fact]
    public void QuoteComparisonRowVm_DeltaUf_IsSeleccionadoMinusMejor()
    {
        var row = new QuoteComparisonRowVm("COD", "Desc", "ProvA", 1.5m, "ProvB", 1.0m, "Cat");

        Assert.Equal(0.5m, row.DeltaUf);
    }

    // 21. QuoteComparisonRowVm — Delta Pct calculado
    [Fact]
    public void QuoteComparisonRowVm_DeltaPct_IsCorrect()
    {
        var row = new QuoteComparisonRowVm("COD", "Desc", "ProvA", 1.5m, "ProvB", 1.0m, "Cat");

        Assert.Equal(50m, row.DeltaPct);
    }

    // 22. QuoteComparisonRowVm — Delta Pct cuando precio mejor es cero
    [Fact]
    public void QuoteComparisonRowVm_DeltaPct_WhenBestIsZero_IsZero()
    {
        var row = new QuoteComparisonRowVm("COD", "Desc", "ProvA", 1.5m, "ProvB", 0m, "Cat");

        Assert.Equal(0m, row.DeltaPct);
    }

    // 23. QuoteEntryVm — PrecioUf es Mat + Mo
    [Fact]
    public void QuoteEntryVm_PrecioUf_IsMatPlusMo()
    {
        var source = new PriceBankEntry
        {
            Codigo = "X",
            Descripcion = "Test",
            Unidad = "UN",
            MatUf = 1.5m,
            MoUf = 0.3m
        };

        var vm = new QuoteEntryVm(source);

        Assert.Equal(1.8m, vm.PrecioUf);
    }

    // 24. QuoteEntryVm — expone propiedades del source
    [Fact]
    public void QuoteEntryVm_ExposesSourceProperties()
    {
        var source = new PriceBankEntry
        {
            Codigo = "PVC20",
            Descripcion = "PVC 20 mm",
            Unidad = "UN",
            Categoria = "Cables",
            MatUf = 0.35m,
            MoUf = 0m,
            Proveedor = "ProveedorA",
            Marca = "MarcaX",
            MonedaPrecio = "UF",
            OrigenPrecio = "Cotización"
        };

        var vm = new QuoteEntryVm(source);

        Assert.Equal("PVC20", vm.Codigo);
        Assert.Equal("PVC 20 mm", vm.Descripcion);
        Assert.Equal("UN", vm.Unidad);
        Assert.Equal("Cables", vm.Categoria);
        Assert.Equal(0.35m, vm.MatUf);
        Assert.Equal("ProveedorA", vm.Proveedor);
        Assert.Equal("MarcaX", vm.Marca);
        Assert.Equal("UF", vm.Moneda);
        Assert.Equal("Cotización", vm.Origen);
    }

    // 25. Navegación registrada en ShellRouteCatalog
    [Fact]
    public void Quotes_RegisteredInShellRouteCatalog()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.Quotes);
        Assert.Equal("Precios", info.Group);
        Assert.Contains("Cotizaciones", info.Title);
    }

    // 27. Regresión Fase 2 (~/.claude/plans/glittery-stargazing-hippo.md): FilterProvider usaba
    // allEntries.Any(...) por cada proveedor visible cuando había categoría seleccionada, en vez
    // de un índice precomputado. Verifica que el filtro por categoría sigue incluyendo solo a
    // los proveedores que realmente tienen una entrada en esa categoría.
    [Fact]
    public void SelectedCategory_FiltersProviders_ToOnlyThoseWithEntryInThatCategory()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string categoriaObjetivo = $"CategoriaX-{suffix}";
        string categoriaOtra = $"CategoriaY-{suffix}";
        string proveedorConCategoria = $"ProveedorConCategoria-{suffix}";
        string proveedorSinCategoria = $"ProveedorSinCategoria-{suffix}";

        var store = WpfAppHost.Current.Services.PriceBankStore;
        store.UpsertMany(new[]
        {
            new PriceBankEntry { Codigo = $"COD-{suffix}-1", Descripcion = "Material 1", Unidad = "UN", Categoria = categoriaObjetivo, Proveedor = proveedorConCategoria, MatUf = 1m },
            new PriceBankEntry { Codigo = $"COD-{suffix}-2", Descripcion = "Material 2", Unidad = "UN", Categoria = categoriaOtra, Proveedor = proveedorSinCategoria, MatUf = 1m }
        });

        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedCategory = categoriaObjetivo;

        var visibleProviders = vm.FilteredProviderSummaries.Cast<QuoteProviderSummaryVm>()
            .Select(p => p.Proveedor)
            .ToList();

        Assert.Contains(proveedorConCategoria, visibleProviders);
        Assert.DoesNotContain(proveedorSinCategoria, visibleProviders);
    }

    // 26. Sin referencias a WinForms
    [Fact]
    public void QuotesViewModel_HasNoWinFormsReference()
    {
        var type = typeof(QuotesViewModel);
        var referencedTypes = type.Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencedTypes, a =>
            a.Name != null &&
            (a.Name.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
             a.Name.Contains("QubiCostElectrical.WinForms", StringComparison.OrdinalIgnoreCase)));
    }

    // 27. SelectedComparisonRow setter actualiza ApplyPriceCommand
    [Fact]
    public void SelectedComparisonRow_Set_UpdatesApplyPriceCanExecute()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);
        Assert.False(vm.ApplyPriceCommand.CanExecute(null));

        var row = new QuoteComparisonRowVm("X", "Y", "A", 1m, "B", 0.8m, "C");
        vm.SelectedComparisonRow = row;

        Assert.True(vm.ApplyPriceCommand.CanExecute(row));
    }

    // 28. SummaryText refleja estado
    [Fact]
    public void SummaryText_IsPopulated()
    {
        var vm = new QuotesViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.NotNull(vm.SummaryText);
        Assert.NotEmpty(vm.SummaryText);
    }

    // 29. ApplyPriceCommand actualiza precio y preserva trazabilidad (proveedor, origen, fecha)
    [Fact]
    public void ApplyPriceCommand_UpdatesMaterialAndPreservesTraceability()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("quotes-trace-test");
        var mat = new MaterialRow
        {
            Codigo = "MAT-01",
            Descripcion = "Cable THHN 2.5",
            MatClp = 1000m,
            Proveedor = "Antiguo Proveedor"
        };
        host.Session.Materiales.Add(mat);

        var vm = new QuotesViewModel(new FakeShellHost(), host);
        var row = new QuoteComparisonRowVm("MAT-01", "Cable THHN 2.5", "Antiguo Proveedor", 1m, "ElectroChile", 0.75m, "Cables", 1000m, 750m);
        vm.SelectedComparisonRow = row;

        vm.ApplyPriceCommand.Execute(row);

        var updated = host.Session.Materiales.First(m => m.Codigo == "MAT-01");
        Assert.Equal(750m, updated.MatClp);
        Assert.Equal("ElectroChile", updated.Proveedor);
        Assert.Equal("Cotización ElectroChile", updated.OrigenPrecio);
        Assert.Equal(DateTime.Today, updated.FechaPrecio.Date);
    }
}
