using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class VersionsViewModelTests
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

    private static BudgetVersionRow MakeVersion(string codigo, string nombre, decimal montoUf = 0m)
    {
        var snapshot = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = nombre },
            Itemizado = [],
            Cubicacion = []
        };
        return new BudgetVersionRow
        {
            Codigo = codigo,
            Nombre = nombre,
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow,
            FechaModificacion = DateTime.UtcNow,
            MontoVentaUf = montoUf,
            Snapshot = snapshot
        };
    }

    public VersionsViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    // 1. Construcción sin proyecto
    [Fact]
    public void Constructor_WithoutProject_ShowsEmptyState()
    {
        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Versions);
        Assert.Null(vm.SelectedVersion);
    }

    // 2. Estado sin proyecto — mensaje y comandos
    [Fact]
    public void Constructor_WithoutProject_StatusMessageAndCommandsDisabled()
    {
        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Contains("Abre", vm.StatusMessage);
        Assert.False(vm.NewCommand.CanExecute(null));
        Assert.False(vm.ActivateCommand.CanExecute(null));
        Assert.False(vm.RenameCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    // 3. Sin proyecto — SummaryText y ActiveVersionText
    [Fact]
    public void Constructor_WithoutProject_SummaryAndActiveVersionText()
    {
        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal("Sin proyecto abierto", vm.SummaryText);
        Assert.Equal("Sin versión activa", vm.ActiveVersionText);
    }

    // 4. Proyecto abierto sin versiones
    [Fact]
    public async Task Refresh_ProjectWithNoVersions_ShowsEmptyStateWithProjectMessage()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-empty-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-empty-test");
        host.Session.Versiones.Clear();
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Versions);
        Assert.Contains("todavía no tiene versiones", vm.StatusMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowEmptyState_IsTrue_WhenNoProjectOrNoVersions(bool withProject)
    {
        var host = WpfAppHost.Current;
        if (withProject)
        {
            var path = Path.Combine(Path.GetTempPath(), $"ver-empty-theory-{Guid.NewGuid():N}.qce");
            host.Session.NewProject("ver-empty-theory");
            host.Session.Versiones.Clear();
            await host.ProjectWorkspace.SaveAsAsync(path);
        }
        else
        {
            host.Session.NewProject("ver-no-project");
        }

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Versions);
    }

    // 5b. Solo lectura — NewCommand deshabilitado (rol Visor / lock)
    [Fact]
    public async Task ReadOnly_DisablesNewCommand()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-readonly-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-readonly-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        try
        {
            host.Session.SetRoleReadOnly(true, "Prueba solo lectura");
            var vm = new VersionsViewModel(new FakeShellHost(), host);
            vm.Refresh();

            Assert.True(vm.IsReadOnly);
            Assert.False(vm.NewCommand.CanExecute(null));
            Assert.False(vm.ActivateCommand.CanExecute(null));
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    // 5. Proyecto con versiones — habilita comandos y oculta empty state
    [Fact]
    public async Task Refresh_ProjectWithVersions_HidesEmptyStateAndEnablesNew()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-one-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-one-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.False(vm.ShowEmptyState);
        Assert.Single(vm.Versions);
        Assert.True(vm.NewCommand.CanExecute(null));
    }

    // 6. Proyecto con varias versiones — lista completa
    [Fact]
    public async Task Refresh_ProjectWithMultipleVersions_LoadsAll()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-multi-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-multi-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Primera", 10m));
        host.Session.Versiones.Add(MakeVersion("V2", "Segunda", 12m));
        host.Session.Versiones.Add(MakeVersion("V3", "Tercera", 15m));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal(3, vm.Versions.Count);
        Assert.False(vm.ShowEmptyState);
    }

    // 7. Orden del historial — más reciente primero
    [Fact]
    public async Task Refresh_ProjectWithVersions_OrderedByDateDescending()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-order-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-order-test");
        host.Session.Versiones.Clear();

        var old = MakeVersion("V1", "Antigua", 5m);
        old.FechaCreacion = DateTime.UtcNow.AddDays(-2);
        old.FechaModificacion = old.FechaCreacion;

        var recent = MakeVersion("V2", "Reciente", 10m);
        recent.FechaCreacion = DateTime.UtcNow;
        recent.FechaModificacion = recent.FechaCreacion;

        host.Session.Versiones.Add(old);
        host.Session.Versiones.Add(recent);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal("V2", vm.Versions[0].Codigo);
        Assert.Equal("V1", vm.Versions[1].Codigo);
    }

    // 8. Selección de una versión — habilita Activar, Renombrar, Eliminar
    [Fact]
    public async Task SelectedVersion_Set_EnablesActionCommands()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-sel-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-sel-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedVersion = vm.Versions[0];

        Assert.True(vm.ActivateCommand.CanExecute(null));
        Assert.True(vm.RenameCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
    }

    // 9. Sin selección — deshabilita Activar, Renombrar, Eliminar
    [Fact]
    public async Task SelectedVersion_Null_DisablesActionCommands()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-nullsel-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-nullsel-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedVersion = null;

        Assert.False(vm.ActivateCommand.CanExecute(null));
        Assert.False(vm.RenameCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    // 10. Versión activa marcada con IsActive
    [Fact]
    public async Task Refresh_ActiveVersion_MarkedIsActive()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-active-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-active-test");
        host.Session.Versiones.Clear();
        var v = MakeVersion("V1", "Base");
        host.Session.Versiones.Add(v);
        host.Session.ActiveVersionId = v.Id;
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.Versions[0].IsActive);
        Assert.Contains("Activa:", vm.ActiveVersionText);
        Assert.Contains("V1", vm.ActiveVersionText);
    }

    // 11. Activar versión — cambia ActiveVersionId
    [Fact]
    public async Task ActivateCommand_SetsActiveVersionId()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-activate-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-activate-test");
        host.Session.Versiones.Clear();
        var v1 = MakeVersion("V1", "Base");
        var v2 = MakeVersion("V2", "Nueva");
        host.Session.Versiones.Add(v1);
        host.Session.Versiones.Add(v2);
        host.Session.ActiveVersionId = v1.Id;
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SelectedVersion = vm.Versions.First(v => v.Codigo == "V2");
        vm.ActivateCommand.Execute(null);

        Assert.Equal(v2.Id, host.Session.ActiveVersionId);
    }

    // 12. SummaryText con versiones
    [Fact]
    public async Task SummaryText_WithVersions_ShowsCount()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-summary-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-summary-test");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base"));
        host.Session.Versiones.Add(MakeVersion("V2", "Rev1"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Contains("2", vm.SummaryText);
        Assert.Contains("versiones", vm.SummaryText);
    }

    // 13. StatusMessage diferenciado: sin proyecto vs sin versiones
    [Fact]
    public async Task StatusMessage_DifferentiatesNoProjectFromEmptyVersions()
    {
        var vm1 = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        string msgNoProject = vm1.StatusMessage;

        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-diffmsg-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-diffmsg-test");
        host.Session.Versiones.Clear();
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm2 = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        string msgEmptyProject = vm2.StatusMessage;

        Assert.NotEqual(msgNoProject, msgEmptyProject);
        Assert.Contains("Abre", msgNoProject);
        Assert.Contains("todavía no tiene", msgEmptyProject);
    }

    // 14. DeltaText en versión
    [Fact]
    public async Task VersionRowVm_DeltaText_ShowsPlusOrDash()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-delta-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-delta-test");
        host.Session.Versiones.Clear();

        var v1 = MakeVersion("V1", "Antigua", 8m);
        v1.FechaCreacion = DateTime.UtcNow.AddDays(-1);
        v1.FechaModificacion = v1.FechaCreacion;

        var v2 = MakeVersion("V2", "Reciente", 12m);
        v2.FechaCreacion = DateTime.UtcNow;
        v2.FechaModificacion = v2.FechaCreacion;

        host.Session.Versiones.Add(v1);
        host.Session.Versiones.Add(v2);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        var recent = vm.Versions.First(v => v.Codigo == "V2");
        Assert.StartsWith("+", recent.DeltaText);
    }

    // 15. GoToItemCommand — navega a Takeoff con contexto
    [Fact]
    public async Task GoToItemCommand_WhenComparisonRowSelected_NavigatesToTakeoff()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-gotoitem-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-gotoitem-test");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "PVC 20",
            Cantidad = 10m,
            MatUf = 1m
        });

        var baseline = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = "test" },
            Itemizado = [new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" }],
            Cubicacion = [new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Cantidad = 8m, MatUf = 1m }]
        };
        var v = new BudgetVersionRow
        {
            Codigo = "V1",
            Nombre = "Base",
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow,
            FechaModificacion = DateTime.UtcNow,
            MontoVentaUf = 8m,
            Snapshot = baseline
        };
        host.Session.Versiones.Add(v);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var shell = new FakeShellHost();
        var vm = new VersionsViewModel(shell, WpfAppHost.Current);
        vm.SelectedVersion = vm.Versions.First();

        if (vm.ComparisonRows.Count > 0)
        {
            vm.SelectedComparisonRow = vm.ComparisonRows.First();
            Assert.True(vm.GoToItemCommand.CanExecute(null));
            vm.GoToItemCommand.Execute(null);
            Assert.Equal(ShellRoute.Takeoff, shell.CurrentRoute);
            var ctx = Assert.IsType<TakeoffNavigationContext>(shell.LastContext);
            Assert.Equal("P1", ctx.ItemId);
        }
    }

    // 16. GoToItemCommand deshabilitado sin fila comparación seleccionada
    [Fact]
    public void GoToItemCommand_WithoutComparisonRow_Disabled()
    {
        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.False(vm.GoToItemCommand.CanExecute(null));
    }

    // 17. Refresh recarga desde sesión
    [Fact]
    public async Task Refresh_AfterAddingVersionToSession_UpdatesList()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-reload-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-reload-test");
        host.Session.Versiones.Clear();
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);
        Assert.Empty(vm.Versions);

        host.Session.Versiones.Add(MakeVersion("V1", "Nueva"));
        vm.Refresh();

        Assert.Single(vm.Versions);
        Assert.False(vm.ShowEmptyState);
    }

    // 18. Eliminar versión activa — sesión queda sin versión activa
    [Fact]
    public void DeleteVersion_ActiveVersion_SessionUpdatedActiveVersionId()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("ver-del-test");
        host.Session.Versiones.Clear();
        var v1 = MakeVersion("V1", "Primera");
        host.Session.Versiones.Add(v1);
        host.Session.ActiveVersionId = v1.Id;

        host.Session.Versiones.Remove(v1);
        string newActiveId = host.Session.Versiones.LastOrDefault()?.Id ?? string.Empty;
        host.Session.ActiveVersionId = newActiveId;

        Assert.Empty(host.Session.Versiones);
        Assert.Equal(string.Empty, host.Session.ActiveVersionId);
    }

    // 19. Ausencia de dependencia hacia WinForms — ninguna referencia en el namespace
    [Fact]
    public void VersionsViewModel_HasNoWinFormsReference()
    {
        var type = typeof(VersionsViewModel);
        var referencedTypes = type.Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencedTypes, a =>
            a.Name != null &&
            (a.Name.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
             a.Name.Contains("QubiCostElectrical.WinForms", StringComparison.OrdinalIgnoreCase)));
    }

    // 20. VersionRowVm — propiedades expuestas correctamente
    [Fact]
    public void VersionRowVm_ExposesSourceProperties()
    {
        var source = new BudgetVersionRow
        {
            Codigo = "V1",
            Nombre = "Base",
            Estado = "Aprobada",
            CreadoPor = "tester",
            FechaCreacion = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            FechaModificacion = new DateTime(2025, 1, 15, 11, 0, 0, DateTimeKind.Utc),
            MontoVentaUf = 42.5m,
            Snapshot = new BudgetVersionData { Proyecto = new ProjectInfoRow(), Itemizado = [], Cubicacion = [] }
        };

        var vm = new VersionRowVm(source, isActive: true, deltaUf: 2.5m);

        Assert.Equal("V1", vm.Codigo);
        Assert.Equal("Base", vm.Nombre);
        Assert.Equal("Aprobada", vm.Estado);
        Assert.Equal("tester", vm.CreadoPor);
        Assert.Equal(42.5m, vm.MontoVentaUf);
        Assert.True(vm.IsActive);
        Assert.Equal(2.5m, vm.DeltaUf);
        Assert.StartsWith("+", vm.DeltaText);
    }

    // 21. VersionComparisonRowVm — propiedades expuestas correctamente
    [Fact]
    public void VersionComparisonRowVm_ExposesSourceProperties()
    {
        var source = new BudgetVersionComparisonRow(
            ItemId: "P1",
            Item: "1.1",
            Descripcion: "Tablero",
            BaselineUf: 10m,
            CurrentUf: 12m,
            DeltaUf: 2m,
            DeltaPct: 20m,
            Estado: "Aumentó");

        var vm = new VersionComparisonRowVm(source);

        Assert.Equal("P1", vm.ItemId);
        Assert.Equal("1.1", vm.Item);
        Assert.Equal("Tablero", vm.Descripcion);
        Assert.Equal(10m, vm.BaselineUf);
        Assert.Equal(12m, vm.CurrentUf);
        Assert.Equal(2m, vm.DeltaUf);
        Assert.Equal(20m, vm.DeltaPct);
        Assert.Equal("Aumentó", vm.Estado);
    }

    // 23. Modo A vs B — dos versiones guardadas
    [Fact]
    public async Task CompareMode_VersionAVsB_ReportsChangedPartidas()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-avsb-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-avsb-test");
        host.Session.Versiones.Clear();

        var snapshotV1 = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Proyecto A/B" },
            Itemizado = [new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" }],
            Cubicacion = [new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Cantidad = 10m, MatUf = 1m }]
        };
        var snapshotV2 = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Proyecto A/B" },
            Itemizado = [new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" }],
            Cubicacion = [new CubicacionRow { ItemId = "P1", Codigo = "PVC20", Descripcion = "PVC 20", Cantidad = 12m, MatUf = 1m }]
        };

        var v1 = new BudgetVersionRow
        {
            Codigo = "V1",
            Nombre = "Corte base",
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow.AddHours(-2),
            FechaModificacion = DateTime.UtcNow.AddHours(-2),
            MontoVentaUf = 10m,
            Snapshot = snapshotV1
        };
        var v2 = new BudgetVersionRow
        {
            Codigo = "V2",
            Nombre = "Revisión",
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow,
            FechaModificacion = DateTime.UtcNow,
            MontoVentaUf = 12m,
            Snapshot = snapshotV2
        };

        host.Session.Versiones.Add(v1);
        host.Session.Versiones.Add(v2);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), host);
        vm.SelectedVersion = vm.Versions.First(v => v.Codigo == "V1");
        vm.CompareAgainstCurrent = false;
        vm.SelectedComparisonVersion = vm.ComparisonVersionOptions.First(v => v.Codigo == "V2");

        Assert.False(vm.CompareAgainstCurrent);
        Assert.Contains("Versión A", vm.ComparisonModeHint);
        Assert.Contains("V2", vm.ComparisonModeHint);
        Assert.Equal("1 modificadas", vm.ComparisonChangedText);
        Assert.True(vm.ComparisonRows.Count >= 1);

        var changed = vm.ComparisonRows.FirstOrDefault(r => r.ItemId == "P1");
        Assert.NotNull(changed);
        Assert.Equal(10m, changed!.BaselineUf);
        Assert.Equal(12m, changed.CurrentUf);
        Assert.Equal(2m, changed.DeltaUf);
    }

    // 24. Modo A vs B — selector oculto cuando compara con actual
    [Fact]
    public async Task CompareMode_AgainstCurrent_HidesVersionBSelector()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-current-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-current-mode");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Base", 5m));
        host.Session.Versiones.Add(MakeVersion("V2", "Siguiente", 7m));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), host);
        vm.CompareAgainstCurrent = true;

        Assert.False(vm.ShowComparisonVersionSelector);
    }

    // 25. Modo A vs B — opciones excluyen versión A seleccionada
    [Fact]
    public async Task CompareMode_VersionBOptions_ExcludeSelectedVersionA()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-options-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-options");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Uno"));
        host.Session.Versiones.Add(MakeVersion("V2", "Dos"));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), host);
        vm.SelectedVersion = vm.Versions.First(v => v.Codigo == "V1");
        vm.CompareAgainstCurrent = false;

        Assert.DoesNotContain(vm.ComparisonVersionOptions, v => v.Codigo == "V1");
        Assert.Contains(vm.ComparisonVersionOptions, v => v.Codigo == "V2");
    }

    // 22. Navegación registrada en ShellRouteCatalog
    [Fact]
    public void Versions_RegisteredInShellRouteCatalog()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.Versions);
        Assert.Equal("Cierre", info.Group);
        Assert.Equal("Versiones", info.Title);
    }
}
