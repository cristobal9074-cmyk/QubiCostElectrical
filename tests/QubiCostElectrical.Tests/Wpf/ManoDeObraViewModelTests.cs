using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.ManoDeObra;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Contrato editable de la grilla de mano de obra: dotación fija de los roles indirectos
/// (columna Dot.) y fracción de obra (prorrateo del sueldo) con persistencia en la sesión.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ManoDeObraViewModelTests
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

    private sealed class FakeWorkspace : IProjectWorkspaceService
    {
        public FakeWorkspace(BudgetProjectSession session, bool hasOpenProject = true)
        {
            Session = session;
            HasOpenProject = hasOpenProject;
        }

        public BudgetProjectSession Session { get; }
        public bool HasOpenProject { get; }
        public Task OpenAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public BudgetValidationResult ValidateCurrent() => new();
        public void DiscardChanges() { }
    }

    private static (ManoDeObraViewModel Vm, BudgetProjectSession Session) CreateVm()
    {
        var session = new BudgetProjectSession();
        session.NewProject("tester", "Tester");
        var vm = new ManoDeObraViewModel(new FakeShellHost(), session, new FakeWorkspace(session));
        vm.Load();
        return (vm, session);
    }

    [Fact]
    public void Load_SinProyecto_MuestraEstadoVacioYBloqueaEdicion()
    {
        var session = new BudgetProjectSession();
        var vm = new ManoDeObraViewModel(new FakeShellHost(), session, new FakeWorkspace(session, hasOpenProject: false));

        vm.Load();

        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.CanEdit);
        Assert.Empty(vm.CargosDef);
        Assert.Empty(session.Data.MoIndirecta);
    }

    [Fact]
    public void SetIndirectDotPico_FijaLaDotacionDeTodosLosMeses_YLimpiaLosOverridesDelCargo()
    {
        var (vm, session) = CreateVm();

        vm.SetOverride("AD", 0, 5);
        vm.SetOverride("AD", 6, 2);
        Assert.True(vm.HasOverride("AD", 0));

        vm.SetIndirectDotPico("AD", 3);

        Assert.False(vm.HasOverride("AD", 0));
        Assert.False(vm.HasOverride("AD", 6));
        Assert.Equal(3, vm.CargosDef.Single(c => c.Id == "AD").DotPico);

        var row = vm.DotacionRows.Single(r => r.Tag == "AD");
        Assert.Equal(vm.TotalMonths, row.Dotaciones.Length);
        Assert.All(row.Dotaciones, d => Assert.Equal(3, d));

        var persisted = session.Data.MoIndirecta.Single(r => r.Id == "AD");
        Assert.Equal(3, persisted.Dotacion);
        Assert.Empty(persisted.DotacionMensual);
    }

    [Fact]
    public void StageIndirectDotPico_SeAplicaAlHacerFlush_YEscapeLoCancela()
    {
        var (vm, _) = CreateVm();

        vm.StageIndirectDotPico("AD", 4);
        Assert.Equal(0, vm.CargosDef.Single(c => c.Id == "AD").DotPico);

        vm.FlushStaffingEdits();
        Assert.Equal(4, vm.CargosDef.Single(c => c.Id == "AD").DotPico);

        vm.StageIndirectDotPico("SV", 7);
        Assert.True(vm.StageClearDotPico("SV"));
        vm.FlushStaffingEdits();
        Assert.Equal(0, vm.CargosDef.Single(c => c.Id == "SV").DotPico);
    }

    [Fact]
    public void FraccionObra_EditableEnLaTarjeta_PersisteYProrrateaElCostoIndirecto()
    {
        var (vm, session) = CreateVm();
        var ed = vm.IndirectaEditors.Single(e => e.Id == "AD");

        Assert.Equal(1m, ed.FraccionObra);
        Assert.Equal("1,00", ed.FraccionEditText);

        vm.SetIndirectDotPico("AD", 1);
        ed.FraccionEditText = "0,3";
        ed.CommitEdit();

        Assert.Equal(0.3m, ed.FraccionObra);
        Assert.Equal("0,30", ed.FraccionEditText);
        Assert.Equal(0.3m, session.Data.MoIndirecta.Single(r => r.Id == "AD").FraccionObra);
        // 1.237.500 × 0,3 × 1,35 (leyes 35%)
        Assert.Equal("$ 501.188", ed.ConLeyesLabel);
        // 1 dotación × 1.237.500 × 0,3 × 12 meses
        Assert.Equal(4455000m, ExcelCostosBridge.SumMoIndirecta(session.Data.MoIndirecta, 12));
    }

    [Fact]
    public void FraccionObraCero_NoCobraNada_YDejaLaDirectaIntacta()
    {
        var (vm, session) = CreateVm();
        vm.SetIndirectDotPico("AD", 2);

        vm.IndirectaEditors.Single(e => e.Id == "AD").FraccionEditText = "0";

        Assert.Equal(0m, session.Data.MoIndirecta.Single(r => r.Id == "AD").FraccionObra);
        Assert.Equal(0m, ExcelCostosBridge.SumMoIndirecta(session.Data.MoIndirecta, 12));
        Assert.All(vm.CargosDef.Where(c => c.EsDirecta), c => Assert.Equal(1m, c.FraccionObra));
        Assert.DoesNotContain(vm.IndirectaEditors, e => e.Id == "M1");
    }
}
