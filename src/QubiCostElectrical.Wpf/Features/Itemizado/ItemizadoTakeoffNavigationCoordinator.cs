using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoTakeoffNavigationCoordinator
{
    public static bool CanCubicar(
        BudgetProjectSession session,
        ItemizadoPresenter presenter,
        IReadOnlyList<ItemizadoRowVm> allRows,
        bool isSelectionVisible,
        ItemizadoRowVm? selectedRow,
        Func<ItemizadoRowVm, bool> passesFilter)
    {
        if (session.IsReadOnly)
            return false;

        return GetSelectedPartidaIds(presenter, allRows, passesFilter).Count > 0
            || (isSelectionVisible && selectedRow != null && (!selectedRow.EsTitulo || !presenter.HasChildPartidas(selectedRow.Id)));
    }

    public static bool CanDuplicate(
        BudgetProjectSession session,
        bool isSelectionVisible,
        ItemizadoRowVm? selectedRow,
        bool showEmptyState,
        IReadOnlyList<ItemizadoRowVm> allRows)
    {
        return !session.IsReadOnly
            && isSelectionVisible
            && selectedRow != null
            && !selectedRow.EsTitulo
            && !showEmptyState
            && allRows.Count(r => r.IsSelected) <= 1;
    }

    public static List<string> GetSelectedPartidaIds(
        ItemizadoPresenter presenter,
        IReadOnlyList<ItemizadoRowVm> allRows,
        Func<ItemizadoRowVm, bool> passesFilter)
    {
        return allRows
            .Where(x => x.IsSelected && (!x.EsTitulo || !presenter.HasChildPartidas(x.Id)) && passesFilter(x))
            .Select(x => x.Id)
            .ToList();
    }

    public static async Task CubicarSelectedAsync(
        BudgetProjectSession session,
        IShellNavigationHost shell,
        ItemizadoPresenter presenter,
        IReadOnlyList<ItemizadoRowVm> allRows,
        bool isSelectionVisible,
        ItemizadoRowVm? selectedRow,
        Func<ItemizadoRowVm, bool> passesFilter,
        Action<string> setStatusMessage)
    {
        if (session.IsReadOnly)
            return;

        var ids = GetSelectedPartidaIds(presenter, allRows, passesFilter);
        if (ids.Count == 0 && isSelectionVisible && selectedRow != null && (!selectedRow.EsTitulo || !presenter.HasChildPartidas(selectedRow.Id)))
            ids.Add(selectedRow.Id);

        if (ids.Count == 0)
        {
            setStatusMessage("Selecciona al menos una partida para cubicar.");
            return;
        }

        var valid = new List<(string Id, ItemizadoCubicacionNavigation Nav)>();
        string? firstError = null;
        foreach (var id in ids)
        {
            string? validation = presenter.ValidateCubicacionNavigation(id);
            if (!string.IsNullOrWhiteSpace(validation))
            {
                firstError ??= validation;
                continue;
            }

            var navigation = presenter.TryBuildCubicacionNavigation(id);
            if (navigation == null)
                continue;

            valid.Add((id, navigation));
        }

        if (selectedRow != null && selectedRow.EsTitulo != selectedRow.Model.EsTitulo)
        {
            selectedRow.IsTitleRow = selectedRow.Model.EsTitulo;
            selectedRow.UpdateModel(selectedRow.Model);
        }

        if (valid.Count == 0)
        {
            WpfDialogService.Info("Cubicar", firstError ?? "Ninguna de las partidas seleccionadas se puede cubicar.");
            return;
        }

        if (valid.Count > 1 &&
            !WpfDialogService.Confirm(
                "Cubicar",
                $"¿Abrir cubicación de {valid.Count:N0} partidas en secuencia?\n\nSe abrirá la primera; el resto quedará en cola con aviso «Siguiente».",
                isWarning: false))
            return;

        session.SetPendingTakeoffQueue(
            valid.Count > 1 ? valid.Skip(1).Select(v => v.Id).ToList() : null);

        var first = valid[0].Nav;
        session.PendingTakeoffNavigation = new TakeoffNavigationContext(
            first.ItemId, first.ItemCode, first.Description);
        session.PendingItemizadoSelectionId = first.ItemId;
        shell.RequestNavigate(ShellRoute.Takeoff, session.PendingTakeoffNavigation);
    }
}
