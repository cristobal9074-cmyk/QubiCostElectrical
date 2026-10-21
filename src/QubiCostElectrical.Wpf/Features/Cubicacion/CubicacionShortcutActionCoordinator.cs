using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Coordina la ejecución de atajos de teclado, diálogos asociados (precio de lista,
/// resumen SC, itemizado, quick jump, reemplazos masivos) y operaciones masivas de celdas y mediciones.
/// </summary>
internal sealed class CubicacionShortcutActionCoordinator
{
    private readonly CubicacionViewModel _vm;
    private readonly BudgetProjectSession _session;
    private readonly IPriceBankStore _priceBankStore;
    private readonly CubicacionRowMutationService _rowMutationService;
    private readonly CubicacionLoadingService _loadingService;
    private readonly CubicacionPresenter _cubicacionPresenter;
    private readonly ItemizadoPresenter _itemizadoPresenter;
    private readonly WpfUndoService _undo;

    public ICommand OpenPrecioListaCommand { get; }
    public ICommand OpenScTomarCommand { get; }
    public ICommand OpenItemizadoCommand { get; }
    public ICommand OpenQuickJumpCommand { get; }
    public ICommand OpenBatchReplaceCommand { get; }
    public ICommand OpenFamilyBatchReplaceCommand { get; }
    public ICommand AutocompletarUnidadYCantidadCommand { get; }
    public ICommand AutocompletarUnidadYCantidadTodasCommand { get; }
    public ICommand AplicarCantidadALaPartidaCommand { get; }

    public Action? RequestFillDown { get; set; }
    public Action? RequestFillRight { get; set; }
    public Action? RequestRepeatMeasurement { get; set; }
    public event Action? RequestFocusMotivoNoCubicar;

    public CubicacionShortcutActionCoordinator(
        CubicacionViewModel vm,
        BudgetProjectSession session,
        IPriceBankStore priceBankStore,
        CubicacionRowMutationService rowMutationService,
        CubicacionLoadingService loadingService,
        CubicacionPresenter cubicacionPresenter,
        ItemizadoPresenter itemizadoPresenter,
        WpfUndoService undo)
    {
        _vm = vm;
        _session = session;
        _priceBankStore = priceBankStore;
        _rowMutationService = rowMutationService;
        _loadingService = loadingService;
        _cubicacionPresenter = cubicacionPresenter;
        _itemizadoPresenter = itemizadoPresenter;
        _undo = undo;

        OpenPrecioListaCommand = new RelayCommand(OpenPrecioLista);
        OpenScTomarCommand = new RelayCommand(OpenCubicacionScTomar, () => _vm.CanEdit);
        OpenItemizadoCommand = new RelayCommand(OpenCubicacionItemizado);
        OpenQuickJumpCommand = new RelayCommand(OpenQuickJumpPartida);
        OpenBatchReplaceCommand = new RelayCommand(OpenBatchReplace, () => _vm.CanEdit);
        OpenFamilyBatchReplaceCommand = new RelayCommand(OpenFamilyBatchReplace, () => _vm.CanEdit);
        AutocompletarUnidadYCantidadCommand = new RelayCommand(AutocompletarUnidadYCantidadActual, () => _vm.CanEdit && _vm.SelectedPartida?.EsPartida == true);
        AutocompletarUnidadYCantidadTodasCommand = new RelayCommand(AutocompletarUnidadYCantidadTodas, () => _vm.CanEdit);
        AplicarCantidadALaPartidaCommand = new RelayCommand(AplicarCantidadALaPartida, () => _vm.CanEdit && _vm.SelectedPartida?.EsPartida == true);
    }

    public bool TryExecuteShortcut(string actionId)
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
                if (_vm.AddPartidaCommand.CanExecute(null))
                {
                    _vm.AddPartidaCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.DeletePartida:
                if (_vm.DeletePartidaCommand.CanExecute(null))
                {
                    _vm.DeletePartidaCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.RowMoveUp:
                if (_vm.MoveRowUpCommand.CanExecute(null))
                {
                    _vm.MoveRowUpCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.RowMoveDown:
                if (_vm.MoveRowDownCommand.CanExecute(null))
                {
                    _vm.MoveRowDownCommand.Execute(null);
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
                if (_vm.AplicarConjuntoCommand.CanExecute(null))
                {
                    _vm.AplicarConjuntoCommand.Execute(null);
                    return true;
                }
                return false;
            case CubicacionShortcutCatalog.SectorMatrix:
                _vm.IsSectorMatrixMode = !_vm.IsSectorMatrixMode;
                return true;
            case CubicacionShortcutCatalog.ResourceDetail:
                _vm.IsSectorMatrixMode = false;
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

    private bool TryToggleExclusionFromShortcut()
    {
        if (_session.IsReadOnly || _vm.SelectedPartida?.EsPartida != true)
            return false;

        _vm.ExcluidoCubicacion = !_vm.ExcluidoCubicacion;
        return true;
    }

    private bool TryFocusObservacionFromShortcut()
    {
        if (_session.IsReadOnly || _vm.SelectedPartida?.EsPartida != true)
            return false;

        RequestFocusMotivoNoCubicar?.Invoke();
        return true;
    }

    public void NavigatePartidaRelative(int delta)
    {
        var list = _vm.FilteredPartidas.Count > 0 ? _vm.FilteredPartidas : _vm.Partidas;
        if (list.Count == 0) return;

        var current = _vm.SelectedPartida;
        var index = current != null ? list.IndexOf(current) : -1;
        if (index < 0 && current != null)
            index = list.ToList().FindIndex(p => string.Equals(p.Id, current.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = 0;
        else index = Math.Clamp(index + delta, 0, list.Count - 1);

        var target = list[index];
        if (target.EsPartida)
            _vm.SelectPartida(target);
        else
        {
            for (var step = delta >= 0 ? 1 : -1; index >= 0 && index < list.Count; index += step)
            {
                if (list[index].EsPartida)
                {
                    _vm.SelectPartida(list[index]);
                    break;
                }
            }
        }
    }

    private void OpenPrecioLista()
    {
        var codigo = "";
        if (_vm.SelectedRow != null && !string.IsNullOrWhiteSpace(_vm.SelectedRow.Codigo))
            codigo = _vm.SelectedRow.Codigo;
        else if (_vm.SelectedPartida != null && !string.IsNullOrWhiteSpace(_vm.SelectedPartida.Codigo))
            codigo = _vm.SelectedPartida.Codigo;

        if (!string.IsNullOrWhiteSpace(codigo) && _priceBankStore.GetByCodigo(codigo) == null) codigo = "";

        var dialog = new PrecioListaResumenDialog(_session, _priceBankStore, codigo) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        _rowMutationService.AplicarMaterialCreado(dialog.MaterialCreado);
    }

    private void OpenCubicacionScTomar()
    {
        var cuadros = _session.CubicacionSC;
        var config = _session.CubicacionScConfig;
        if (cuadros == null || cuadros.Count == 0 || config == null)
        {
            WpfDialogService.Info("Sin datos SC", "No hay datos de servicios comunes en el proyecto actual.");
            return;
        }

        var dialog = new ResumenScTomarDialog(cuadros, config, _session.ZonasProyecto) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result == null)
            return;

        var row = _vm.SelectedRow;
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

        var zonas = _session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        row.BeginBatchUpdate();
        for (var i = 0; i < material.ZonaQties.Count && i < zonas.Count; i++)
        {
            var zonaQty = material.ZonaQties[i];
            var zonaNombre = zonas[i].Nombre;
            if (!string.Equals(zonaQty.ZonaNombre, zonaNombre, StringComparison.OrdinalIgnoreCase))
                continue;

            if (decimal.TryParse(zonaQty.Text, out var qty) && qty > 0m)
                _vm.SetSectorExpressionDirect(row, i, qty.ToString("0.##"));
        }
        row.EndBatchUpdate();
        row.RefreshDisplayProperties();
        _vm.RightPanel.LoadRow(row);
        _vm.ValidateRow(row);
        _vm.MarkDirty();
        _vm.NotifyCubicacionSummaryChanged();
    }

    private void OpenCubicacionItemizado()
    {
        if (_vm.Partidas.Count == 0)
        {
            WpfDialogService.Info("Sin partidas", "No hay partidas en el proyecto actual.");
            return;
        }

        var costs = new Dictionary<string, PartidaCost>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _session.Cubicacion)
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

        var items = _vm.Partidas.Select(p =>
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

        _vm.SetSessionChangeActive(false);
        try
        {
            var dialog = new ResumenItemizadoDialog(items, _vm.SelectedPartida?.Id, _session, _itemizadoPresenter, _undo) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (dialog.HasMutations)
            {
                _session.MarkDirty(ProjectDirtyScope.Itemizado);
                _loadingService.LoadPartidas();
                _vm.NotifyCubicacionSummaryChanged();
            }

            var resultTarget = dialog.Result ?? (dialog.HasMutations ? dialog.SelectedRow : null);
            if (resultTarget == null) return;

            var targetId = resultTarget.Id;
            var targetPartida = _vm.Partidas.FirstOrDefault(p => string.Equals(p.Id, targetId, StringComparison.OrdinalIgnoreCase));
            if (targetPartida != null)
            {
                if (!_vm.FilteredPartidas.Contains(targetPartida)) _vm.IsPartidaFilterTodos = true;
                _vm.SelectPartida(targetPartida);
            }

            if (dialog.ShouldAddResource && _vm.AddRowCommand.CanExecute(null)) _vm.AddRowCommand.Execute(null);

            if (dialog.ShouldDeleteRows)
            {
                var rowIds = _session.Cubicacion.Where(r => string.Equals(r.ItemId, targetId, StringComparison.OrdinalIgnoreCase)).Select(r => r.Id).ToList();
                if (rowIds.Count > 0) _cubicacionPresenter.DeleteRows(rowIds);
            }
        }
        finally { _vm.SetSessionChangeActive(true); }
    }

    public void OpenQuickJumpPartida()
    {
        if (_vm.Partidas.Count == 0) return;

        string currentCapitulo = "";
        var jumpItems = new List<QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem>(_vm.Partidas.Count);

        foreach (var p in _vm.Partidas)
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

        var dialog = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaDialog(jumpItems, _vm.SelectedPartida?.Id)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedPartidaId))
        {
            var match = _vm.Partidas.FirstOrDefault(p => string.Equals(p.Id, dialog.SelectedPartidaId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _vm.SelectPartida(match);
            }
        }
    }

    private void OpenBatchReplace()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Reemplazar materiales", _session.ReadOnlyReason);
            return;
        }

        var result = MaterialBatchReplaceDialog.Execute(Application.Current?.MainWindow, _session, _priceBankStore, _undo);
        if (result != null)
        {
            if (_vm.SelectedPartida != null)
            {
                var current = _vm.SelectedPartida;
                _vm.SelectPartida(current);
            }
            _vm.MarkDirty();
            _vm.SaveStatusText = $"Reemplazo completado: {result.PartidasActualizadas} partidas actualizadas (Δ {(result.DeltaTotalClp >= 0 ? "+ " : "")}$ {result.DeltaTotalClp:N0} CLP).";
        }
    }

    private void OpenFamilyBatchReplace()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Cambio de familia", _session.ReadOnlyReason);
            return;
        }

        var result = FamilyBatchReplaceDialog.Execute(Application.Current?.MainWindow, _session, _priceBankStore, _undo);
        if (result != null)
        {
            if (_vm.SelectedPartida != null)
            {
                var current = _vm.SelectedPartida;
                _vm.SelectPartida(current);
            }
            _vm.MarkDirty();
            _vm.SaveStatusText = $"Cambio de familia completado: {result.ItemsReemplazados} materiales y {result.PartidasActualizadas} partidas actualizadas (Δ {(result.DeltaTotalClp >= 0 ? "+ " : "")}$ {result.DeltaTotalClp:N0} CLP).";
        }
    }

    public void ExecuteFillDownSector(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows, int sectorSlot)
        => CubicacionFillHelper.FillDownSector(_vm, sourceRow, targetRows, sectorSlot);

    public void ExecuteFillDownCantidad(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows)
        => CubicacionFillHelper.FillDownCantidad(_vm, sourceRow, targetRows);

    public void ExecuteFillRight(CubicacionRowVm row, int sourceSlot, IReadOnlyList<int> targetSlots)
        => CubicacionFillHelper.FillRight(_vm, row, sourceSlot, targetSlots);

    public void ExecuteRepeatMeasurement(CubicacionRowVm row, int targetSlot, string expression)
        => CubicacionFillHelper.RepeatMeasurement(_vm, row, targetSlot, expression);

    public void AplicarCantidadALaPartida()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Aplicar cantidad a la partida", _session.ReadOnlyReason);
            return;
        }

        if (_vm.SelectedPartida == null || !_vm.SelectedPartida.EsPartida)
        {
            WpfDialogService.Info("Aplicar cantidad a la partida", "Seleccione una partida.");
            return;
        }

        decimal cantidad = _vm.SelectedPartida.Cantidad;
        bool applied = CubicacionPartidaHelper.SincronizarMaestroDesdePartida(_vm.Rows, cantidad, _session, _vm.ValidateRow);
        if (!applied)
        {
            _vm.SaveStatusText = "La cantidad global queda en la partida. Las cantidades de los materiales no cambian.";
            return;
        }

        _vm.MarkDirty();
        _vm.ScheduleRefreshTotals();
        _vm.SaveStatusText = $"Cantidad {cantidad:G29} aplicada al recurso maestro. El resto de los materiales no cambió.";
    }

    public void AutocompletarUnidadYCantidadActual()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Autocompletar unidad y cantidad", _session.ReadOnlyReason);
            return;
        }

        if (_vm.SelectedPartida == null || !_vm.SelectedPartida.EsPartida)
        {
            WpfDialogService.Info("Autocompletar unidad y cantidad", "Seleccione una partida para autocompletar su unidad y cantidad.");
            return;
        }

        var firstRow = _vm.Rows.FirstOrDefault(r => r.HasContent && !string.IsNullOrWhiteSpace(r.Codigo))
                       ?? _vm.Rows.FirstOrDefault(r => r.HasContent)
                       ?? _vm.Rows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Unidad) || r.Cantidad > 0m);

        if (firstRow == null)
        {
            WpfDialogService.Info("Autocompletar unidad y cantidad",
                $"La partida '{_vm.SelectedPartida.DisplayLabel}' no tiene materiales cubicados en su tabla.");
            return;
        }

        string unidad = !string.IsNullOrWhiteSpace(firstRow.Unidad)
            ? firstRow.Unidad.Trim()
            : (_vm.Rows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Unidad))?.Unidad?.Trim() ?? "");

        decimal cantidad = firstRow.Cantidad != 0m
            ? firstRow.Cantidad
            : (_vm.Rows.FirstOrDefault(r => r.Cantidad > 0m)?.Cantidad ?? 0m);

        if (string.IsNullOrWhiteSpace(unidad) && cantidad <= 0m)
        {
            WpfDialogService.Info("Autocompletar unidad y cantidad",
                "El primer material de la partida no tiene definida unidad ni cantidad para autocompletar.");
            return;
        }

        using (_vm.SuppressSessionReload())
        {
            _undo.PushUndoPoint(_session, $"Autocompletar unidad y cantidad en partida {_vm.SelectedPartida.Codigo}");

            var item = _session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, _vm.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                if (!string.IsNullOrWhiteSpace(unidad)) item.Unidad = unidad;
                if (cantidad > 0m) item.Cantidad = cantidad;
            }

            if (!string.IsNullOrWhiteSpace(unidad))
            {
                _vm.SelectedPartida.Unidad = unidad;
            }

            if (cantidad > 0m)
            {
                _vm.SelectedPartida.Cantidad = cantidad;
                _vm.SelectedPartida.CantidadFormula = "";
            }

            _vm.NotifyProperty(nameof(_vm.PartidaUnidadText));
            _vm.NotifyProperty(nameof(_vm.PartidaUnidad));
            _vm.NotifyProperty(nameof(_vm.PartidaCantidadText));
            _vm.NotifyProperty(nameof(_vm.PartidaCantidadToolTip));
            _vm.NotifyGlobalValorizacionChanged();
            _vm.UpdateFilterCounts();
            _vm.MarkDirty();
            _session.MarkDirty(ProjectDirtyScope.Itemizado);
            _vm.ScheduleRefreshTotals();

            _vm.SaveStatusText = $"Autocompletado: UD = '{_vm.SelectedPartida.Unidad}', CANT = {_vm.SelectedPartida.Cantidad:G29}";
        }
    }

    public void AutocompletarUnidadYCantidadTodas()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Autocompletar unidad y cantidad", _session.ReadOnlyReason);
            return;
        }

        var candidatePartidas = _vm.Partidas
            .Where(p => p.EsPartida && !p.ExcluidoCubicacion && p.TieneCubicacion && (string.IsNullOrWhiteSpace(p.Unidad) || p.Cantidad <= 0m))
            .ToList();

        if (candidatePartidas.Count == 0)
        {
            WpfDialogService.Info("Autocompletar unidad y cantidad",
                "No hay partidas pendientes de valorización global con materiales cubicados en el proyecto.");
            return;
        }

        bool ok = WpfDialogService.Confirm(
            "Autocompletar todo el proyecto",
            $"Se encontraron {candidatePartidas.Count} partida(s) con cubicación que no tienen unidad o cantidad global definida.\n\n¿Desea autocompletar la unidad y cantidad del primer material en todas estas partidas?",
            isWarning: false);

        if (!ok) return;

        using (_vm.SuppressSessionReload())
        {
            _undo.PushUndoPoint(_session, $"Autocompletar unidad y cantidad en {candidatePartidas.Count} partidas");
            int actualizadas = 0;

            foreach (var partidaVm in candidatePartidas)
            {
                var item = _session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, partidaVm.Id, StringComparison.OrdinalIgnoreCase));
                var cubRows = _session.Cubicacion
                    .Where(r => string.Equals(r.ItemId, partidaVm.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var firstRow = cubRows.FirstOrDefault(r => (!string.IsNullOrWhiteSpace(r.Codigo) || r.Cantidad != 0m) && !string.IsNullOrWhiteSpace(r.Codigo))
                               ?? cubRows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Codigo) || r.Cantidad != 0m)
                               ?? cubRows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Unidad) || r.Cantidad > 0m);

                if (firstRow == null) continue;

                string unidad = !string.IsNullOrWhiteSpace(firstRow.Unidad)
                    ? firstRow.Unidad.Trim()
                    : (cubRows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Unidad))?.Unidad?.Trim() ?? "");

                decimal cantidad = firstRow.Cantidad != 0m
                    ? firstRow.Cantidad
                    : (cubRows.FirstOrDefault(r => r.Cantidad > 0m)?.Cantidad ?? 0m);

                bool updated = false;
                if ((string.IsNullOrWhiteSpace(partidaVm.Unidad) || string.Equals(partidaVm.Unidad, "GL", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(unidad))
                {
                    if (item != null) item.Unidad = unidad;
                    partidaVm.Unidad = unidad;
                    updated = true;
                }
                if (partidaVm.Cantidad <= 0m && cantidad > 0m)
                {
                    if (item != null) item.Cantidad = cantidad;
                    partidaVm.Cantidad = cantidad;
                    partidaVm.CantidadFormula = "";
                    updated = true;
                }

                if (updated)
                {
                    actualizadas++;
                    if (_vm.SelectedPartida != null && string.Equals(_vm.SelectedPartida.Id, partidaVm.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _vm.NotifyProperty(nameof(_vm.PartidaUnidadText));
                        _vm.NotifyProperty(nameof(_vm.PartidaUnidad));
                        _vm.NotifyProperty(nameof(_vm.PartidaCantidadText));
                        _vm.NotifyProperty(nameof(_vm.PartidaCantidadToolTip));
                        _vm.NotifyGlobalValorizacionChanged();
                    }
                }
            }

            _vm.UpdateFilterCounts();
            _vm.MarkDirty();
            _session.MarkDirty(ProjectDirtyScope.Itemizado);
            _vm.ScheduleRefreshTotals();

            _vm.SaveStatusText = $"Se autocompletaron {actualizadas} partidas.";
            WpfDialogService.Info("Autocompletar unidad y cantidad",
                $"Se autocompletaron exitosamente {actualizadas} partida(s) en el proyecto.");
        }
    }
}
