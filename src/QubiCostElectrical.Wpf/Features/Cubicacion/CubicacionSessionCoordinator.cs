using System;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Reacción a Session.Changed/Saved — extraído de CubicacionViewModel.Session.cs (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md). Comportamiento preservado tal cual, incluyendo
/// el marshalling a UI thread (Session.Changed/Saved pueden dispararse desde un hilo de fondo,
/// ver comentario en RunOnUiThread) y la supresión de eco vía SuppressSessionReloadCount.
/// </summary>
internal sealed class CubicacionSessionCoordinator : IDisposable
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private EventHandler<ProjectSessionChangedEventArgs>? sessionChangedHandler;
    private EventHandler<ProjectSessionChangedEventArgs>? sessionSavedHandler;

    public CubicacionSessionCoordinator(CubicacionViewModel parent, BudgetProjectSession session)
    {
        this.parent = parent;
        this.session = session;
    }

    public void Initialize()
    {
        // Session.Saved/Changed pueden dispararse desde un hilo de fondo: ProjectWorkspaceService
        // guarda con ConfigureAwait(false), así que su continuación final (Session.MarkSaved(),
        // que dispara Saved sincrónicamente) puede correr en un hilo del thread pool en vez del
        // hilo de UI. Estos handlers terminan tocando ObservableCollection ligadas a la UI (Rows,
        // etc.), lo que lanza NotSupportedException si no se marshalla antes al Dispatcher. Se
        // ejecuta sincrónico cuando ya se está en el hilo correcto (o no hay Application real,
        // como en tests) para no romper ese comportamiento síncrono existente.
        sessionChangedHandler = (s, e) => RunOnUiThread(() => OnSessionChanged(s, e));
        sessionSavedHandler = (_, _) => RunOnUiThread(() =>
        {
            parent.HasPendingUndoForEdit = false;
            if (!parent.HasPendingChanges)
                parent.SaveStatusText = "Guardado";
        });
        session.Changed += sessionChangedHandler;
        session.Saved += sessionSavedHandler;
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
                break;
            case ProjectSessionChangeKind.Cubicacion:
            case ProjectSessionChangeKind.QtyHeaders:
            case ProjectSessionChangeKind.Zonas:
                RefreshCubicacionFromSession();
                break;
            case ProjectSessionChangeKind.Materiales:
                // Un precio del catálogo cambió (p. ej. se aplicó el Banco de Precios): las
                // líneas ya cubicadas con ese código deben reflejarlo sin que el usuario tenga
                // que hacer nada — ver CubicacionViewModel.RefreshPricesFromMaterialesCatalog.
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
        // Eco del propio cambio de esta instancia: Rows ya está al día, recargar solo
        // produciría el parpadeo de "Cargando partida…" en cada edición.
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
