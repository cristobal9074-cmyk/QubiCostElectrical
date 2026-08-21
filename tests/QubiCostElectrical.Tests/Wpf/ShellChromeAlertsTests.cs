using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ShellChromeAlertsTests
{
    [Fact]
    public void RefreshChrome_WithoutProject_HasNoAlerts()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.CurrentProjectPath = "";

        var shell = new ShellViewModel(host);
        shell.RefreshChrome();

        Assert.Equal("0", shell.AlertCount);
        Assert.False(shell.HasAlerts);
    }

    [Fact]
    public void RefreshChrome_WithReviewIssues_ShowsAlertBadge()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("chrome-alerts");
        host.Session.CurrentProjectPath = "chrome-alerts.qce";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "Título", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida", Unidad = "GL" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Cantidad = 10m, MatUf = 1m, Unidad = "UN" });

        var shell = new ShellViewModel(host);
        shell.RefreshChrome();

        Assert.True(int.Parse(shell.AlertCount) > 0);
        Assert.True(shell.HasAlerts);
    }
}
