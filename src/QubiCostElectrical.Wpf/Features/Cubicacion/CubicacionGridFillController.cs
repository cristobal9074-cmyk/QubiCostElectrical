using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Encapsula las operaciones de autollenado tipo Excel (FillDown Ctrl+D, FillRight Ctrl+R)
/// y repetición de medición (F4) sobre el DataGrid de cubicación.
/// </summary>
internal sealed class CubicacionGridFillController
{
    private string? _lastEnteredMeasurement;

    public void RecordMeasurement(string? value)
    {
        string clean = (value ?? "").Trim();
        if (!string.IsNullOrEmpty(clean))
            _lastEnteredMeasurement = clean;
    }

    public void HandleFillDown(
        CubicacionViewModel? vm,
        DataGrid resourceGrid,
        DataGridColumn? cantidadColumn,
        Func<DataGridColumn?, int> getSectorSlot)
    {
        if (vm == null || vm.IsReadOnly || vm.SelectedPartida == null)
            return;

        try
        {
            resourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            resourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CubicacionGridFillController] CommitEdit en FillDown: {ex.Message}");
        }

        var selectedCells = resourceGrid.SelectedCells.ToList();
        if (selectedCells.Count == 0 && resourceGrid.CurrentCell.IsValid)
        {
            selectedCells.Add(resourceGrid.CurrentCell);
        }

        if (selectedCells.Count == 0)
            return;

        var byColumn = selectedCells.GroupBy(c => c.Column).ToList();

        foreach (var colGroup in byColumn)
        {
            var col = colGroup.Key;
            var rowsInCol = colGroup
                .Select(c => c.Item as CubicacionRowVm)
                .Where(r => r != null)
                .Distinct()
                .OrderBy(r => resourceGrid.Items.IndexOf(r!))
                .ToList();

            if (rowsInCol.Count == 0)
                continue;

            CubicacionRowVm sourceRow;
            List<CubicacionRowVm> targetRows;

            int firstSelectedIdx = resourceGrid.Items.IndexOf(rowsInCol[0]!);
            if (rowsInCol.Count == 1)
            {
                if (firstSelectedIdx <= 0)
                    continue;
                sourceRow = (CubicacionRowVm)resourceGrid.Items[firstSelectedIdx - 1];
                targetRows = rowsInCol!;
            }
            else
            {
                sourceRow = rowsInCol[0]!;
                targetRows = rowsInCol.Skip(1).ToList()!;
            }

            int sectorSlot = getSectorSlot(col);
            if (sectorSlot >= 0 && sectorSlot < CubicacionViewModel.MaximumSectorCount)
            {
                vm.ExecuteFillDownSector(sourceRow, targetRows, sectorSlot);
            }
            else if (col == cantidadColumn)
            {
                vm.ExecuteFillDownCantidad(sourceRow, targetRows);
            }
        }
    }

    public void HandleFillRight(
        CubicacionViewModel? vm,
        DataGrid resourceGrid,
        Func<DataGridColumn?, int> getSectorSlot)
    {
        if (vm == null || vm.IsReadOnly || vm.SelectedPartida == null)
            return;

        try
        {
            resourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            resourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CubicacionGridFillController] CommitEdit en FillRight: {ex.Message}");
        }

        var selectedCells = resourceGrid.SelectedCells.ToList();
        if (selectedCells.Count == 0 && resourceGrid.CurrentCell.IsValid)
        {
            selectedCells.Add(resourceGrid.CurrentCell);
        }

        if (selectedCells.Count == 0)
            return;

        var byRow = selectedCells
            .GroupBy(c => c.Item as CubicacionRowVm)
            .Where(g => g.Key != null)
            .ToList();

        foreach (var rowGroup in byRow)
        {
            var row = rowGroup.Key!;
            var sectorCols = rowGroup
                .Select(c => (Column: c.Column, Slot: getSectorSlot(c.Column)))
                .Where(x => x.Slot >= 0 && x.Slot < CubicacionViewModel.MaximumSectorCount)
                .OrderBy(x => x.Slot)
                .ToList();

            if (sectorCols.Count == 0)
                continue;

            int sourceSlot;
            List<int> targetSlots;

            if (sectorCols.Count == 1)
            {
                sourceSlot = sectorCols[0].Slot;
                if (sourceSlot + 1 >= CubicacionViewModel.MaximumSectorCount)
                    continue;
                targetSlots = new List<int> { sourceSlot + 1 };
            }
            else
            {
                sourceSlot = sectorCols[0].Slot;
                targetSlots = sectorCols.Skip(1).Select(x => x.Slot).ToList();
            }

            vm.ExecuteFillRight(row, sourceSlot, targetSlots);
        }
    }

    public void HandleRepeatMeasurement(
        CubicacionViewModel? vm,
        DataGrid resourceGrid,
        DataGridColumn? cantidadColumn,
        Func<DataGridColumn?, int> getSectorSlot)
    {
        if (vm == null || vm.IsReadOnly || vm.SelectedPartida == null || string.IsNullOrWhiteSpace(_lastEnteredMeasurement))
            return;

        try
        {
            resourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            resourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CubicacionGridFillController] CommitEdit en RepeatMeasurement: {ex.Message}");
        }

        var currentCell = resourceGrid.CurrentCell;
        if (!currentCell.IsValid || currentCell.Item is not CubicacionRowVm row)
            return;

        int slot = getSectorSlot(currentCell.Column);
        if (slot >= 0 && slot < CubicacionViewModel.MaximumSectorCount)
        {
            vm.ExecuteRepeatMeasurement(row, slot, _lastEnteredMeasurement);
        }
        else if (currentCell.Column == cantidadColumn)
        {
            if (decimal.TryParse(_lastEnteredMeasurement, out decimal val))
            {
                vm.PushUndoPoint("Repetir medición (F4)");
                row.Cantidad = val;
                vm.OnRowEdited(row);
                vm.MarkDirty();
                vm.SaveStatusText = $"Cantidad repetida: '{val}' (F4).";
            }
        }
    }
}
