using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ShellChromeAlertsTests : IDisposable
{
    private readonly List<ShellViewModel> shells = new();

    public ShellChromeAlertsTests()
    {
        WpfTestHost.EnsureInitialized();
        ResetSharedHostState();
    }

    public void Dispose()
    {
        foreach (var shell in shells)
            shell.Dispose();
        shells.Clear();
        ResetSharedHostState();
    }

    /// <summary>
    /// WpfHostCollection comparte un único DashboardPresenter: limpiar cache y path
    /// para que un Fact no deje LastAlertTotalIssues / proyecto abierto al siguiente.
    /// </summary>
    private static void ResetSharedHostState()
    {
        var host = WpfAppHost.Current;
        host.Dashboard.ClearAlertCache();
        host.Session.CurrentProjectPath = "";
    }

    private ShellViewModel CreateShell(WpfAppHost host)
    {
        var shell = new ShellViewModel(host);
        shells.Add(shell);
        return shell;
    }

    [Fact]
    public void RefreshChrome_WithoutProject_HasNoAlerts()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Dashboard.ClearAlertCache();
        host.Session.CurrentProjectPath = "";

        var shell = CreateShell(host);
        shell.RefreshChrome();

        Assert.Equal("0", shell.AlertCount);
        Assert.False(shell.HasAlerts);
    }

    [Fact]
    public void RefreshChrome_UsesCachedDashboardAlerts_WithoutCreateSnapshot()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Dashboard.ClearAlertCache();
        host.Session.NewProject("chrome-alerts");
        host.Session.CurrentProjectPath = "chrome-alerts.qce";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "Título", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida", Unidad = "GL" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Cantidad = 10m, MatUf = 1m, Unidad = "UN" });

        // El chrome ya no recalcula alertas en cada Session.Changed; usa el cache del dashboard.
        host.Dashboard.ClearAlertCache();
        _ = host.Dashboard.BuildAlerts(host.Session.Data);

        var shell = CreateShell(host);
        shell.RefreshChrome();

        Assert.True(int.Parse(shell.AlertCount) > 0);
        Assert.True(shell.HasAlerts);
    }

    [Fact]
    public void RefreshChrome_WithoutCachedAlerts_ShowsPlaceholder()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Dashboard.ClearAlertCache();
        host.Session.NewProject("chrome-alerts-empty-cache");
        host.Session.CurrentProjectPath = "chrome-alerts-empty-cache.qce";
        host.Dashboard.ClearAlertCache();

        var shell = CreateShell(host);
        // NewProject / ctor pueden haber tocado el host compartido; limpiar de nuevo
        // justo antes del RefreshChrome bajo prueba.
        host.Dashboard.ClearAlertCache();
        shell.RefreshChrome();

        Assert.Equal("—", shell.AlertCount);
        Assert.False(shell.HasAlerts);
    }

    [Fact]
    public void QuickSearch_WhenReadOnly_OmitsMutationActions()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Dashboard.ClearAlertCache();
        var previousRole = host.Permissions.CurrentRole;
        host.Session.NewProject("chrome-readonly-search");
        host.Session.CurrentProjectPath = "chrome-readonly-search.qce";
        host.Permissions.SetRole(UserRole.Viewer);

        try
        {
            // El ctor aplica RoleSessionSync → Session.IsReadOnly = true para Visor.
            var shell = CreateShell(host);
            shell.PostCloseNavigate = () => { };
            shell.SearchText = "";

            Assert.True(host.Session.IsReadOnly);
            Assert.True(shell.IsReadOnly);
            Assert.False(shell.CanSaveProject);
            Assert.DoesNotContain(shell.QuickSearchResults, r => r.Title.Equals("Nuevo proyecto", StringComparison.Ordinal));
            Assert.DoesNotContain(shell.QuickSearchResults, r => r.Title.Equals("Guardar proyecto", StringComparison.Ordinal));
            Assert.DoesNotContain(shell.QuickSearchResults, r => r.Title.Equals("Guardar como", StringComparison.Ordinal));
            Assert.DoesNotContain(shell.QuickSearchResults, r => r.Title.Equals("Deshacer", StringComparison.Ordinal));
            Assert.DoesNotContain(shell.QuickSearchResults, r => r.Title.Equals("Rehacer", StringComparison.Ordinal));
            Assert.Contains(shell.QuickSearchResults, r => r.Title.Equals("Abrir proyecto", StringComparison.Ordinal));
            Assert.Contains(shell.QuickSearchResults, r => r.Title.Equals("Preferencias", StringComparison.Ordinal));
        }
        finally
        {
            host.Permissions.SetRole(previousRole);
            host.Session.SetRoleReadOnly(false);
            host.Dashboard.ClearAlertCache();
            host.Session.CurrentProjectPath = "";
        }
    }
}
