using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DashboardViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
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

    /// <summary>
    /// Los pinceles de tema se resuelven por recurso o por color de respaldo, así que en pruebas
    /// pueden ser instancias distintas: se compara el tono, no la referencia.
    /// </summary>
    private static void AssertSameColor(Brush expected, Brush actual)
        => Assert.Equal(expected.ToString(), actual.ToString());

    [Fact]
    public async Task Load_WithOpenProject_PopulatesCostDistributionAndKpiAccents()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("dashboard-visual-test");
        host.Session.ProjectInfo.Nombre = "Proyecto visual";
        host.Session.ProjectInfo.MetrosCuadrados = 120m;

        var path = Path.Combine(Path.GetTempPath(), $"dashboard-visual-{Guid.NewGuid():N}.qce");
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new DashboardViewModel(new FakeShell(), WpfAppHost.Current);
        vm.Load();

        Assert.False(vm.ShowEmptyState);
        Assert.Equal(6, vm.KpiCards.Count);
        Assert.Equal(6, vm.WorkflowSteps.Count);
        Assert.Equal("TOTAL VENTA", vm.KpiCards[0].Title);
        Assert.Equal("PARTIDAS LISTAS", vm.KpiCards[1].Title);
        Assert.Equal("MARGEN REAL", vm.KpiCards[4].Title);
        Assert.Equal("CONCENTRACIÓN A", vm.KpiCards[5].Title);
        Assert.NotNull(vm.Waterfall);
        Assert.InRange(vm.Waterfall.Rows.Count, 4, 11);
        Assert.Equal("Costo de ejecución", vm.Waterfall.Rows[0].Label);
        Assert.Contains("TOTAL VENTA", vm.Waterfall.Rows[^1].Label);
        Assert.True(vm.Waterfall.Rows[^1].IsGrandTotal);
        // Ningún escalón opcional puede quedar en blanco: los recargos en cero se omiten.
        Assert.DoesNotContain(vm.Waterfall.Rows, r => r.AmountLabel == "0");
        Assert.True(vm.Readiness.TotalPartidas >= 0);
        Assert.False(string.IsNullOrWhiteSpace(vm.VerdictTitle));
        Assert.False(string.IsNullOrWhiteSpace(vm.VerdictKind));
        Assert.False(string.IsNullOrWhiteSpace(vm.VerdictActionLabel));
        Assert.False(string.IsNullOrWhiteSpace(vm.BlockingAlertLabel));
        Assert.Contains("bloqueante", vm.BlockingAlertLabel);
        // Una sola fuente del texto: el chip comparte el rótulo con el compositor.
        Assert.Equal(DashboardCockpitComposer.BlockingLabel(vm.BlockingAlertCount), vm.BlockingAlertLabel);
        Assert.Equal(4, vm.CostShares.Count);
        Assert.Equal(GridUnitType.Star, vm.MaterialShareWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, vm.LaborShareWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, vm.GoShareWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, vm.MarginShareWidth.GridUnitType);
    }

    [Fact]
    public async Task Load_WithoutProject_ShowsLandingScreenAndBuildsSummaryFromRecentFiles()
    {
        WpfTestHost.EnsureInitialized();

        var prefsPath = Path.Combine(Path.GetTempPath(), $"dashboard-recent-{Guid.NewGuid():N}.json");
        using var prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(prefsPath);

        var host = WpfAppHost.Current;
        var repo = host.Services.ProjectRepository;

        string CreateProject(string prefix, string estado)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.qce");
            repo.SaveSQLiteSnapshot(path, new ProjectFileData
            {
                Proyecto = new ProjectInfoRow
                {
                    Nombre = prefix,
                    Cliente = "Cliente",
                    Ubicacion = "Obra",
                    EstadoPresupuesto = estado,
                    FechaModificacion = DateTime.UtcNow
                }
            });
            return path;
        }

        var completePath = CreateProject("proyecto-completo", "Completo");
        var progressPath = CreateProject("proyecto-progreso", "En progreso");
        var draftPath = CreateProject("proyecto-borrador", "Borrador");

        var recent = new RecentProjectsService();
        recent.AddProject(draftPath);
        recent.AddProject(progressPath);
        recent.AddProject(completePath);

        var vm = new DashboardViewModel(new FakeShell(), host);
        vm.Load();
        if (vm.LastRefreshTask != null) await vm.LastRefreshTask;

        Assert.True(vm.ShowEmptyState);
        Assert.True(vm.HasRecentProjects);
        Assert.Equal(3, vm.RecentProjects.Count);
        Assert.Equal(4, vm.QuickSummaryCards.Count);
        Assert.Equal("3", vm.QuickSummaryCards[0].Value);
        Assert.Equal("1", vm.QuickSummaryCards[1].Value);
        Assert.Equal("1", vm.QuickSummaryCards[2].Value);
        Assert.Equal("1", vm.QuickSummaryCards[3].Value);
        Assert.Equal("Completo", vm.RecentProjects[0].StatusText);
        Assert.Equal("En progreso", vm.RecentProjects[1].StatusText);
        Assert.Equal("Borrador", vm.RecentProjects[2].StatusText);
        Assert.Empty(vm.KpiCards);
        Assert.Empty(vm.CostShares);
        Assert.Empty(vm.WorkflowSteps);
        Assert.Equal(0d, vm.MaterialShareWidth.Value);
    }

    // Regresión: la pantalla de Datos del proyecto ahora ofrece "Aprobado" como estado fijo
    // (antes el campo era texto libre), y el Dashboard debe reconocerlo como "Completo" en
    // vez de caer al valor por defecto "Borrador" (que era el comportamiento antes de este fix).
    [Fact]
    public async Task Load_WithoutProject_ClassifiesAprobadoAsComplete()
    {
        WpfTestHost.EnsureInitialized();

        var prefsPath = Path.Combine(Path.GetTempPath(), $"dashboard-aprobado-{Guid.NewGuid():N}.json");
        using var prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(prefsPath);

        var host = WpfAppHost.Current;
        var repo = host.Services.ProjectRepository;

        var path = Path.Combine(Path.GetTempPath(), $"proyecto-aprobado-{Guid.NewGuid():N}.qce");
        repo.SaveSQLiteSnapshot(path, new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "proyecto-aprobado",
                Cliente = "Cliente",
                Ubicacion = "Obra",
                EstadoPresupuesto = "Aprobado",
                FechaModificacion = DateTime.UtcNow
            }
        });

        var recent = new RecentProjectsService();
        recent.AddProject(path);

        var vm = new DashboardViewModel(new FakeShell(), host);
        vm.Load();
        if (vm.LastRefreshTask != null) await vm.LastRefreshTask;

        Assert.Equal("Completo", vm.RecentProjects[0].StatusText);
    }

    // Regresión: el usuario reporta que la app "se queda pegada" o "se cierra inesperadamente"
    // tras usarla un rato — un patrón típico es abrir y cerrar proyectos repetidamente sin
    // reiniciar la app, ya que cada cierre vuelve a esta pantalla (Load() se llama de nuevo).
    // Este test simula 10 ciclos de abrir/cerrar sobre la MISMA instancia de ViewModel (como
    // pasaría con el cacheo de páginas del shell) y verifica que ninguna colección crezca sin
    // límite (señal de un Clear() olvidado) y que no se lance ninguna excepción.
    [Fact]
    public async Task Load_OpenAndCloseProjectTenTimes_DoesNotThrowOrAccumulateState()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var vm = new DashboardViewModel(new FakeShell(), host);

        for (int cycle = 0; cycle < 10; cycle++)
        {
            host.Session.NewProject($"ciclo-{cycle}");
            host.Session.ProjectInfo.Nombre = $"Proyecto ciclo {cycle}";
            host.Session.ProjectInfo.MetrosCuadrados = 100m;
            host.Session.Itemizado.Add(new ItemizadoRow { Id = $"item-{cycle}", Item = "1", Descripcion = "Partida", Cantidad = 1m });
            host.Session.Cubicacion.Add(new CubicacionRow { Id = Guid.NewGuid().ToString("N"), ItemId = $"item-{cycle}", Codigo = "MAT-1", Cantidad = 1m, MatUf = 1m });

            var path = Path.Combine(Path.GetTempPath(), $"dashboard-cycle-{cycle}-{Guid.NewGuid():N}.qce");
            await host.ProjectWorkspace.SaveAsAsync(path);

            vm.Load();
            Assert.False(vm.ShowEmptyState);
            Assert.Equal(6, vm.KpiCards.Count);
            Assert.Equal(4, vm.CostShares.Count);
            Assert.Equal(6, vm.WorkflowSteps.Count);

            // "Cerrar" el proyecto: NewProject() deja CurrentProjectPath vacío, que es lo
            // que HasOpenProject usa para decidir si mostrar la pantalla de bienvenida.
            host.Session.NewProject($"cerrado-{cycle}");
            vm.Load();
            Assert.True(vm.ShowEmptyState);
            Assert.Empty(vm.KpiCards);
            Assert.Empty(vm.CostShares);
            Assert.Empty(vm.WorkflowSteps);
        }
    }

    [Fact]
    public void DashboardLandingCoordinator_MissingArchiveIsAmberWithReason()
    {
        // Archivo ausente: manda por sobre el semáforo de avance y explica el motivo.
        var missing = DashboardLandingCoordinator.DescribeArchiveState(true, DashboardProjectStateKind.Unknown);
        Assert.Equal("Archivo no encontrado", missing.Text);
        Assert.Contains("ya no existe", missing.Caption);
        AssertSameColor(DashboardKpiBrushes.Amber, missing.Accent);
        AssertSameColor(DashboardKpiBrushes.AmberSoft, missing.Soft);

        // Archivo presente: el semáforo de avance no se contamina.
        var draft = DashboardLandingCoordinator.DescribeArchiveState(false, DashboardProjectStateKind.Draft);
        Assert.Equal("Borrador", draft.Text);
        AssertSameColor(DashboardKpiBrushes.Blue, draft.Accent);

        var complete = DashboardLandingCoordinator.DescribeArchiveState(false, DashboardProjectStateKind.Complete);
        Assert.Equal("Completo", complete.Text);
        AssertSameColor(DashboardKpiBrushes.Green, complete.Accent);

        var progress = DashboardLandingCoordinator.DescribeArchiveState(false, DashboardProjectStateKind.InProgress);
        Assert.Equal("En progreso", progress.Text);
        AssertSameColor(DashboardKpiBrushes.Amber, progress.Accent);

        var unknown = DashboardLandingCoordinator.DescribeArchiveState(false, DashboardProjectStateKind.Unknown);
        Assert.Equal("Sin estado", unknown.Text);
        AssertSameColor(DashboardKpiBrushes.BlueDark, unknown.Accent);
    }

    [Fact]
    public void DashboardLandingCoordinator_ExposesFourTemplatesWithSecNorm()
    {
        var templates = DashboardLandingCoordinator.CreateQuickTemplates(new FakeShell()).ToList();

        Assert.Equal(4, templates.Count);
        Assert.All(templates, t => Assert.False(string.IsNullOrWhiteSpace(t.NormaSec)));
        Assert.All(templates, t => Assert.NotNull(t.UseTemplateCommand));
        Assert.Contains(templates, t => t.Id == "residential_building");
        Assert.Contains(templates, t => t.Id == "solar_pv_plant");
    }

    [Fact]
    public void DashboardCockpitComposer_VerdictPhrasesAgreeInNumber()
    {
        var bad = DashboardCockpitComposer.BuildVerdict(
            DashboardReadinessContract.Build(10, 0, 0, 1, 0), "");
        Assert.Contains("1 alerta bloqueante impide cerrar la oferta", bad.Reason);
        Assert.Equal("bad", bad.Kind);

        var badMany = DashboardCockpitComposer.BuildVerdict(
            DashboardReadinessContract.Build(10, 0, 0, 3, 0), "");
        Assert.Contains("3 alertas bloqueantes impiden cerrar la oferta", badMany.Reason);

        var warn = DashboardCockpitComposer.BuildVerdict(
            DashboardReadinessContract.Build(10, 1, 0, 0, 0), "");
        Assert.Contains("1 pendiente", warn.Reason);
        Assert.DoesNotContain("1 pendientes", warn.Reason);
        Assert.Equal("warn", warn.Kind);

        var ok = DashboardCockpitComposer.BuildVerdict(
            DashboardReadinessContract.Build(10, 0, 0, 0, 0), "");
        Assert.Equal("ok", ok.Kind);
        Assert.Equal("Apta para ofertar", ok.Title);

        Assert.Equal("1 bloqueante", DashboardCockpitComposer.BlockingLabel(1));
        Assert.Equal("2 bloqueantes", DashboardCockpitComposer.BlockingLabel(2));
    }

    [Fact]
    public void DashboardCockpitComposer_OmitsZeroStepsButAlwaysKeepsMilestones()
    {
        var cost = new CostBreakdown
        {
            CostoDirectoUf = 100m,
            RiesgoUf = 0m,
            GastosGeneralesUf = 5m,
            UtilidadUf = 8m,
            ValorAgregadoEquiposUf = 0m,
            DescuentoUf = 0m,
            TotalVentaNetoUf = 120m,
            IvaUf = 22.8m,
            TotalVentaConIvaUf = 142.8m,
            TotalVentaUf = 142.8m
        };
        var policy = new DashboardWaterfallPolicy(0m, 5m, 8m, 0m, 0m, 19m, true);

        var waterfall = DashboardCockpitComposer.BuildWaterfall(cost, policy, v => v.ToString("N0"));

        var labels = waterfall.Rows.Select(r => r.Label).ToList();
        // Hitos invariantes: nunca se omiten aunque el proyecto no tenga recargos.
        Assert.Equal("Costo de ejecución", labels[0]);
        Assert.Contains("Costo total proyecto", labels);
        Assert.Contains("Venta neta", labels);
        Assert.Contains(labels, l => l.StartsWith("TOTAL VENTA"));
        Assert.True(waterfall.Rows[^1].IsGrandTotal);

        // Recargos en cero: sin fila en blanco.
        Assert.DoesNotContain(waterfall.Rows, r => r.AmountLabel == "0");
        Assert.DoesNotContain(labels, l => l.StartsWith("Riesgo ("));
        Assert.DoesNotContain(labels, l => l.StartsWith("VA sobre equipos ("));
        Assert.DoesNotContain(labels, l => l.StartsWith("Descuento comercial ("));

        // Sin descuento, la diferencia por factores cierra la estructura contra el neto.
        Assert.Contains("Diferencia por factores", labels);
        Assert.Equal("IVA (19,0%)", labels[labels.Count - 2]);
    }

    [Fact]
    public void DashboardParetoSummary_Calculate_CorrectlyClassifiesABC()
    {
        var partidas = new List<DashboardPartidaVm>
        {
            new() { Rank = 1, Item = "1.1", Partida = "Tablero General", SharePct = 50.0, AccumPct = 50.0 },
            new() { Rank = 2, Item = "1.2", Partida = "Alimentador Principal", SharePct = 30.0, AccumPct = 80.0 },
            new() { Rank = 3, Item = "1.3", Partida = "Subalimentador", SharePct = 15.0, AccumPct = 95.0 },
            new() { Rank = 4, Item = "1.4", Partida = "Canalización", SharePct = 5.0, AccumPct = 100.0 }
        };

        var summary = DashboardParetoSummary.Calculate(partidas, 100_000_000m, 38_000m);

        Assert.True(summary.HasData);
        Assert.Equal(2, summary.CountA); // 50% + 30% <= 80.5%
        Assert.Equal(1, summary.CountB); // 95%
        Assert.Equal(1, summary.CountC); // 100%
        Assert.Equal(80.0, summary.PctA);
        Assert.Equal(15.0, summary.PctB);
        Assert.Equal(5.0, summary.PctC);
        Assert.Contains("Tipo A", summary.BadgeA);
        Assert.Contains("Tipo B", summary.BadgeB);
        Assert.Contains("Tipo C", summary.BadgeC);
        Assert.Equal("A", partidas[0].ParetoClass);
        Assert.Equal("A", partidas[1].ParetoClass);
        Assert.Equal("B", partidas[2].ParetoClass);
        Assert.Equal("C", partidas[3].ParetoClass);
    }

    [Fact]
    public void QuickTemplates_ContainsRichRicTemplates()
    {
        var templates = DashboardLandingCoordinator.CreateQuickTemplates(new FakeShell()).ToList();
        Assert.Equal(4, templates.Count);
        Assert.Contains(templates, t => t.Id == "residential_building");
        Assert.Contains(templates, t => t.Id == "industrial_warehouse");
        Assert.Contains(templates, t => t.Id == "commercial_retail");
        Assert.Contains(templates, t => t.Id == "solar_pv_plant");
    }
}
