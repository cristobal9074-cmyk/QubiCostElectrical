using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Epp;
using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Política de moneda única en vistas WPF: con MonedaPresupuesto = CLP ninguna cifra en UF
/// debe llegar a la pantalla (chip de oferta, gran total EPP y columnas de plantillas).
/// Con UF/USD se mantiene el par "moneda + equivalente en pesos".
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class MonedaUnicaVistasTests
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

    public MonedaUnicaVistasTests() => WpfTestHost.EnsureInitialized();

    // ---------------------------------------------------------------- Oferta y exportación

    [Fact]
    public void Export_ClpProject_OfferTotalNeverShowsUf()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-moneda-clp");
        host.Session.ProjectInfo.MonedaPresupuesto = "CLP";

        var vm = new ExportViewModel(host);
        vm.Refresh();

        Assert.DoesNotContain("UF", vm.TotalOfertaDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UF", vm.TotalOfertaPillText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UF", vm.TotalOfertaPillHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tasa UF", vm.TotalOfertaSecondaryTooltip, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Total CLP", vm.TotalOfertaPillHeader);
    }

    [Fact]
    public void Export_UfProject_OfferTotalShowsUfWithClpEquivalent()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-moneda-uf");
        host.Session.ProjectInfo.MonedaPresupuesto = "UF";

        var vm = new ExportViewModel(host);
        vm.Refresh();

        Assert.Contains("UF", vm.TotalOfertaDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CLP", vm.TotalOfertaDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Total UF", vm.TotalOfertaPillHeader);
    }

    // ---------------------------------------------------------------- EPP (gran total)

    [Fact]
    public void Epp_ClpProject_GranTotalNeverShowsUf()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-moneda-clp");
        host.Session.CurrentProjectPath = "epp-moneda-clp.qce";
        host.Session.ProjectInfo.MonedaPresupuesto = "CLP";
        host.Session.Data.Epp = EppBudgetData.CreateSeed(12, 20, 2);

        var vm = new EppViewModel(new FakeShell(), host);
        vm.MoDirectaSecciones[0].AddItem("Casco de prueba", 2m, 10000m);

        Assert.True(vm.TotalEpp > 0m, "el ítem agregado debe producir un gran total positivo para el test");
        Assert.DoesNotContain("UF", vm.GranTotalDualLabel, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("$", vm.GranTotalDualLabel);
    }

    [Fact]
    public void Epp_UfProject_GranTotalShowsUfEquivalent()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-moneda-uf");
        host.Session.CurrentProjectPath = "epp-moneda-uf.qce";
        host.Session.ProjectInfo.MonedaPresupuesto = "UF";
        host.Session.Data.Epp = EppBudgetData.CreateSeed(12, 20, 2);

        var vm = new EppViewModel(new FakeShell(), host);
        vm.MoDirectaSecciones[0].AddItem("Casco de prueba", 2m, 10000m);

        Assert.True(vm.TotalEpp > 0m, "el ítem agregado debe producir un gran total positivo para el test");
        Assert.Contains("UF", vm.GranTotalDualLabel, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Plantillas (preview de recursos)

    [Fact]
    public void Plantillas_ClpProject_HidesUfColumn()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("plantillas-moneda-clp");
        host.Session.ProjectInfo.MonedaPresupuesto = "CLP";

        var vm = new PlantillasVm(new FakeShell(), host);

        Assert.False(vm.MuestraUf);
    }

    [Fact]
    public void Plantillas_UfProject_ShowsUfColumn()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("plantillas-moneda-uf");
        host.Session.ProjectInfo.MonedaPresupuesto = "UF";

        var vm = new PlantillasVm(new FakeShell(), host);

        Assert.True(vm.MuestraUf);
    }

    [Fact]
    public void Plantillas_CurrencyChanged_ReappliesPolicyOnRefresh()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("plantillas-moneda-refresh");
        host.Session.ProjectInfo.MonedaPresupuesto = "CLP";

        var vm = new PlantillasVm(new FakeShell(), host);
        Assert.False(vm.MuestraUf);

        bool notified = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlantillasVm.MuestraUf))
                notified = true;
        };

        host.Session.ProjectInfo.MonedaPresupuesto = "UF";
        vm.Refresh();

        Assert.True(vm.MuestraUf);
        Assert.True(notified, "Refresh() debe notificar MuestraUf para que la vista reaplique la política");
    }
}
