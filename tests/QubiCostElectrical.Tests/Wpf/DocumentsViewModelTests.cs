using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Documents;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DocumentsViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Documents;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) { CurrentRoute = route; return true; }
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => RequestNavigate(route, force);
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

    public DocumentsViewModelTests() => WpfTestHost.EnsureInitialized();

    // Regresión: AddNew() llamaba ApplyFilter() + NotifyKpisChanged() UNA VEZ POR ARCHIVO al
    // importar una carpeta completa (habitual: decenas o cientos de planos). Con la lógica ya
    // extraída a AddFiles(tipo, filePaths[]), se verifica que agregar muchos archivos de una
    // vez deja el estado final correcto (todas las filas agregadas, sin duplicados por código,
    // fila seleccionada = la última, resumen actualizado) sin depender del diálogo real de
    // selección de archivos.
    [Fact]
    public void AddFiles_MassImport_AddsAllRowsAndSelectsLast()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("docs-mass-import");
        host.Session.CurrentProjectPath = "docs-mass-import.qce";
        var vm = new DocumentsViewModel(new FakeShell(), host);
        vm.Load();

        var filePaths = Enumerable.Range(0, 150)
            .Select(i => $"C:\\Planos\\PLANO-{i:0000}.pdf")
            .ToArray();

        vm.AddFiles(AntecedenteTecnicoTipo.Plano, filePaths);

        Assert.Equal(150, vm.FilteredRows.Count);
        Assert.Equal("PLANO-0149", vm.SelectedRow?.Codigo);
        Assert.True(vm.HasPendingChanges);
        Assert.Equal(150, vm.KpiPlanosCount);
    }

    [Fact]
    public void AddFiles_DuplicateCodigoAcrossCalls_SkipsAndReportsCount()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("docs-dedup");
        host.Session.CurrentProjectPath = "docs-dedup.qce";
        var vm = new DocumentsViewModel(new FakeShell(), host);
        vm.Load();

        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\E-01.pdf" });
        // Segunda importación con el mismo código: debe omitirse (ya existe un antecedente
        // "Plano" con código E-01), no crear un duplicado.
        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Otros\\E-01.pdf", "C:\\Otros\\E-02.pdf" });

        Assert.Equal(2, vm.FilteredRows.Count);
        Assert.Contains("omitido", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Regresión (patrón general de la app): abrir y cerrar el proyecto repetidamente no debe
    // lanzar excepciones ni dejar estado incoherente en esta pantalla.
    [Fact]
    public void Load_OpenAndCloseProjectTenTimes_DoesNotThrowOrAccumulateState()
    {
        var host = WpfAppHost.Current;
        var vm = new DocumentsViewModel(new FakeShell(), host);

        for (int cycle = 0; cycle < 10; cycle++)
        {
            host.Session.NewProject($"docs-cycle-{cycle}");
            host.Session.CurrentProjectPath = $"docs-cycle-{cycle}.qce";
            host.Session.ProjectInfo.AntecedentesTecnicos.Add(new AntecedenteTecnicoRow
            {
                Tipo = AntecedenteTecnicoTipo.Plano,
                Codigo = $"E-{cycle:00}",
                FechaDocumento = DateTime.Today,
                EsVigente = true
            });

            vm.Load();
            Assert.False(vm.ShowEmptyState);
            Assert.Single(vm.FilteredRows);
            Assert.False(vm.HasPendingChanges);
        }
    }
}
