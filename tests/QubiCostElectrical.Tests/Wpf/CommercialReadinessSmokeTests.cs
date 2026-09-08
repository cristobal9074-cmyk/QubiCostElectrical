using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Smoke de preparación comercial (semana A): persistencia de fórmulas, undo tras pegar,
/// visor/solo lectura en APU y SC, Tomar SC, y matriz de roles.
/// </summary>
/// <remarks>
/// Ya cubierto — no se duplica:
/// <list type="bullet">
/// <item><see cref="CubicacionViewModelTests.PasteClipboardDirect_FormulaLikeQuantity_StoresExpressionAndEvaluates"/> (10+2 → 12 en memoria)</item>
/// <item><see cref="CubicacionViewModelTests.PasteClipboardDirect_ReadOnlySession_DoesNotAddRows"/></item>
/// <item><see cref="CubicacionViewModelTests.ReadOnly_DisablesAddRow"/></item>
/// <item><see cref="CubicacionViewModelTests.ReadOnly_PartidaHeaderSetters_DoNotMutateOrMarkDirty"/></item>
/// <item><see cref="CubicacionScReadOnlyTests"/> (setters SC Visor + dirty estimador; no duplicar AddCircuito/AddCuadro aquí)</item>
/// <item><see cref="VisorSixModulesReadOnlyTests"/> (Visor demo-proof: Itemizado, Cubicación, SC, APU, Costo, Oferta/Export)</item>
/// <item><see cref="QubiCostElectrical.Tests.Infrastructure.SQLiteProjectStoreTests.SaveSnapshot_ThenLoad_PreservesTotalExpressionOnCubicacionRow"/> (store, expresión textual)</item>
/// <item><see cref="QubiCostElectrical.Tests.Domain.UserRolePermissionsTests.Role_matrix"/> y <see cref="QubiCostElectrical.Tests.Application.UserRolePermissionServiceTests"/></item>
/// <item><see cref="QubiCostElectrical.Tests.Domain.CubicacionSCEngineTests"/> (Aggregate / AggregateConZonas)</item>
/// </list>
/// </remarks>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CommercialReadinessSmokeTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
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

    public CommercialReadinessSmokeTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task Cubicacion_Formula_SaveAndReopen_KeepsQuantityAndExpression()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"crs-formula-{Guid.NewGuid():N}.qce");
        try
        {
            host.Session.NewProject("crs-formula");
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P1",
                Item = "1.1",
                Descripcion = "Canalización",
                Unidad = "ML",
                Cantidad = 10m
            });

            var vm = new CubicacionPageVm(new FakeShell(), host);
            vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
            vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t10+2");

            var before = Assert.Single(host.Session.Cubicacion);
            Assert.Equal("10+2", before.TotalExpression);
            Assert.Equal(12m, before.Cantidad);

            await host.ProjectWorkspace.SaveAsAsync(path);
            await host.ProjectWorkspace.OpenAsync(path);

            var reloaded = Assert.Single(host.Session.Cubicacion);
            Assert.Equal("10+2", reloaded.TotalExpression);
            Assert.Equal(12m, reloaded.Cantidad);
        }
        finally
        {
            CleanupQce(path);
        }
    }

    [Fact]
    public void PasteClipboardDirect_ThenUndo_RestoresRows()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("crs-undo-paste");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        host.UndoService.Clear();

        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t15\nPVC25\tTubo PVC 25mm\tML\t8");
        Assert.Equal(2, host.Session.Cubicacion.Count);
        Assert.True(host.UndoService.CanUndo);

        host.UndoService.Undo(host.Session);

        Assert.Empty(host.Session.Cubicacion);
    }

    [Fact]
    public void ReadOnlySession_ApuAndSc_CannotEdit()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("crs-readonly-modules");
        host.Session.CurrentProjectPath = "crs-readonly-modules.qce";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1",
            Descripcion = "Partida",
            Unidad = "GL",
            Cantidad = 1m
        });
        host.Session.Apus.Add(new ApuRow
        {
            ItemId = "P1",
            Descripcion = "Partida",
            Unidad = "GL",
            Recursos =
            [
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Cable", Rendimiento = 1m, PrecioUf = 1m }
            ]
        });

        var sc = new CubicacionScViewModel(new FakeShell(), host);
        var apu = new ApuWorkspaceVm(new FakeShell(), host);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            Assert.False(sc.CanEdit);
            Assert.False(sc.AddCircuitoCommand.CanExecute(null));
            Assert.False(sc.AddCuadroCommand.CanExecute(null));

            Assert.False(apu.CanEdit);
            Assert.False(apu.AddRecursoCommand.CanExecute(null));
            Assert.False(apu.SaveCommand.CanExecute(null));
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void CubicacionSc_CuadroCircuitoTomar_ProducesNonZeroMaterials()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("crs-sc-tomar");
        host.Session.CurrentProjectPath = "crs-sc-tomar.qce";

        var sc = new CubicacionScViewModel(new FakeShell(), host);
        Assert.NotEmpty(sc.Cuadros);
        sc.AddCircuitoCommand.Execute(null);
        var circuito = sc.SelectedCircuito;
        Assert.NotNull(circuito);

        circuito!.TipoTuberia = "PVCLH";
        circuito.SeccionTuberia = "20mm";
        circuito.ConductorDerivacion = "H07V-U";
        circuito.SeccionDerivacion = "4mm2";
        circuito.LargoTuberia = 20m;

        Assert.True(sc.TryApplyChanges());
        Assert.NotEmpty(host.Session.CubicacionSC);

        MaterialConZonasVm? tomado = null;
        var tomarVm = new ResumenScTomarViewModel(
            host.Session.CubicacionSC.ToList(),
            host.Session.CubicacionScConfig,
            host.Session.ZonasProyecto.ToList());

        var materiales = tomarVm.FlatRows.OfType<MaterialConZonasVm>().ToList();
        Assert.NotEmpty(materiales);
        Assert.Contains(materiales, m => m.Total > 0m);

        tomarVm.OnTomar = row => tomado = row;
        var primero = materiales.First(m => m.Total > 0m);
        Assert.True(tomarVm.TomarCommand.CanExecute(primero));
        tomarVm.TomarCommand.Execute(primero);

        Assert.NotNull(tomado);
        Assert.Same(primero, tomado);
        Assert.True(tomado!.Total > 0m, "Tomar debe entregar un material con cantidad distinta de cero.");
    }

    [Fact]
    public void Roles_AdminEstimatorViewer_MatchPublishedPermissionMatrix()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanExportDocuments(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Viewer));

        Assert.True(UserRolePermissions.CanEditBudget(UserRole.Estimator));
        Assert.True(UserRolePermissions.CanExportDocuments(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Estimator));

        Assert.True(UserRolePermissions.CanEditBudget(UserRole.Admin));
        Assert.True(UserRolePermissions.CanExportDocuments(UserRole.Admin));
        Assert.True(UserRolePermissions.CanManageAppSettings(UserRole.Admin));
    }

    private static void CleanupQce(string path)
    {
        foreach (var candidate in new[] { path, path + ".autosave", path + ".lock" })
        {
            try
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
            catch
            {
                // ignore leftover temp files
            }
        }
    }
}
