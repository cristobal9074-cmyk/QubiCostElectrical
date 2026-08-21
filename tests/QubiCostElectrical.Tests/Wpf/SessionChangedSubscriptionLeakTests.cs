using System.Reflection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Features.Purchases;
using QubiCostElectrical.Wpf.Features.Quotes;
using QubiCostElectrical.Wpf.Features.Review;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Regresión: host.Session vive toda la vida de la app (singleton de WpfAppHost). 7 pantallas
/// se suscribían a session.Changed en su constructor sin desuscribirse nunca — sin implementar
/// IDisposable, o (en VersionsViewModel) con un Dispose() que existía pero estaba vacío. Cada
/// vez que el usuario cierra y reabre un proyecto, el shell recrea estas pantallas
/// (ClearPageCache); sin desuscripción, cada instancia vieja queda retenida para siempre
/// porque session.Changed sigue apuntándole. Esto crece sin límite con el uso normal de la app
/// (justo el patrón "se cierra inesperadamente tras un rato de uso" reportado).
///
/// Se verifica contando los suscriptores reales del evento vía reflexión sobre el campo
/// generado por el compilador para "Changed" — no depende de bombear el Dispatcher de WPF
/// (el propio handler usa Dispatcher.InvokeAsync, así que no se puede observar de forma
/// confiable el efecto async en un test síncrono).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class SessionChangedSubscriptionLeakTests
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

    public SessionChangedSubscriptionLeakTests() => WpfTestHost.EnsureInitialized();

    private static int CountSessionChangedSubscribers(BudgetProjectSession session)
    {
        var field = typeof(BudgetProjectSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = field?.GetValue(session) as MulticastDelegate;
        return del?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public void ReviewViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new ReviewViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void PriceBankViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new PriceBankViewModel(host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void VersionsViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        // Antes de este fix, la clase ni siquiera declaraba IDisposable (tenía un Dispose()
        // vacío sin efecto real): ClearPageCache() nunca lo llamaba y la suscripción persistía.
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new VersionsViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void QuotesViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new QuotesViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void PurchaseListViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new PurchaseListViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void CubicacionScViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new CubicacionScViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void ItemizadoViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        // ItemizadoViewModel también se suscribe a Session.Saved (además de Changed) y hace su
        // propia limpieza de filas wireadas — quedó fuera de la migración a
        // SessionAwareViewModelBase por eso, pero el Changed sigue pasando por la base vía
        // DisposeCore(); esto confirma que ese camino también queda limpio al Dispose().
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void ExportViewModel_Dispose_UnsubscribesFromSessionChanged()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new ExportViewModel(host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    // Diez ciclos de "abrir pantalla / cerrarla" (lo mismo que hace ClearPageCache al
    // cerrar y reabrir un proyecto) no deben dejar suscriptores acumulados.
    [Fact]
    public void TenOpenCloseCycles_DoNotAccumulateSubscribers()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        for (int i = 0; i < 10; i++)
        {
            var vm = new PriceBankViewModel(host);
            vm.Dispose();
        }

        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    private static int CountSessionSavedSubscribers(BudgetProjectSession session)
    {
        var field = typeof(BudgetProjectSession).GetField("Saved", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = field?.GetValue(session) as MulticastDelegate;
        return del?.GetInvocationList().Length ?? 0;
    }

    // ShellViewModel se suscribe DOS VECES a session.Changed (RefreshChrome y
    // SyncReadOnlyFromSession, en constructores separados) además de session.Saved,
    // autoSave.PropertyChanged y host.UndoService.PropertyChanged — quedó fuera de la
    // cobertura anterior porque no reutiliza SessionAwareViewModelBase (necesita 2
    // suscripciones a Changed, no 1). Antes de este fix, la clase ni declaraba IDisposable:
    // cada "Cerrar sesión" (que antes además terminaba la app entera por el bug de
    // ShellWindow.Closing, ver ShellLogoutTests) creaba una instancia nueva sin liberar la
    // vieja ni su AutoSaveService (con Timer propio).
    [Fact]
    public void ShellViewModel_Dispose_UnsubscribesFromSessionChangedTwiceAndSaved()
    {
        var host = WpfAppHost.Current;
        int changedBefore = CountSessionChangedSubscribers(host.Session);
        int savedBefore = CountSessionSavedSubscribers(host.Session);

        var vm = new ShellViewModel(host);
        Assert.Equal(changedBefore + 2, CountSessionChangedSubscribers(host.Session));
        Assert.Equal(savedBefore + 1, CountSessionSavedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(changedBefore, CountSessionChangedSubscribers(host.Session));
        Assert.Equal(savedBefore, CountSessionSavedSubscribers(host.Session));
    }

    [Fact]
    public void ShellViewModel_Dispose_IsIdempotent()
    {
        var host = WpfAppHost.Current;
        var vm = new ShellViewModel(host);

        var ex = Record.Exception(() =>
        {
            vm.Dispose();
            vm.Dispose();
        });

        Assert.Null(ex);
    }
}
