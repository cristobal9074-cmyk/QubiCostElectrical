using System;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Base para ViewModels de pantalla que reaccionan a host.Session.Changed. Ese singleton vive
/// toda la vida de la app (WpfAppHost.Session): sin desuscribirse, cada ciclo de cerrar/reabrir
/// un proyecto que recrea la pantalla (ShellWindow.ClearPageCache) deja la instancia vieja
/// retenida para siempre. Antes de esta clase, 7 pantallas repetían a mano el mismo par
/// suscribir/desuscribir y una (VersionsViewModel) tenía un Dispose() que nunca se llamaba
/// porque la clase no declaraba IDisposable — ver
/// tests/QubiCostElectrical.Tests/Wpf/SessionChangedSubscriptionLeakTests.cs.
/// </summary>
public abstract class SessionAwareViewModelBase : ViewModelBase, IDisposable
{
    private readonly BudgetProjectSession session;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedHandler;
    private bool disposed;

    protected SessionAwareViewModelBase(WpfAppHost host)
    {
        session = host.Session;
        sessionChangedHandler = OnSessionChanged;
        session.Changed += sessionChangedHandler;
    }

    protected abstract void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session.Changed -= sessionChangedHandler;
        DisposeCore();
    }

    /// <summary>Punto de extensión para limpieza adicional (timers, wiring) en clases derivadas.</summary>
    protected virtual void DisposeCore()
    {
    }
}
