using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Base para ViewModels de pantalla que reaccionan a host.Session.Changed.
/// Ola 2: coalesce 75 ms, batch de kinds, silencia cache inactiva.
/// </summary>
public abstract class SessionAwareViewModelBase : ViewModelBase, IDisposable, ISessionChangeAware
{
    private static readonly TimeSpan CoalesceDelay = TimeSpan.FromMilliseconds(75);

    private readonly BudgetProjectSession session;
    private readonly EventHandler<ProjectSessionChangedEventArgs> sessionChangedHandler;
    private readonly SessionChangeCoalescer? coalescer;
    private readonly Dictionary<ProjectSessionChangeKind, ProjectSessionChangedEventArgs> pendingByKind = new();
    private bool sessionChangeActive = true;
    private bool disposed;

    protected SessionAwareViewModelBase(WpfAppHost host) : this(host.Session) { }

    protected SessionAwareViewModelBase(BudgetProjectSession session)
    {
        this.session = session;
        sessionChangedHandler = OnSessionChangedRaw;
        session.Changed += sessionChangedHandler;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
            coalescer = new SessionChangeCoalescer(dispatcher, CoalesceDelay, FlushPendingSessionChange);
    }

    public void SetSessionChangeActive(bool active)
    {
        sessionChangeActive = active;
        if (active && pendingByKind.Count > 0)
            FlushPendingSessionChange();
    }

    /// <summary>True si había cambios pendientes (el flush los aplicará vía coalescer o inmediato).</summary>
    public bool HasPendingSessionChanges => pendingByKind.Count > 0;

    private void OnSessionChangedRaw(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (disposed) return;
        pendingByKind[e.Kind] = e;
        if (!sessionChangeActive)
            return;

        if (coalescer == null)
        {
            FlushPendingSessionChange();
            return;
        }

        coalescer.Signal();
    }

    private void FlushPendingSessionChange()
    {
        if (disposed || !sessionChangeActive) return;
        if (pendingByKind.Count == 0) return;

        var batch = pendingByKind.Values.ToArray();
        pendingByKind.Clear();
        foreach (var args in batch)
            OnSessionChanged(session, args);
    }

    protected abstract void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session.Changed -= sessionChangedHandler;
        coalescer?.Dispose();
        DisposeCore();
    }

    protected virtual void DisposeCore()
    {
    }
}
