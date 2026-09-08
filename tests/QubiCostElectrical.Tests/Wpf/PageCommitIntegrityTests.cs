using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Features.Epp;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// P0 integridad: páginas con draft/commit deben implementar las interfaces del shell
/// y al salir de pestaña (apply silencioso) debe llamarse TryApplyChanges antes de
/// persistir el .qce en background. El diálogo Guardar/Descartar ya no aplica al leave.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PageCommitIntegrityTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
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

    public PageCommitIntegrityTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void CubicacionScViewModel_ImplementsIPageWithCommitChanges()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("sc-commit-iface");
        host.Session.CurrentProjectPath = "sc-commit-iface.qce";
        var vm = new CubicacionScViewModel(new FakeShell(), host);

        Assert.IsAssignableFrom<IPageWithPendingChanges>(vm);
        Assert.IsAssignableFrom<IPageWithCommitChanges>(vm);
    }

    [Fact]
    public void CubicacionSc_MarkDirty_MarksSessionDirty_AndDiscardRestoresBaseline()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("sc-discard-baseline");
        host.Session.CurrentProjectPath = "sc-discard-baseline.qce";
        // Limpia suciedad del NewProject/EnsureDefault para aislar el escenario.
        host.Session.MarkSaved();

        var vm = new CubicacionScViewModel(new FakeShell(), host);
        Assert.False(vm.HasPendingChanges);
        int cuadrosAntes = host.Session.CubicacionSC.Count;

        vm.AddCuadroCommand.Execute(null);
        Assert.True(vm.HasPendingChanges);
        Assert.True(host.Session.IsDirty);
        Assert.Equal(cuadrosAntes + 1, host.Session.CubicacionSC.Count);

        Assert.True(((IPageWithPendingChanges)vm).TryDiscardChanges());
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(cuadrosAntes, host.Session.CubicacionSC.Count);
    }

    [Fact]
    public void CubicacionSc_TryApplyChanges_ClearsPagePending_AndKeepsSessionDirty()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("sc-apply");
        host.Session.CurrentProjectPath = "sc-apply.qce";
        host.Session.MarkSaved();

        var vm = new CubicacionScViewModel(new FakeShell(), host);
        vm.AddCircuitoCommand.Execute(null);
        Assert.True(vm.HasPendingChanges);

        Assert.True(((IPageWithCommitChanges)vm).TryApplyChanges());
        Assert.False(vm.HasPendingChanges);
        Assert.True(host.Session.IsDirty);
    }

    [Fact]
    public void EppViewModel_ImplementsIPageWithPendingAndCommitChanges()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-iface");
        host.Session.CurrentProjectPath = "epp-iface.qce";
        var vm = new EppViewModel(new FakeShell(), host);

        Assert.IsAssignableFrom<IPageWithPendingChanges>(vm);
        Assert.IsAssignableFrom<IPageWithCommitChanges>(vm);
    }

    [Fact]
    public void Epp_TryDiscardChanges_ReloadsDraft_AndTryApplyPersistsToSession()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-apply-discard");
        host.Session.CurrentProjectPath = "epp-apply-discard.qce";
        var vm = new EppViewModel(new FakeShell(), host);

        int mesesAntes = vm.MesesDirecta;
        vm.MesesDirecta = Math.Clamp(mesesAntes == 3 ? 4 : 3, 1, 24);
        Assert.True(vm.HasPendingChanges);

        Assert.True(((IPageWithPendingChanges)vm).TryDiscardChanges());
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(mesesAntes, vm.MesesDirecta);

        vm.MesesDirecta = Math.Clamp(mesesAntes == 3 ? 5 : 3, 1, 24);
        Assert.True(((IPageWithCommitChanges)vm).TryApplyChanges());
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(vm.MesesDirecta, host.Session.Data.Epp?.MesesDirecta);
        Assert.True(host.Session.IsDirty);
    }

    [Fact]
    public void TryApplyCurrentPageChanges_Contract_CallsCommitWhenPagePending()
    {
        // Mirrors ShellViewModel.TryApplyCurrentPageChanges (leave de pestaña → apply silencioso).
        var host = WpfAppHost.Current;
        host.Session.NewProject("leave-save-contract");
        host.Session.CurrentProjectPath = "leave-save-contract.qce";
        host.Session.MarkSaved();

        object page = new CubicacionScViewModel(new FakeShell(), host);
        var sc = (CubicacionScViewModel)page;
        sc.AddCircuitoCommand.Execute(null);
        Assert.True(sc.HasPendingChanges);

        bool applied = TryApplyCurrentPageChanges(page);
        Assert.True(applied);
        Assert.False(sc.HasPendingChanges);
        Assert.True(host.Session.IsDirty);
    }

    [Fact]
    public void ApuWorkspaceVm_OnRowEdited_MarksSessionDirty_AndImplementsCommit()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("apu-markdirty");
        host.Session.CurrentProjectPath = "apu-markdirty.qce";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1",
            Descripcion = "Partida APU",
            Unidad = "GL",
            Cantidad = 1m
        });
        host.Session.Apus.Add(new ApuRow
        {
            ItemId = "P1",
            Descripcion = "Partida APU",
            Unidad = "GL",
            Recursos =
            [
                new ApuRecursoRow
                {
                    Tipo = ApuTipoRecurso.MAT,
                    Descripcion = "Cable",
                    Rendimiento = 1m,
                    PrecioUf = 1m
                }
            ]
        });
        host.Session.MarkSaved();

        var vm = new ApuWorkspaceVm(new FakeShell(), host);
        Assert.IsAssignableFrom<IPageWithPendingChanges>(vm);
        Assert.IsAssignableFrom<IPageWithCommitChanges>(vm);
        Assert.False(host.Session.IsDirty);

        vm.OnRowEdited();
        Assert.True(((IPageWithPendingChanges)vm).HasPendingChanges);
        Assert.True(host.Session.IsDirty);

        Assert.True(((IPageWithCommitChanges)vm).TryApplyChanges());
        Assert.False(((IPageWithPendingChanges)vm).HasPendingChanges);
        Assert.True(host.Session.IsDirty);
    }

    [Fact]
    public void ApuWorkspaceVm_TryDiscardChanges_RestoresBaseline()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("apu-discard");
        host.Session.CurrentProjectPath = "apu-discard.qce";
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
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Original", Rendimiento = 1m, PrecioUf = 2m }
            ]
        });
        host.Session.MarkSaved();

        var vm = new ApuWorkspaceVm(new FakeShell(), host);
        var apu = host.Session.Apus[0];
        Assert.Single(apu.Recursos);
        apu.Recursos.Add(new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Extra", Rendimiento = 1m, PrecioUf = 1m });
        vm.OnRowEdited();
        Assert.Equal(2, host.Session.Apus[0].Recursos.Count);

        Assert.True(((IPageWithPendingChanges)vm).TryDiscardChanges());
        Assert.False(((IPageWithPendingChanges)vm).HasPendingChanges);
        Assert.Single(host.Session.Apus[0].Recursos);
        Assert.Equal("Original", host.Session.Apus[0].Recursos[0].Descripcion);
    }

    [Fact]
    public void ApuRecursoVm_Setters_Blocked_WhenReadOnly()
    {
        var row = new ApuRecursoRow { Descripcion = "Antes", Rendimiento = 1m, PrecioUf = 1m };
        var vm = new ApuRecursoVm(row, () => false);

        vm.Descripcion = "Después";
        vm.Rendimiento = 9m;
        vm.PrecioUf = 9m;

        Assert.Equal("Antes", row.Descripcion);
        Assert.Equal(1m, row.Rendimiento);
        Assert.Equal(1m, row.PrecioUf);
    }

    [Fact]
    public void PriceBankEntryVm_Setters_Blocked_WhenReadOnly()
    {
        var entry = new PriceBankEntry
        {
            Codigo = "C1",
            Descripcion = "Antes",
            MatClp = 1000m
        };
        var vm = new PriceBankEntryVm(entry, 39000m, () => false);

        vm.Descripcion = "Después";
        vm.PrecioClp = 9999m;
        vm.Codigo = "XX";

        Assert.Equal("Antes", entry.Descripcion);
        Assert.Equal(1000m, entry.MatClp);
        Assert.Equal("C1", entry.Codigo);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void CubicacionViewModel_ImplementsIPageWithCommitChanges()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cub-commit-iface");
        host.Session.CurrentProjectPath = "cub-commit-iface.qce";
        var vm = new CubicacionPageVm(new FakeShell(), host);

        Assert.IsAssignableFrom<IPageWithPendingChanges>(vm);
        Assert.IsAssignableFrom<IPageWithCommitChanges>(vm);
    }

    /// <summary>Misma condición que ShellViewModel.TryApplyCurrentPageChanges.</summary>
    private static bool TryApplyCurrentPageChanges(object? currentPageVm)
    {
        if (currentPageVm is IPageWithPendingChanges pending &&
            pending.HasPendingChanges &&
            currentPageVm is IPageWithCommitChanges commitPage)
            return commitPage.TryApplyChanges();

        return true;
    }
}