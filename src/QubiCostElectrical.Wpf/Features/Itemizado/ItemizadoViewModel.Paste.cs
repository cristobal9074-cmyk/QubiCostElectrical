using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    public async Task HandlePasteKeyAsync(DataGrid? grid = null, bool showPreview = false)
    {
        if (!Clipboard.ContainsText())
            return;

        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (grid != null && TryPasteAnchored(grid, text))
            return;

        if (SelectedRow != null && !text.Contains('\n') && !text.Contains('\t'))
            return;

        await PasteFromClipboardAsync(text, showPreview);
    }

    private const int MaxEditableColumnIndex = 4;

    internal bool TryPasteAnchored(DataGrid grid, string text)
    {
        if (!grid.CurrentCell.IsValid || grid.CurrentCell.Item is not ItemizadoRowVm row)
            return false;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        // Excel agrega un salto de línea final al copiar; no es una fila separadora real.
        if (lines.Length > 1 && lines[^1].Length == 0)
            lines = lines[..^1];
        if (lines.Length == 0)
            return false;

        string header = grid.CurrentCell.Column?.Header?.ToString() ?? "";
        // Se usa la ruta del binding (estable ante renombres de encabezado o reordenamiento
        // visual de columnas) en vez del texto del encabezado, para que el pegado anclado no
        // se rompa silenciosamente si alguien traduce o reordena las columnas.
        string? bindingPath = (grid.CurrentCell.Column as System.Windows.Controls.DataGridBoundColumn)?.Binding is System.Windows.Data.Binding binding
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
            ApplyPastedValue(row, startColumn, lines[0].Trim(), host.Session.Itemizado);
            OnCellEdited(header, row);
            if (overflowColumns > 0)
                StatusMessage = $"Se ignoró {overflowColumns} columna(s) pegada(s) fuera de rango.";
            return true;
        }

        bool canGrowGrid = !host.Session.IsReadOnly
            && FilterMode == ItemizadoFilterMode.Todos
            && string.IsNullOrWhiteSpace(debouncedSearchText);
        int requiredNewRows = startRow + lines.Length - grid.Items.Count;
        int rowsSkippedForGrowth = 0;

        host.UndoService.PushUndoPoint(host.Session, "Pegar celdas itemizado");
        hasPendingUndoForEdit = false;

        bool grewGrid = false;
        if (requiredNewRows > 0)
        {
            if (canGrowGrid)
            {
                int sessionIndex = host.Session.Itemizado.Count - 1;
                for (int g = 0; g < requiredNewRows; g++)
                {
                    var addResult = presenter.AddBelow(sessionIndex, string.Empty, string.Empty);
                    sessionIndex = addResult.SelectedIndex;
                }
                SyncRowModels();
                grewGrid = true;
            }
            else
            {
                rowsSkippedForGrowth = requiredNewRows;
            }
        }

        // grid.Items (la vista del DataGrid) no siempre refleja de inmediato las filas recién
        // agregadas por SyncRowModels dentro de la misma llamada síncrona. Rows sí lo hace, ya
        // que SyncRowModels lo muta directamente. Cuando hubo crecimiento (sólo posible sin
        // filtro activo, momento en que Rows y la vista visible coinciden en contenido y orden)
        // se usa Rows como fuente de verdad para no perder filas recién creadas.
        IReadOnlyList<ItemizadoRowVm> targetRows = grewGrid
            ? Rows
            : grid.Items.OfType<ItemizadoRowVm>().ToList();

        var peers = host.Session.Itemizado;
        int appliedRows = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int targetIndex = startRow + i;
            if (targetIndex >= targetRows.Count)
                break;
            // Línea vacía del origen (separador de sección): conserva la alineación sin tocar la fila.
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
                ApplyPastedValue(targetRow, columnIndex, cells[c].Trim(), peers);
            }
            targetRow.ValidateEditableCells(peers);
            appliedRows++;
        }

        host.Session.MarkDirty("itemizado");
        UpdateSaveState();
        RefreshPresentation(includeCosts: true);

        var warnings = new List<string>();
        if (rowsSkippedForGrowth > 0)
            warnings.Add($"{rowsSkippedForGrowth} fila(s) no se pegaron porque el pegado excede la grilla visible: quita filtros/búsqueda y reintenta.");
        if (overflowColumns > 0)
            warnings.Add($"Se ignoraron {overflowColumns} columna(s) pegada(s) fuera de rango.");
        StatusMessage = warnings.Count > 0
            ? string.Join(" ", warnings)
            : $"{appliedRows} fila(s) actualizadas desde el portapapeles.";

        return true;
    }

    private void ApplyPastedValue(ItemizadoRowVm row, int columnIndex, string value, IReadOnlyList<ItemizadoRow> peers)
    {
        switch (columnIndex)
        {
            case 0:
                row.Item = value;
                row.CommitItemEdit(peers);
                break;
            case 1:
                row.Descripcion = value;
                break;
            case 2:
                row.Unidad = value;
                break;
            case 3:
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var qty))
                    row.Cantidad = qty;
                break;
            case 4:
                if (ItemizadoPasteValueParser.TryParseExcluidoCubicacion(value, out var excluded))
                    row.ExcluidoCubicacion = excluded;
                break;
        }
    }

    private async Task ImportExcelAsync()
    {
        if (host.Session.IsReadOnly)
        {
            StatusMessage = "Solo lectura: " + host.Session.ReadOnlyReason;
            return;
        }
        var path = WpfDialogService.PromptOpenExcel();
        if (path == null)
            return;

        StatusMessage = "Leyendo archivo…";
        try
        {
            var outcome = await importCoordinator.ImportExcelAsync(path);
            if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
            {
                WpfDialogService.Error(outcome.ErrorTitle ?? "Importar Excel", outcome.ErrorMessage);
                StatusMessage = outcome.StatusMessage ?? $"Error al leer: {outcome.ErrorMessage}";
                return;
            }

            if (outcome.Cancelled)
            {
                StatusMessage = outcome.StatusMessage ?? "Importación cancelada.";
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            var plan = outcome.Plan;
            if (plan.RequiresReplaceConfirmation &&
                !WpfDialogService.Confirm(plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(plan.CurrentRowCount, plan.ActionLabel), isWarning: true))
            {
                StatusMessage = "Importación cancelada.";
                return;
            }

            host.UndoService.PushUndoPoint(host.Session, "Importar itemizado");
            presenter.CommitReplace(outcome.Rows);
            pendingSelectionId = outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id;
            Reload(syncOnly: false);
            StatusMessage = outcome.StatusMessage ?? "Importación completada.";
            UpdateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló la importación de Excel", ex);
            StatusMessage = OperationFailureMessages.ToUserMessage(ex);
            WpfDialogService.Error("Error al importar", StatusMessage);
        }
    }

    private async Task PasteFromClipboardAsync() => await PasteFromClipboardAsync(Clipboard.GetText());

    private async Task PasteFromClipboardAsync(string text, bool showPreview = true)
    {
        if (host.Session.IsReadOnly)
        {
            var reason = host.Session.ReadOnlyReason;
            StatusMessage = string.IsNullOrWhiteSpace(reason)
                ? "Proyecto en modo solo lectura."
                : $"Solo lectura: {reason}";
            WpfDialogService.Warning("Pegar itemizado", StatusMessage);
            return;
        }
        try
        {
            var (result, plan) = await Task.Run(() => presenter.TryBuildPastePlan(text));
            var outcome = importCoordinator.ResolvePaste(result, plan, showPreview);

            if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
            {
                WpfDialogService.Error(outcome.ErrorTitle ?? "Pegar itemizado", outcome.ErrorMessage);
                if (!string.IsNullOrWhiteSpace(outcome.StatusMessage))
                    StatusMessage = outcome.StatusMessage;
                return;
            }

            if (outcome.Cancelled)
            {
                StatusMessage = outcome.StatusMessage ?? "Pegado cancelado.";
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            if (outcome.Plan.RequiresReplaceConfirmation &&
                !WpfDialogService.Confirm(outcome.Plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(outcome.Plan.CurrentRowCount, outcome.Plan.ActionLabel), isWarning: true))
                return;

            host.UndoService.PushUndoPoint(host.Session, "Pegar itemizado");
            presenter.CommitReplace(outcome.Rows);
            pendingSelectionId = outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id;
            Reload(syncOnly: false);
            StatusMessage = outcome.StatusMessage ?? "Pegado completado.";
            UpdateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló pegar itemizado desde el portapapeles", ex);
            WpfDialogService.Error("Pegar itemizado", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
