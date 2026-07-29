using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ItemizadoViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Itemized;
        public string ActiveProjectLabel => "";

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) =>
            RequestNavigate(route, force);

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

    public ItemizadoViewModelTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void Refresh_WithoutOpenProject_ShowsEmptyState()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("itemizado-empty");

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task FilterMode_ChangesActiveFilterFlags()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-filter-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-filter");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();

        Assert.True(vm.IsFilterTodos);

        vm.FilterMode = ItemizadoFilterMode.Incompletos;
        Assert.True(vm.IsFilterIncompletos);
        Assert.False(vm.IsFilterTodos);

        vm.FilterMode = ItemizadoFilterMode.SinPrecio;
        Assert.True(vm.IsFilterSinPrecio);
        Assert.False(vm.IsFilterIncompletos);
    }

    [Fact]
    public async Task ReadOnly_DisablesAddCommands()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-readonly-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-readonly");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        host.Session.SetRoleReadOnly(true, "Prueba solo lectura");

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();

        Assert.True(vm.IsReadOnly);
        Assert.False(vm.AddPartidaCommand.CanExecute(null));
        Assert.False(vm.AddTitleCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_UpdatesSummaryCounts()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-summary-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-summary");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "T1",
            Item = "1",
            Descripcion = "INSTALACIONES",
            EsTitulo = true
        });
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización PVC",
            Unidad = "ML",
            Cantidad = 25m
        });
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P2",
            Item = "1.2",
            Descripcion = "Cableado",
            Unidad = "ML",
            Cantidad = 40m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();

        Assert.False(vm.ShowEmptyState);
        Assert.Equal("2", vm.SummaryPartidas);
        Assert.Contains("2 partidas", vm.SummaryStripText);
    }

    [Fact]
    public async Task FilterIncompletos_ExcludesCompletedPartidas()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-incompletos-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-incompletos");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Sin cubicar",
            Unidad = "UN",
            Cantidad = 1m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();
        vm.FilterMode = ItemizadoFilterMode.Incompletos;

        Assert.True(vm.IsFilterIncompletos);
        Assert.Contains(vm.Rows, r => r.Item == "1.1");
    }

    [Fact]
    public async Task ProcessPendingShellAction_adds_partida_when_flagged()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-pending-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-pending");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "1.0",
            Item = "1,0",
            Descripcion = "Capítulo",
            EsTitulo = true
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        host.Session.PendingItemizadoAction = ItemizadoPendingAction.AddPartida;
        var vm = new ItemizadoViewModel(new FakeShell(), host);

        Assert.True(host.Session.Itemizado.Count >= 2);
        Assert.Equal(ItemizadoPendingAction.None, host.Session.PendingItemizadoAction);
        vm.Dispose();
    }

    [Fact]
    public async Task AddPartidaCommand_inserts_partida_below_selection()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-add-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-add");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Cap", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "Base", Unidad = "UN", Cantidad = 1m });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.SelectedRow = vm.Rows.Last();
        int before = host.Session.Itemizado.Count;

        vm.AddPartidaCommand.Execute(null);

        Assert.Equal(before + 1, host.Session.Itemizado.Count);
        Assert.Contains(host.Session.Itemizado, r => r.Descripcion == "Nueva partida");
        vm.Dispose();
    }

    [Fact]
    public async Task CubicarCommand_uses_grid_selection_when_no_row_is_marked()
    {
        // La columna de checkbox se quitó de la grilla; Cubicar ahora opera sobre la fila
        // resaltada (SelectedRow), no sobre IsSelected.
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-cubicar-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-cubicar-label");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "A", Unidad = "UN", Cantidad = 1m });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00002", Item = "1,2", Descripcion = "B", Unidad = "UN", Cantidad = 1m });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);

        Assert.Equal("Cubicar partida", vm.CubicarButtonLabel);
        Assert.False(vm.CubicarCommand.CanExecute(null));

        vm.SelectedRow = vm.Rows.First(r => !r.EsTitulo);

        Assert.True(vm.CubicarCommand.CanExecute(null));
        vm.Dispose();
    }

    [Fact]
    public async Task FilteredCountLabel_shows_visible_of_total_when_filter_active()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"itemizado-count-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("itemizado-count");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Cap", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "A", Unidad = "UN", Cantidad = 1m, ExcluidoCubicacion = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00002", Item = "1,2", Descripcion = "B", Unidad = "UN", Cantidad = 1m });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.FilterMode = ItemizadoFilterMode.SinCubicar;

        Assert.Contains(" de ", vm.FilteredCountLabel);
        vm.Dispose();
    }
}
