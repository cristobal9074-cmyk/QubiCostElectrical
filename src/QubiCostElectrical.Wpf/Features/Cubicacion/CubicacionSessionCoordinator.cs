using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Reacción a Session.Changed/Saved. Ola 2: acumula kinds en cache inactiva y flushea al reactivar.
/// </summary>
internal sealed class CubicacionSessionCoordinator : IDisposable, ISessionChangeAware
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private EventHandler<ProjectSessionChangedEventArgs>? sessionChangedHandler;
    private EventHandler<ProjectSessionChangedEventArgs>? sessionSavedHandler;
    private readonly Dictionary<ProjectSessionChangeKind, ProjectSessionChangedEventArgs> pendingByKind = new();
    private bool pendingSaved;
    private bool sessionChangeActive = true;

    public CubicacionSessionCoordinator(CubicacionViewModel parent, BudgetProjectSession session)
    {
        this.parent = parent;
        this.session = session;
    }

    public bool HasPendingSessionChanges => pendingByKind.Count > 0 || pendingSaved;

    public void Initialize()
    {
        sessionChangedHandler = (s, e) => RunOnUiThread(() => OnSessionChangedRaw(s, e));
        sessionSavedHandler = (_, _) => RunOnUiThread(OnSessionSavedRaw);
        session.Changed += sessionChangedHandler;
        session.Saved += sessionSavedHandler;
    }

    public void SetSessionChangeActive(bool active)
    {
        sessionChangeActive = active;
        if (active)
            FlushPending();
    }

    private void OnSessionChangedRaw(object? sender, ProjectSessionChangedEventArgs e)
    {
        pendingByKind[e.Kind] = e;
        if (!sessionChangeActive)
            return;
        FlushPending();
    }

    private void OnSessionSavedRaw()
    {
        pendingSaved = true;
        if (!sessionChangeActive)
            return;
        FlushPending();
    }

    private void FlushPending()
    {
        if (pendingByKind.Count > 0)
        {
            var batch = pendingByKind.Values.ToArray();
            pendingByKind.Clear();
            foreach (var args in batch)
                OnSessionChanged(session, args);
        }

        if (pendingSaved)
        {
            pendingSaved = false;
            parent.HasPendingUndoForEdit = false;
            if (!parent.HasPendingChanges)
                parent.SaveStatusText = "Guardado";
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }

    public void Dispose()
    {
        if (sessionChangedHandler != null)
            session.Changed -= sessionChangedHandler;
        if (sessionSavedHandler != null)
            session.Saved -= sessionSavedHandler;
    }

    private void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        parent.NotifyIsReadOnlyChanged();
        if (e.Scope is "role_access" or "lock_access")
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

        switch (e.Kind)
        {
            case ProjectSessionChangeKind.Itemizado:
            case ProjectSessionChangeKind.ProjectInfo:
                ReloadPartidasFromSession();
                parent.RefreshValorUfEnFilasCargadas();
                parent.NotifyMonedaChanged();
                break;
            case ProjectSessionChangeKind.Cubicacion:
            case ProjectSessionChangeKind.QtyHeaders:
            case ProjectSessionChangeKind.Zonas:
                RefreshCubicacionFromSession();
                break;
            case ProjectSessionChangeKind.Materiales:
                parent.RefreshPricesFromMaterialesCatalog();
                break;
        }
    }

    private void ReloadPartidasFromSession()
    {
        string? prevId = parent.SelectedPartida?.Id;
        parent.LoadPartidas();

        if (string.IsNullOrWhiteSpace(prevId))
        {
            parent.SelectFirstPartida();
            return;
        }

        var still = parent.Partidas.FirstOrDefault(p => string.Equals(p.Id, prevId, StringComparison.OrdinalIgnoreCase));
        if (still != null && still.EsPartida)
            parent.SelectPartida(still);
        else
            parent.SelectFirstPartida();
    }

    private void RefreshCubicacionFromSession()
    {
        if (parent.SuppressSessionReloadCount > 0)
            return;

        if (parent.SelectedPartida == null)
        {
            parent.LoadPartidas();
            return;
        }

        string prevId = parent.SelectedPartida.Id;
        parent.LoadPartidas();
        var still = parent.Partidas.FirstOrDefault(p => string.Equals(p.Id, prevId, StringComparison.OrdinalIgnoreCase));
        if (still != null && still.EsPartida)
            parent.SelectPartida(still);
        else
            parent.SelectFirstPartida();
    }
}
