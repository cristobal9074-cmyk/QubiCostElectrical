using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    private CubicacionShortcutActionCoordinator shortcutActionCoordinator = null!;

    public ICommand OpenPrecioListaCommand => shortcutActionCoordinator.OpenPrecioListaCommand;
    public ICommand OpenScTomarCommand => shortcutActionCoordinator.OpenScTomarCommand;
    public ICommand OpenItemizadoCommand => shortcutActionCoordinator.OpenItemizadoCommand;
    public ICommand OpenQuickJumpCommand => shortcutActionCoordinator.OpenQuickJumpCommand;
    public ICommand OpenBatchReplaceCommand => shortcutActionCoordinator.OpenBatchReplaceCommand;
    public ICommand OpenFamilyBatchReplaceCommand => shortcutActionCoordinator.OpenFamilyBatchReplaceCommand;
    public ICommand AutocompletarUnidadYCantidadCommand => shortcutActionCoordinator.AutocompletarUnidadYCantidadCommand;
    public ICommand AutocompletarUnidadYCantidadTodasCommand => shortcutActionCoordinator.AutocompletarUnidadYCantidadTodasCommand;

    private void InitializeShortcutCommands()
    {
        shortcutActionCoordinator = new CubicacionShortcutActionCoordinator(
            this,
            session,
            priceBankStore,
            rowMutationService,
            loadingService,
            cubicacionPresenter,
            itemizadoPresenter,
            undo);
    }

    internal bool TryExecuteShortcut(string actionId) => shortcutActionCoordinator.TryExecuteShortcut(actionId);

    public Action? RequestFillDown
    {
        get => shortcutActionCoordinator.RequestFillDown;
        set => shortcutActionCoordinator.RequestFillDown = value;
    }

    public Action? RequestFillRight
    {
        get => shortcutActionCoordinator.RequestFillRight;
        set => shortcutActionCoordinator.RequestFillRight = value;
    }

    public Action? RequestRepeatMeasurement
    {
        get => shortcutActionCoordinator.RequestRepeatMeasurement;
        set => shortcutActionCoordinator.RequestRepeatMeasurement = value;
    }

    public event Action? RequestFocusMotivoNoCubicar
    {
        add => shortcutActionCoordinator.RequestFocusMotivoNoCubicar += value;
        remove => shortcutActionCoordinator.RequestFocusMotivoNoCubicar -= value;
    }

    internal void NavigatePartidaRelative(int delta) => shortcutActionCoordinator.NavigatePartidaRelative(delta);
    public void OpenQuickJumpPartida() => shortcutActionCoordinator.OpenQuickJumpPartida();
    public void ExecuteFillDownSector(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows, int sectorSlot) => shortcutActionCoordinator.ExecuteFillDownSector(sourceRow, targetRows, sectorSlot);
    public void ExecuteFillDownCantidad(CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows) => shortcutActionCoordinator.ExecuteFillDownCantidad(sourceRow, targetRows);
    public void ExecuteFillRight(CubicacionRowVm row, int sourceSlot, IReadOnlyList<int> targetSlots) => shortcutActionCoordinator.ExecuteFillRight(row, sourceSlot, targetSlots);
    public void ExecuteRepeatMeasurement(CubicacionRowVm row, int targetSlot, string expression) => shortcutActionCoordinator.ExecuteRepeatMeasurement(row, targetSlot, expression);
    public void AutocompletarUnidadYCantidadActual() => shortcutActionCoordinator.AutocompletarUnidadYCantidadActual();
    public void AutocompletarUnidadYCantidadTodas() => shortcutActionCoordinator.AutocompletarUnidadYCantidadTodas();
}
