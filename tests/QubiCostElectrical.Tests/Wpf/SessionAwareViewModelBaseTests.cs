using System.Reflection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Cubre la clase base en sí (no una pantalla concreta): cualquier ViewModel futuro que herede
/// de SessionAwareViewModelBase queda automáticamente suscrito/desuscrito de
/// host.Session.Changed sin repetir el patrón a mano — el mismo patrón que, repetido a mano en
/// 7 pantallas, causó la fuga documentada en SessionChangedSubscriptionLeakTests. Un VM nuevo
/// que olvide desuscribirse ya no es posible salvo que ignore esta base a propósito.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class SessionAwareViewModelBaseTests
{
    private sealed class FakeSessionAwareViewModel : SessionAwareViewModelBase
    {
        public int OnSessionChangedCallCount { get; private set; }
        public int DisposeCoreCallCount { get; private set; }

        public FakeSessionAwareViewModel(WpfAppHost host) : base(host)
        {
        }

        protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
            OnSessionChangedCallCount++;

        protected override void DisposeCore() => DisposeCoreCallCount++;
    }

    public SessionAwareViewModelBaseTests() => WpfTestHost.EnsureInitialized();

    private static int CountSessionChangedSubscribers(BudgetProjectSession session)
    {
        var field = typeof(BudgetProjectSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = field?.GetValue(session) as MulticastDelegate;
        return del?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public void Constructor_SubscribesToSessionChanged_AndDispose_Unsubscribes()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new FakeSessionAwareViewModel(host);
        Assert.Equal(before + 1, CountSessionChangedSubscribers(host.Session));

        vm.Dispose();
        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
    }

    [Fact]
    public void Dispose_IsIdempotent_DoesNotDoubleUnsubscribeOrDoubleRunDisposeCore()
    {
        var host = WpfAppHost.Current;
        int before = CountSessionChangedSubscribers(host.Session);

        var vm = new FakeSessionAwareViewModel(host);
        vm.Dispose();
        vm.Dispose();

        Assert.Equal(before, CountSessionChangedSubscribers(host.Session));
        Assert.Equal(1, vm.DisposeCoreCallCount);
    }

    [Fact]
    public void SessionChanged_InvokesDerivedOverride()
    {
        // No hay una API pública para disparar Changed en pruebas: se invoca el delegado
        // real vía reflexión, igual que CountSessionChangedSubscribers lo lee.
        var host = WpfAppHost.Current;
        var vm = new FakeSessionAwareViewModel(host);
        try
        {
            var field = typeof(BudgetProjectSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance);
            var del = (MulticastDelegate?)field?.GetValue(host.Session);
            var args = new ProjectSessionChangedEventArgs(ProjectSessionChangeKind.Reset, "test", "test", false);
            foreach (var handler in del?.GetInvocationList() ?? [])
                handler.Method.Invoke(handler.Target, [host.Session, args]);

            Assert.Equal(1, vm.OnSessionChangedCallCount);
        }
        finally
        {
            vm.Dispose();
        }
    }
}
