using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Alta rápida de materiales, pegado anclado (una celda) y pegado desde Excel (preview y
/// directo) — extraído de CubicacionViewModel.Clipboard.cs (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md). El colaborador más enredado de los 7:
/// depende conceptualmente de casi todos los anteriores (RowMutation, Sector, Persistence).
/// Llamado <c>CubicacionClipboardCoordinator</c> y no <c>CubicacionClipboardService</c> porque
/// ese nombre ya lo usa una clase estática de parseo de Excel en
/// QubiCostElectrical.Application.Services (namespace QubiCostElectrical) — un nombre
/// duplicado habría roto la resolución de <see cref="QubiCostElectrical.CubicacionClipboardService"/>
/// dentro de este mismo archivo. Comportamiento preservado tal cual.
/// </summary>
internal sealed class CubicacionClipboardCoordinator
{
    private static readonly Action UndoToastAction = () => ShellNotificationService.RequestUndo?.Invoke();

    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService undo;
    private readonly CubicacionPresenter cubicacionPresenter;
    private readonly ICubicacionTakeoffQueryService querySvc;

    public CubicacionClipboardCoordinator(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        WpfUndoService undo,
        CubicacionPresenter cubicacionPresenter,
        ICubicacionTakeoffQueryService querySvc)
    {
        this.parent = parent;
        this.session = session;
        this.undo = undo;
        this.cubicacionPresenter = cubicacionPresenter;
        this.querySvc = querySvc;
    }

    public void BeginQuickAddMaterial(string? codigo, string? descripcion, string? unidad)
    {
        if (!parent.CanCreateMaterial)
            return;

        string contexto = parent.AutocompleteVisible && !string.IsNullOrWhiteSpace(parent.AutocompleteQueryText)
            ? $"No hubo una coincidencia clara para '{parent.AutocompleteQueryText}'. Completa el material y quedará disponible en el catálogo del proyecto."
            : "Completa los campos y agrega el material al catálogo del proyecto.";

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new QuickAddMaterialDialog(
            codigo ?? parent.QuickAddCodigoDraft ?? parent.SelectedRow?.Codigo,
            descripcion ?? parent.SelectedRow?.Descripcion,
            unidad ?? parent.SelectedRow?.Unidad,
            contexto,
            parent.SelectedRow?.ValorUfClp ?? 0m)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() != true || dialog.Result == null)
            return;

        AddMaterialToCatalog(dialog.Result, applyToSelectedRow: true);
    }

    public bool AddMaterialToCatalog(MaterialRow material, bool applyToSelectedRow)
    {
        if (!parent.CanCreateMaterial || material == null)
            return false;

        if (session.Materiales.Any(m =>
                string.Equals(m.Codigo, material.Codigo, StringComparison.OrdinalIgnoreCase)))
        {
            ShellNotificationService.Instance.ShowWarning(
                "Material duplicado",
                $"El código '{material.Codigo}' ya existe en el catálogo del proyecto.");
            return false;
        }

        undo.PushUndoPoint(session, $"Agregar material {material.Codigo}");
        session.Materiales.Add(material);
        session.MarkDirty(ProjectDirtyScope.Materiales);
        parent.InvalidateAutocompleteMaterialIndex();
        parent.ValidationMaterialesIndex = null;

        if (applyToSelectedRow && parent.SelectedRow != null)
            parent.ApplyMaterial(material);
        else
            ShellNotificationService.Instance.ShowSuccess(
                "Material agregado",
                $"{material.Codigo} quedó disponible en el catálogo del proyecto.");

        return true;
    }

    // Agrupa varias ediciones de celda (pegado/borrado multi-celda estilo Excel) bajo un solo
    // punto de undo, en vez de uno por celda tocada.
    public void RunBatchedEdit(string undoLabel, Action action)
    {
        if (!parent.CanEdit || session.IsReadOnly)
            return;

        undo.PushUndoPoint(session, undoLabel);
        parent.HasPendingUndoForEdit = true;
        try
        {
            action();
        }
        finally
        {
            parent.HasPendingUndoForEdit = false;
        }
    }

    public bool TryPasteAnchored(DataGrid grid, string text)
    {
        var selectedPartida = parent.SelectedPartida;
        if (!parent.CanEdit || selectedPartida == null || !selectedPartida.EsPartida)
            return false;
        if (!grid.CurrentCell.IsValid || grid.CurrentCell.Item is not CubicacionRowVm row)
            return false;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return false;

        int startColumn = CubicacionViewModel.ResolveAnchoredPasteColumn(grid.CurrentCell.Column);
        if (startColumn < 0)
            return false;

        int startRow = grid.Items.IndexOf(row);
        if (startRow < 0)
            return false;

        int maxColumn = CubicacionViewModel.FixedPasteColumnCount + CubicacionViewModel.MaximumSectorCount - 1;

        undo.PushUndoPoint(session, "Pegar cubicación");
        // Evita que OnRowEdited() empuje undo points adicionales por cada celda tocada
        // durante este pegado; ya se registró uno solo que cubre toda la operación.
        parent.HasPendingUndoForEdit = true;

        if (lines.Length == 1 && !lines[0].Contains('\t'))
        {
            ApplyPastedCells(row, startColumn, new[] { lines[0].Trim() }, maxColumn);
            if (startColumn < CubicacionViewModel.FixedPasteColumnCount)
                parent.OnRowEdited(row);
            parent.HasPendingUndoForEdit = false;
            return true;
        }

        int imported = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var cells = lines[i].Split('\t');
            int targetIndex = startRow + i;
            CubicacionRowVm targetRow;

            if (targetIndex < parent.Rows.Count)
            {
                targetRow = parent.Rows[targetIndex];
            }
            else
            {
                var source = new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = selectedPartida.Id,
                    Tipo = "MAT",
                    Area = "General"
                };
                session.Cubicacion.Add(source);
                targetRow = parent.CreateRowVm(source);
                parent.InitializeSectorRow(targetRow);
                parent.Rows.Add(targetRow);
            }

            ApplyPastedCells(targetRow, startColumn, cells, maxColumn);
            parent.EnsureRowInSession(targetRow);
            parent.TryAutoResolveFromPriceBank(targetRow);
            targetRow.RefreshDisplayProperties();
            cubicacionPresenter.AfterRowEdited(targetRow.Source);
            imported++;
        }

        parent.HasPendingUndoForEdit = false;
        if (imported > 0)
        {
            if (parent.SelectedRow != null)
                parent.RightPanel.LoadRow(parent.SelectedRow);
            parent.ScheduleRefreshTotals();
            parent.ValidateRows();
            parent.UpdatePartidaCount(selectedPartida);
            parent.NotifyCubicacionSummaryChanged();
            parent.MarkDirty();
            parent.NotifyIsEmptyChanged();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        ShellNotificationService.Instance.ShowSuccess(
            "Pegado",
            $"{imported} fila(s) actualizada(s) desde el portapapeles.",
            actionLabel: "Deshacer",
            action: UndoToastAction);
        return true;
    }

    private void ApplyPastedCells(CubicacionRowVm row, int startColumn, string[] cells, int maxColumn)
    {
        row.BeginBatchUpdate();
        try
        {
            for (int c = 0; c < cells.Length; c++)
            {
                int columnIndex = startColumn + c;
                if (columnIndex > maxColumn)
                    break;
                ApplyPastedValue(row, columnIndex, cells[c].Trim());
            }
        }
        finally
        {
            row.EndBatchUpdate();
        }
    }

    private static void ApplyPastedValue(CubicacionRowVm row, int columnIndex, string value)
    {
        if (columnIndex >= CubicacionViewModel.FixedPasteColumnCount)
        {
            int slot = columnIndex - CubicacionViewModel.FixedPasteColumnCount;
            if (slot >= 0 && slot < CubicacionViewModel.MaximumSectorCount)
                row.SectorValues[slot] = value;
            return;
        }

        switch (columnIndex)
        {
            case 0:
                row.Codigo = value;
                break;
            case 1:
                row.Descripcion = value;
                break;
            case 2:
                row.Unidad = value;
                break;
            case 3:
                if (QuantityFormulaService.IsFormulaLike(value))
                {
                    row.Source.TotalExpression = value;
                    row.Source.QtyExpressions = Array.Empty<string>();
                }
                else if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
                    row.Cantidad = qty;
                else if (string.IsNullOrWhiteSpace(value))
                    row.Cantidad = 0m;
                break;
        }
    }

    public void PasteClipboardDirect(string text, bool showPreviewDialog)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var result = CubicacionClipboardService.ParseExcelText(text);
        if (result.Rows.Count == 0)
        {
            ShellNotificationService.Instance.ShowWarning("Pegado", "No se detectaron filas en el portapapeles.");
            return;
        }

        if (showPreviewDialog)
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            var dialog = new ExcelPastePreviewDialog(result) { Owner = owner };
            var previous = System.Windows.Input.Keyboard.FocusedElement as System.Windows.IInputElement;
            try
            {
                if (dialog.ShowDialog() == true)
                    ApplyClipboardRows(dialog.RowsToImport);
            }
            finally
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    previous?.Focus();
                    ShellWindow.RestoreWorkspaceGridFocus();
                }, System.Windows.Threading.DispatcherPriority.Input);
            }
            return;
        }

        var accepted = result.Rows.Where(r => !r.IsRejected).ToList();
        if (accepted.Count == 0)
        {
            ShellNotificationService.Instance.ShowWarning(
                "Pegado",
                $"{result.RejectedCount} fila(s) rechazada(s). Revise cantidades y códigos.");
            return;
        }

        ApplyClipboardRows(accepted);
        int rejected = result.RejectedCount;
        string msg = rejected > 0
            ? $"{accepted.Count} fila(s) importada(s). {rejected} rechazada(s)."
            : $"{accepted.Count} fila(s) importada(s).";
        ShellNotificationService.Instance.ShowSuccess("Pegado", msg, actionLabel: "Deshacer", action: UndoToastAction);
        parent.ValidateRows();
    }

    public void ApplyClipboardRows(IEnumerable<CubicacionClipboardRow> rows)
    {
        var selectedPartida = parent.SelectedPartida;
        if (session.IsReadOnly || selectedPartida == null || !selectedPartida.EsPartida) return;

        undo.PushUndoPoint(session, $"Pegar {rows.Count(r => !r.IsRejected)} recurso(s)");

        foreach (var cr in rows.Where(r => !r.IsRejected))
        {
            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = selectedPartida.Id,
                Tipo = "MAT",
                Area = "General",
                Codigo = cr.Codigo ?? "",
                Descripcion = cr.Descripcion ?? "",
                Unidad = cr.Unidad ?? "",
                Cantidad = CubicacionClipboardService.ParseCantidad(cr.CantidadRaw)
            };

            if (!string.IsNullOrWhiteSpace(cr.CantidadRaw) &&
                QuantityFormulaService.IsFormulaLike(cr.CantidadRaw))
            {
                row.TotalExpression = cr.CantidadRaw;
                row.Cantidad = querySvc.EvaluateStoredExpressions(session, row);
            }
            else if (cr.SectorValues.Count > 0 && cr.SectorValues.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                var expressions = new string[cr.SectorValues.Count];
                for (int i = 0; i < cr.SectorValues.Count; i++)
                    expressions[i] = cr.SectorValues[i] ?? "";
                row.QtyExpressions = expressions;
                row.TotalExpression = string.Empty;
                row.Cantidad = querySvc.EvaluateStoredExpressions(session, row);
            }

            session.Cubicacion.Add(row);
            var vm = parent.CreateRowVm(row);
            parent.InitializeSectorRow(vm);
            if (cr.SectorValues.Count > 0)
            {
                vm.BeginBatchUpdate();
                try
                {
                    int limit = Math.Min(cr.SectorValues.Count, CubicacionViewModel.MaximumSectorCount);
                    for (int i = 0; i < limit; i++)
                        vm.SectorValues[i] = cr.SectorValues[i] ?? "";
                }
                finally
                {
                    vm.EndBatchUpdate();
                }
            }
            parent.Rows.Add(vm);
            // Este camino de pegado (vista previa) no pasaba por OnRowEdited, así que a
            // diferencia del pegado directo/anclado nunca completaba Descripción/MAT UF/MO
            // UF desde el catálogo o el Banco de Precios cuando solo se pegaba código+cant.
            parent.TryAutoResolveFromPriceBank(vm);
            // Ver comentario en CubicacionRowMutationService.ApplyMaterial: este camino (pegado
            // con vista previa) crea la fila directo en session.Cubicacion sin pasar por
            // OnRowEdited/AfterRowEdited, así que sin esto la fila también quedaría sin
            // sincronizar hacia la composición normalizada.
            session.SyncCubicacionRowComposition(row);
        }

        parent.MarkDirty();
        parent.ScheduleRefreshTotals();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        parent.ValidateRows();
    }
}
