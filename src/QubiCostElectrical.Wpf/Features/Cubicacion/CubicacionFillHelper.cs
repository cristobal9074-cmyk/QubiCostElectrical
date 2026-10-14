using System.Collections.Generic;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

internal static class CubicacionFillHelper
{
    public static void FillDownSector(CubicacionViewModel vm, CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows, int sectorSlot)
    {
        if (vm.IsReadOnly || sourceRow == null || targetRows.Count == 0 || sectorSlot < 0 || sectorSlot >= CubicacionViewModel.MaximumSectorCount)
            return;

        vm.PushUndoPoint("Rellenar abajo (Ctrl+D)");
        string sourceExpr = sourceRow.SectorValues[sectorSlot];
        foreach (var target in targetRows)
        {
            target.SectorValues[sectorSlot] = sourceExpr;
            vm.ApplyBatchSectorExpressions(target, new HashSet<int> { sectorSlot });
        }
        vm.MarkDirty();
        vm.SaveStatusText = "Rellenado hacia abajo (Ctrl+D).";
    }

    public static void FillDownCantidad(CubicacionViewModel vm, CubicacionRowVm sourceRow, IReadOnlyList<CubicacionRowVm> targetRows)
    {
        if (vm.IsReadOnly || sourceRow == null || targetRows.Count == 0)
            return;

        vm.PushUndoPoint("Rellenar abajo (Ctrl+D)");
        decimal sourceCant = sourceRow.Cantidad;
        foreach (var target in targetRows)
        {
            target.Cantidad = sourceCant;
            vm.OnRowEdited(target);
        }
        vm.MarkDirty();
        vm.SaveStatusText = "Rellenado de cantidad hacia abajo (Ctrl+D).";
    }

    public static void FillRight(CubicacionViewModel vm, CubicacionRowVm row, int sourceSlot, IReadOnlyList<int> targetSlots)
    {
        if (vm.IsReadOnly || row == null || sourceSlot < 0 || sourceSlot >= CubicacionViewModel.MaximumSectorCount || targetSlots.Count == 0)
            return;

        vm.PushUndoPoint("Rellenar a la derecha (Ctrl+R)");
        string sourceExpr = row.SectorValues[sourceSlot];
        var slots = new HashSet<int>();
        foreach (int targetSlot in targetSlots)
        {
            if (targetSlot >= 0 && targetSlot < CubicacionViewModel.MaximumSectorCount)
            {
                row.SectorValues[targetSlot] = sourceExpr;
                slots.Add(targetSlot);
            }
        }
        vm.ApplyBatchSectorExpressions(row, slots);
        vm.MarkDirty();
        vm.SaveStatusText = "Rellenado hacia la derecha (Ctrl+R).";
    }

    public static void RepeatMeasurement(CubicacionViewModel vm, CubicacionRowVm row, int targetSlot, string expression)
    {
        if (vm.IsReadOnly || row == null || targetSlot < 0 || targetSlot >= CubicacionViewModel.MaximumSectorCount || string.IsNullOrWhiteSpace(expression))
            return;

        vm.PushUndoPoint("Repetir medición (F4)");
        row.SectorValues[targetSlot] = expression;
        vm.ApplyBatchSectorExpressions(row, new HashSet<int> { targetSlot });
        vm.MarkDirty();
        vm.SaveStatusText = $"Medición repetida (F4): '{expression}'";
    }
}
