using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static partial class DataGridExcelNavigation
{
    // Ancla de selección por grilla — necesaria para que Shift+Flecha extienda un rectángulo
    // desde un punto fijo (como Excel), en vez de mover solo la celda activa.
    private static readonly ConditionalWeakTable<DataGrid, AnchorBox> _anchors = new();

    private sealed class AnchorBox { public DataGridCellInfo Anchor; }

    // Alcance de columnas seleccionables por grilla (opcional) — usado por Cubicación para que
    // "seleccionar todo"/extender con Shift DESDE una casilla de sector no arrastre también
    // Código/Descripción/Unidad/Cantidad (datos del material, no cantidades a operar). Solo se
    // aplica si la celda ACTUAL ya está dentro del alcance; si el usuario está parado en una
    // columna fuera de él (ej. Código), la selección sigue siendo libre como siempre.
    private static readonly ConditionalWeakTable<DataGrid, ColumnScopeBox> _columnScopes = new();

    private sealed class ColumnScopeBox { public Func<DataGridColumn, bool>? Predicate; }

    public static void ResetAnchor(DataGrid grid)
    {
        _anchors.Remove(grid);
        if (grid.CurrentCell.IsValid)
            _anchors.Add(grid, new AnchorBox { Anchor = grid.CurrentCell });
    }

    public static bool IsExtendingSelection(DataGrid grid)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            return true;
        if (_anchors.TryGetValue(grid, out var anchor) && anchor.Anchor.IsValid && grid.CurrentCell.IsValid)
        {
            if (anchor.Anchor.Item != grid.CurrentCell.Item || anchor.Anchor.Column != grid.CurrentCell.Column)
                return grid.SelectedCells.Count > 1;
        }
        return false;
    }
}
