using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Coordinador de navegación, cola de cubicación secuencial y búsqueda in-grid en Cubicación.
/// </summary>
internal sealed class CubicacionNavigationCoordinator
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly IShellNavigationHost shell;

    private string resourceSearchText = "";

    public CubicacionNavigationCoordinator(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        IShellNavigationHost shell)
    {
        this.parent = parent;
        this.session = session;
        this.shell = shell;
    }

    public string ResourceSearchText
    {
        get => resourceSearchText;
        set
        {
            if (resourceSearchText != value)
            {
                resourceSearchText = value ?? "";
                parent.NotifyProperty(nameof(ResourceSearchText));
            }
        }
    }

    public void PromptTakeoffQueueContinuation()
    {
        var remaining = session.PendingTakeoffQueueCount;
        if (remaining <= 0)
            return;

        ShellNotificationService.Instance.ShowInfo(
            "Cola de cubicación",
            remaining == 1
                ? "Queda 1 partida pendiente."
                : $"Quedan {remaining:N0} partidas pendientes.",
            "Siguiente",
            AdvanceTakeoffQueue);
    }

    private void AdvanceTakeoffQueue()
    {
        var nextId = session.TryDequeueNextTakeoff();
        if (string.IsNullOrWhiteSpace(nextId))
            return;

        var target = parent.Partidas.FirstOrDefault(p =>
            string.Equals(p.Id, nextId, StringComparison.OrdinalIgnoreCase) && p.EsPartida);
        if (target == null)
        {
            WpfDialogService.Info("Cubicar", "La siguiente partida de la cola ya no está disponible.");
            PromptTakeoffQueueContinuation();
            return;
        }

        parent.SelectPartida(target);
        PromptTakeoffQueueContinuation();
    }

    public void SelectRowByTarget(string? rowId, string? code)
    {
        if (parent.Rows.Count == 0) return;
        var match = parent.Rows.FirstOrDefault(r =>
            (!string.IsNullOrWhiteSpace(rowId) && string.Equals(r.Id, rowId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(code) && string.Equals(r.Codigo, code, StringComparison.OrdinalIgnoreCase)));
        if (match != null)
        {
            parent.SelectedRow = match;
        }
    }

    public CubicacionRowVm? FindNextResource()
    {
        var needle = resourceSearchText.Trim();
        if (string.IsNullOrEmpty(needle) || parent.Rows.Count == 0)
            return null;

        int startIndex = parent.SelectedRow != null ? parent.Rows.IndexOf(parent.SelectedRow) + 1 : 0;
        for (int i = 0; i < parent.Rows.Count; i++)
        {
            var row = parent.Rows[(startIndex + i) % parent.Rows.Count];
            if (row.Codigo.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.Descripcion.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                parent.SelectedRow = row;
                return row;
            }
        }
        return null;
    }

    public void NavigateToApu()
    {
        if (parent.SelectedPartida != null && !string.IsNullOrWhiteSpace(parent.SelectedPartida.Id))
            session.PendingItemizadoSelectionId = parent.SelectedPartida.Id;
        shell.RequestNavigate(ShellRoute.Apu);
    }
}
