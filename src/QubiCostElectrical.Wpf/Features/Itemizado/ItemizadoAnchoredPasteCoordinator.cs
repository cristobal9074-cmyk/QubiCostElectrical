using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

internal static class ItemizadoAnchoredPasteCoordinator
{
    private const int MaxEditableColumnIndex = 4;

    public static bool TryPaste(
        DataGrid grid,
        string text,
        BudgetProjectSession session,
        WpfUndoService undo,
        ItemizadoPresenter presenter,
        IReadOnlyList<ItemizadoRowVm> allRowsList,
        ItemizadoFilterMode filterMode,
        string debouncedSearchText,
        Action syncRowModels,
        Action updateSaveState,
        Action refreshPresentationWithCosts,
        Action<string, ItemizadoRowVm?> onCellEdited,
        Action<string> setStatusMessage)
    {
        if (!grid.CurrentCell.IsValid || grid.CurrentCell.Item is not ItemizadoRowVm row)
            return false;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length > 1 && lines[^1].Length == 0)
            lines = lines[..^1];
        if (lines.Length == 0)
            return false;

        string header = grid.CurrentCell.Column?.Header?.ToString() ?? "";
        string? bindingPath = (grid.CurrentCell.Column as DataGridBoundColumn)?.Binding is Binding binding
            ? binding.Path?.Path
            : null;

        int startColumn = bindingPath switch
        {
            "Item" => 0,
            "Descripcion" => 1,
            "Unidad" => 2,
            "CantidadText" => 3,
            "ExcluidoCubicacion" => 4,
            _ => -1
        };
        if (startColumn < 0)
            return false;

        int startRow = grid.Items.IndexOf(row);
        if (startRow < 0)
            return false;

        int maxPastedColumns = lines.Max(l => l.Split('\t').Length);
        int overflowColumns = Math.Max(0, startColumn + maxPastedColumns - 1 - MaxEditableColumnIndex);

        if (lines.Length == 1 && !lines[0].Contains('\t'))
        {
            ItemizadoPasteRowApplicator.Apply(row, startColumn, lines[0].Trim(), session.Itemizado);
            onCellEdited(header, row);
            if (overflowColumns > 0)
                setStatusMessage($"Se ignoró {overflowColumns} columna(s) pegada(s) fuera de rango.");
            return true;
        }

        bool canGrowGrid = !session.IsReadOnly
            && filterMode == ItemizadoFilterMode.Todos
            && string.IsNullOrWhiteSpace(debouncedSearchText);
        int requiredNewRows = startRow + lines.Length - grid.Items.Count;
        int rowsSkippedForGrowth = 0;

        undo.PushUndoPoint(session, "Pegar celdas itemizado");

        bool grewGrid = false;
        if (requiredNewRows > 0)
        {
            if (canGrowGrid)
            {
                int sessionIndex = session.Itemizado.Count - 1;
                for (int g = 0; g < requiredNewRows; g++)
                {
                    var addResult = presenter.AddBelow(sessionIndex, string.Empty, string.Empty);
                    sessionIndex = addResult.SelectedIndex;
                }
                syncRowModels();
                grewGrid = true;
            }
            else
            {
                rowsSkippedForGrowth = requiredNewRows;
            }
        }

        IReadOnlyList<ItemizadoRowVm> targetRows = grewGrid
            ? allRowsList
            : grid.Items.OfType<ItemizadoRowVm>().ToList();

        var peers = session.Itemizado;
        int appliedRows = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int targetIndex = startRow + i;
            if (targetIndex >= targetRows.Count)
                break;
            if (lines[i].Length == 0)
                continue;
            var targetRow = targetRows[targetIndex];
            if (targetRow.EsTitulo)
                continue;

            var cells = lines[i].Split('\t');
            for (int c = 0; c < cells.Length; c++)
            {
                int columnIndex = startColumn + c;
                if (columnIndex > MaxEditableColumnIndex)
                    break;
                ItemizadoPasteRowApplicator.Apply(targetRow, columnIndex, cells[c].Trim(), peers);
            }
            targetRow.ValidateEditableCells(peers);
            appliedRows++;
        }

        session.MarkDirty(ProjectDirtyScope.Itemizado);
        updateSaveState();
        refreshPresentationWithCosts();

        var warnings = new List<string>();
        if (rowsSkippedForGrowth > 0)
            warnings.Add($"{rowsSkippedForGrowth} fila(s) no se pegaron porque el pegado excede la grilla visible: quita filtros/búsqueda y reintenta.");
        if (overflowColumns > 0)
            warnings.Add($"Se ignoraron {overflowColumns} columna(s) pegada(s) fuera de rango.");

        if (warnings.Count > 0)
        {
            string msg = string.Join(" ", warnings);
            setStatusMessage(msg);
            ShellNotificationService.Instance.ShowWarning("Portapapeles", msg);
        }
        else
        {
            string msg = $"{appliedRows} fila(s) actualizadas desde el portapapeles.";
            setStatusMessage(msg);
            if (appliedRows > 0)
            {
                ShellNotificationService.Instance.ShowSuccess(
                    "Portapapeles",
                    msg,
                    actionLabel: "Deshacer",
                    action: () => undo.Undo(session));
            }
        }

        return true;
    }
}
