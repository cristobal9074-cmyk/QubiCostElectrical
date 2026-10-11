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
    public void AddFiles_WithDisciplina_PrefixesCodigoAndSetsDisciplina()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("docs-disciplina");
        host.Session.CurrentProjectPath = "docs-disciplina.qce";
        var vm = new DocumentsViewModel(new FakeShell(), host);
        vm.Load();

        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\PE-01.pdf" }, AntecedenteDisciplina.Electricidad);

        Assert.Single(vm.FilteredRows);
        Assert.Equal("ELE-PE-01", vm.FilteredRows[0].Codigo);
        Assert.Equal(AntecedenteDisciplina.Electricidad, vm.FilteredRows[0].Disciplina);
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

    [Fact]
    public void EspecialidadesPlanos_GeneratesDynamicFiltersByLoadedBlueprintDisciplines()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("docs-disciplinas-dinamicas");
        host.Session.CurrentProjectPath = "docs-disciplinas-dinamicas.qce";
        var vm = new DocumentsViewModel(new FakeShell(), host);
        vm.Load();

        // 1. Agregar planos de Electricidad
        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\E-01.pdf", "C:\\Planos\\E-02.pdf" }, AntecedenteDisciplina.Electricidad);
        // 2. Agregar planos de Corrientes débiles
        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\CD-01.pdf", "C:\\Planos\\CD-02.pdf", "C:\\Planos\\CD-03.pdf" }, AntecedenteDisciplina.CorrientesDebiles);
        // 3. Agregar una especificación técnica
        vm.AddFiles(AntecedenteTecnicoTipo.Especificacion, new[] { "C:\\EETT\\ET-01.pdf" });

        // Debe haber 2 especialidades en EspecialidadesPlanos: Electricidad (2) y Corrientes débiles (3)
        Assert.Equal(2, vm.EspecialidadesPlanos.Count);
        var espElec = vm.EspecialidadesPlanos.FirstOrDefault(x => x.Disciplina == AntecedenteDisciplina.Electricidad);
        var espCcdd = vm.EspecialidadesPlanos.FirstOrDefault(x => x.Disciplina == AntecedenteDisciplina.CorrientesDebiles);

        Assert.NotNull(espElec);
        Assert.Equal(2, espElec.Count);
        Assert.Equal("⚡", espElec.Icon);

        Assert.NotNull(espCcdd);
        Assert.Equal(3, espCcdd.Count);
        Assert.Equal("📡", espCcdd.Icon);

        // Al seleccionar la especialidad de Corrientes débiles:
        vm.SelectNavFilter($"Plano_Esp:{AntecedenteDisciplina.CorrientesDebiles}");
        Assert.Equal(3, vm.FilteredRows.Count);
        Assert.All(vm.FilteredRows, r => Assert.Equal(AntecedenteDisciplina.CorrientesDebiles, r.Disciplina));
        Assert.True(espCcdd.IsSelected);
        Assert.False(espElec.IsSelected);

        // Al seleccionar la especialidad de Electricidad:
        vm.SelectNavFilter($"Plano_Esp:{AntecedenteDisciplina.Electricidad}");
        Assert.Equal(2, vm.FilteredRows.Count);
        Assert.All(vm.FilteredRows, r => Assert.Equal(AntecedenteDisciplina.Electricidad, r.Disciplina));
        Assert.True(espElec.IsSelected);
        Assert.False(espCcdd.IsSelected);

        // Al seleccionar "Todos los Planos":
        vm.SelectNavFilter("Planos_Todos");
        Assert.Equal(5, vm.FilteredRows.Count);
        Assert.All(vm.FilteredRows, r => Assert.True(r.IsTipoPlano));
        Assert.True(vm.IsNavPlanosTodos);

        // Al seleccionar "Todo el Expediente":
        vm.SelectNavFilter("Todos");
        Assert.Equal(6, vm.FilteredRows.Count);
        Assert.True(vm.IsNavTodos);
    }

    [Fact]
    public void ToggleInspectionFilter_FiltersByVigenteAndAttachedFile()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("docs-inspection-filters");
        host.Session.CurrentProjectPath = "docs-inspection-filters.qce";
        var vm = new DocumentsViewModel(new FakeShell(), host);
        vm.Load();

        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\E-01.pdf" });
        vm.AddFiles(AntecedenteTecnicoTipo.Plano, new[] { "C:\\Planos\\E-02.pdf" });

        // Marcar uno como no vigente
        vm.FilteredRows[0].EsVigente = false;

        // Toggle SoloVigentes
        vm.ToggleInspectionFilter("SoloVigentes");
        Assert.True(vm.IsFilterSoloVigentes);
        Assert.Single(vm.FilteredRows);
        Assert.True(vm.FilteredRows[0].EsVigente);

        // Desactivar toggle
        vm.ToggleInspectionFilter("SoloVigentes");
        Assert.False(vm.IsFilterSoloVigentes);
        Assert.Equal(2, vm.FilteredRows.Count);
    }
}
