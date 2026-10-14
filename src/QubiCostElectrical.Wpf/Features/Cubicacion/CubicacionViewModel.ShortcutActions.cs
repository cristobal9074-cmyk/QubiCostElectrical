using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    public ICommand OpenPrecioListaCommand { get; private set; } = null!;
    public ICommand OpenScTomarCommand { get; private set; } = null!;
    public ICommand OpenItemizadoCommand { get; private set; } = null!;
    public ICommand OpenQuickJumpCommand { get; private set; } = null!;
    public ICommand OpenBatchReplaceCommand { get; private set; } = null!;
    public ICommand OpenFamilyBatchReplaceCommand { get; private set; } = null!;

    private void InitializeShortcutCommands()
    {
        OpenPrecioListaCommand = new RelayCommand(OpenPrecioLista);
        OpenScTomarCommand = new RelayCommand(OpenCubicacionScTomar, () => CanEdit);
        OpenItemizadoCommand = new RelayCommand(OpenCubicacionItemizado);
        OpenQuickJumpCommand = new RelayCommand(OpenQuickJumpPartida);
        OpenBatchReplaceCommand = new RelayCommand(OpenBatchReplace, () => CanEdit);
        OpenFamilyBatchReplaceCommand = new RelayCommand(OpenFamilyBatchReplace, () => CanEdit);
    }

    internal bool TryExecuteShortcut(string actionId)
    {
        switch (actionId)
        {
            case CubicacionShortcutCatalog.FamilyBatchReplace:
                if (OpenFamilyBatchReplaceCommand.CanExecute(null))
                {
                    OpenFamilyBatchReplaceCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.BatchReplace:
                if (OpenBatchReplaceCommand.CanExecute(null))
                {
                    OpenBatchReplaceCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.QuickJump:
                if (OpenQuickJumpCommand.CanExecute(null))
                {
                    OpenQuickJumpCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.PartidaPrevious:
                NavigatePartidaRelative(-1);
                return true;
            case CubicacionShortcutCatalog.PartidaNext:
                NavigatePartidaRelative(1);
                return true;
            case CubicacionShortcutCatalog.AddPartida:
                if (AddPartidaCommand.CanExecute(null))
                {
                    AddPartidaCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.DeletePartida:
                if (DeletePartidaCommand.CanExecute(null))
                {
                    DeletePartidaCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.RowMoveUp:
                if (MoveRowUpCommand.CanExecute(null))
                {
                    MoveRowUpCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.RowMoveDown:
                if (MoveRowDownCommand.CanExecute(null))
                {
                    MoveRowDownCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.PrecioLista:
                if (OpenPrecioListaCommand.CanExecute(null))
                {
                    OpenPrecioListaCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.ScTomar:
                if (OpenScTomarCommand.CanExecute(null))
                {
                    OpenScTomarCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.Itemizado:
                if (OpenItemizadoCommand.CanExecute(null))
                {
                    OpenItemizadoCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.Plantilla:
                if (AplicarConjuntoCommand.CanExecute(null))
                {
                    AplicarConjuntoCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.SectorMatrix:
                IsSectorMatrixMode = !IsSectorMatrixMode;
                return true;
            case CubicacionShortcutCatalog.ResourceDetail:
                IsSectorMatrixMode = false;
                return true;
            case CubicacionShortcutCatalog.FillDown:
                RequestFillDown?.Invoke();
                return true;
            case CubicacionShortcutCatalog.FillRight:
                RequestFillRight?.Invoke();
                return true;
            case CubicacionShortcutCatalog.RepeatMeasurement:
                RequestRepeatMeasurement?.Invoke();
                return true;
            case CubicacionShortcutCatalog.NoCubicar:
                return TryToggleExclusionFromShortcut();
            case CubicacionShortcutCatalog.Observacion:
                return TryFocusObservacionFromShortcut();
            default:
                return false;
        }
    }

    public Action? RequestFillDown { get; set; }
    public Action? RequestFillRight { get; set; }
    public Action? RequestRepeatMeasurement { get; set; }

    /// <summary>Ctrl+E: excluye/incluye (sin saltar al campo de observación).</summary>
    private bool TryToggleExclusionFromShortcut()
    {
        if (session.IsReadOnly || SelectedPartida?.EsPartida != true)
            return false;

        ExcluidoCubicacion = !ExcluidoCubicacion;
        return true;
    }

    /// <summary>Ctrl+Shift+E: enfoca el campo de observación para escribir de inmediato.</summary>
    private bool TryFocusObservacionFromShortcut()
    {
        if (session.IsReadOnly || SelectedPartida?.EsPartida != true)
            return false;

        RequestFocusMotivoNoCubicar?.Invoke();
        return true;
    }

    /// <summary>La vista enfoca el cuadro de observación.</summary>
    public event Action? RequestFocusMotivoNoCubicar;

    private void NavigatePartidaRelative(int delta)
    {
        var list = FilteredPartidas.Count > 0 ? FilteredPartidas : Partidas;
        if (list.Count == 0) return;

        var current = SelectedPartida;
        var index = current != null ? list.IndexOf(current) : -1;
        if (index < 0) index = 0;
        else index = Math.Clamp(index + delta, 0, list.Count - 1);

        var target = list[index];
        if (target.EsPartida)
            SelectPartida(target);
        else
        {
            for (var step = delta >= 0 ? 1 : -1; index >= 0 && index < list.Count; index += step)
            {
                if (list[index].EsPartida)
                {
                    SelectPartida(list[index]);
                    break;
                }
            }
        }
    }

    private void OpenPrecioLista()
    {
        var codigo = "";
        if (SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.Codigo))
            codigo = SelectedRow.Codigo;
        else if (SelectedPartida != null && !string.IsNullOrWhiteSpace(SelectedPartida.Codigo))
            codigo = SelectedPartida.Codigo;

        if (!string.IsNullOrWhiteSpace(codigo) && priceBankStore.GetByCodigo(codigo) == null) codigo = "";

        var dialog = new PrecioListaResumenDialog(session, priceBankStore, codigo) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        rowMutationService.AplicarMaterialCreado(dialog.MaterialCreado);
    }

    private void OpenCubicacionScTomar()
    {
        var cuadros = session.CubicacionSC;
        var config = session.CubicacionScConfig;
        if (cuadros == null || cuadros.Count == 0 || config == null)
        {
            WpfDialogService.Info("Sin datos SC", "No hay datos de servicios comunes en el proyecto actual.");
            return;
        }

        var dialog = new ResumenScTomarDialog(cuadros, config, session.ZonasProyecto) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result == null)
            return;

        var row = SelectedRow;
        if (row == null) return;

        if (row.HasContent &&
            !WpfDialogService.Confirm(
                "Tomar de SC",
                "La fila ya tiene contenido. ¿Deseas sobrescribirla con los datos de Servicios Comunes?",
                isWarning: true))
            return;

        var material = dialog.Result;
        row.Codigo = material.Nombre;
        row.Descripcion = material.Nombre;
        row.Unidad = material.Unidad;
        row.Tipo = "MAT";

        var zonas = session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        row.BeginBatchUpdate();
        for (var i = 0; i < material.ZonaQties.Count && i < zonas.Count; i++)
        {
            var zonaQty = material.ZonaQties[i];
            var zonaNombre = zonas[i].Nombre;
            if (!string.Equals(zonaQty.ZonaNombre, zonaNombre, StringComparison.OrdinalIgnoreCase))
                continue;

            if (decimal.TryParse(zonaQty.Text, out var qty) && qty > 0m)
                SetSectorExpressionDirect(row, i, qty.ToString("0.##"));
        }
        row.EndBatchUpdate();
        row.RefreshDisplayProperties();
        RightPanel.LoadRow(row);
        ValidateRow(row);
        MarkDirty();
        NotifyCubicacionSummaryChanged();
    }

    private void OpenCubicacionItemizado()
    {
        if (Partidas.Count == 0)
        {
            WpfDialogService.Info("Sin partidas", "No hay partidas en el proyecto actual.");
            return;
        }

        var costs = new Dictionary<string, PartidaCost>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in session.Cubicacion)
        {
            var itemId = item.ItemId ?? "";
            if (itemId.Length == 0) continue;
            var mat = item.MatClp * item.Cantidad;
            var mo = item.MoClp * item.Cantidad;
            if (costs.TryGetValue(itemId, out var existing))
                costs[itemId] = new PartidaCost(existing.Mat + mat, existing.Mo + mo, existing.Total + mat + mo);
            else
                costs[itemId] = new PartidaCost(mat, mo, mat + mo);
        }

        var items = Partidas.Select(p =>
        {
            var cost = p.Id != null && costs.TryGetValue(p.Id, out var v) ? v : new PartidaCost(0m, 0m, 0m);
            return new ItemizadoPartidaVm
            {
                EsSeccion = !p.EsPartida,
                Id = p.Id ?? "",
                ItemCode = p.Codigo ?? "",
                Descripcion = p.Descripcion,
                Unidad = p.Unidad,
                Cantidad = p.Cantidad,
                MaterialTotal = cost.Mat,
                ManoObraTotal = cost.Mo,
                CostoDirecto = cost.Total
            };
        }).ToList();

        SetSessionChangeActive(false);
        try
        {
            var dialog = new ResumenItemizadoDialog(items, SelectedPartida?.Id, session, itemizadoPresenter, undo) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (dialog.HasMutations)
            {
                session.MarkDirty(ProjectDirtyScope.Itemizado);
                loadingService.LoadPartidas();
                NotifyCubicacionSummaryChanged();
            }

            var resultTarget = dialog.Result ?? (dialog.HasMutations ? dialog.SelectedRow : null);
            if (resultTarget == null) return;

            var targetId = resultTarget.Id;
            var targetPartida = Partidas.FirstOrDefault(p => string.Equals(p.Id, targetId, StringComparison.OrdinalIgnoreCase));
            if (targetPartida != null)
            {
                if (!FilteredPartidas.Contains(targetPartida)) IsPartidaFilterTodos = true;
                SelectPartida(targetPartida);
            }

            if (dialog.ShouldAddResource && AddRowCommand.CanExecute(null)) AddRowCommand.Execute(null);

            if (dialog.ShouldDeleteRows)
            {
                var rowIds = session.Cubicacion.Where(r => string.Equals(r.ItemId, targetId, StringComparison.OrdinalIgnoreCase)).Select(r => r.Id).ToList();
                if (rowIds.Count > 0) cubicacionPresenter.DeleteRows(rowIds);
            }
        }
        finally { SetSessionChangeActive(true); }
    }

    public void OpenQuickJumpPartida()
    {
        if (Partidas.Count == 0) return;

        string currentCapitulo = "";
        var jumpItems = new List<QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem>(Partidas.Count);

        foreach (var p in Partidas)
        {
            if (!p.EsPartida)
            {
                currentCapitulo = p.Descripcion;
                jumpItems.Add(new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
                {
                    Id = p.Id ?? "",
                    Codigo = p.Codigo,
                    Descripcion = p.Descripcion,
                    EsTitulo = true
                });
            }
            else
            {
                string badge = p.ExcluidoCubicacion
                    ? "Excluida"
                    : (p.TieneCubicacion ? $"{p.RecursoCount} recurso(s)" : "Sin cubicar");

                jumpItems.Add(new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
                {
                    Id = p.Id ?? "",
                    Codigo = p.Codigo,
                    Descripcion = p.Descripcion,
                    Unidad = p.Unidad,
                    Cantidad = p.Cantidad,
                    EsTitulo = false,
                    BadgeText = badge,
                    Capitulo = currentCapitulo
                });
            }
        }

        var dialog = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaDialog(jumpItems, SelectedPartida?.Id)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedPartidaId))
        {
            var match = Partidas.FirstOrDefault(p => string.Equals(p.Id, dialog.SelectedPartidaId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectPartida(match);
            }
        }
    }

    private void OpenBatchReplace()
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Reemplazar materiales", session.ReadOnlyReason);
            return;
        }

        var result = MaterialBatchReplaceDialog.Execute(Application.Current?.MainWindow, session, priceBankStore, undo);
        if (result != null)
        {
            if (SelectedPartida != null)
            {
                var current = SelectedPartida;
                SelectPartida(current);
            }
            MarkDirty();
            SaveStatusText = $"Reemplazo completado: {result.PartidasActualizadas} partidas actualizadas (Δ {(result.DeltaTotalClp >= 0 ? "+ " : "")}$ {result.DeltaTotalClp:N0} CLP).";
        }
    }

    private void OpenFamilyBatchReplace()
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Cambio de familia", session.ReadOnlyReason);
            return;
        }

        var result = FamilyBatchReplaceDialog.Execute(Application.Current?.MainWindow, session, priceBankStore, undo);
        if (result != null)
        {
            if (SelectedPartida != null)
            {
                var current = SelectedPartida;
                SelectPartida(current);
            }
            MarkDirty();
            SaveStatusText = $"Cambio de familia completado: {result.ItemsReemplazados} materiales y {result.PartidasActualizadas} partidas actualizadas (Δ {(result.DeltaTotalClp >= 0 ? "+ " : "")}$ {result.DeltaTotalClp:N0} CLP).";
        }
    }

    public void ExecuteFillDownSector(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows, int sectorSlot)
        => CubicacionFillHelper.FillDownSector(this, sourceRow, targetRows, sectorSlot);

    public void ExecuteFillDownCantidad(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows)
        => CubicacionFillHelper.FillDownCantidad(this, sourceRow, targetRows);

    public void ExecuteFillRight(CubicacionRowVm row, int sourceSlot, IReadOnlyList<int> targetSlots)
        => CubicacionFillHelper.FillRight(this, row, sourceSlot, targetSlots);

    public void ExecuteRepeatMeasurement(CubicacionRowVm row, int targetSlot, string expression)
        => CubicacionFillHelper.RepeatMeasurement(this, row, targetSlot, expression);
}
