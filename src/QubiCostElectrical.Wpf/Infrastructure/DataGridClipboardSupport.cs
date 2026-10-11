using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace QubiCostElectrical.Wpf.Infrastructure;

internal static class DataGridClipboardSupport
{
    private static readonly ConditionalWeakTable<DataGrid, CopyMarkerState> _copyMarkers = new();

    private sealed class CopyMarkerState
    {
        public CopyMarkerAdorner? Adorner;
        public List<DataGridCellInfo> Cells = new();
        public bool LayoutHooked;
    }

    public static void ShowCopyMarker(DataGrid grid, List<DataGridCellInfo> cells)
    {
        var state = _copyMarkers.GetOrCreateValue(grid);
        state.Cells = cells;

        var layer = AdornerLayer.GetAdornerLayer(grid);
        if (layer == null)
            return;

        if (state.Adorner == null)
        {
            state.Adorner = new CopyMarkerAdorner(grid);
            layer.Add(state.Adorner);
        }

        if (!state.LayoutHooked)
        {
            state.LayoutHooked = true;
            grid.LayoutUpdated += (_, _) => RepositionCopyMarker(grid);
        }

        RepositionCopyMarker(grid);
    }

    public static void ClearCopyMarker(DataGrid grid)
    {
        if (!_copyMarkers.TryGetValue(grid, out var state))
            return;

        state.Cells.Clear();
        if (state.Adorner != null)
        {
            state.Adorner.Stop();
            var layer = AdornerLayer.GetAdornerLayer(grid);
            layer?.Remove(state.Adorner);
            state.Adorner = null;
        }
    }

    private static void RepositionCopyMarker(DataGrid grid)
    {
        if (!_copyMarkers.TryGetValue(grid, out var state) || state.Adorner == null || state.Cells.Count == 0)
            return;

        Rect? bounds = null;

        // Agrupar por fila para invocar ContainerFromItem y FindVisualChild una sola vez por fila visible
        var cellsByItem = state.Cells.GroupBy(c => c.Item);
        foreach (var rowGroup in cellsByItem)
        {
            var row = grid.ItemContainerGenerator.ContainerFromItem(rowGroup.Key) as DataGridRow;
            if (row == null)
                continue;

            var presenter = DataGridExcelNavigation.FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
                continue;

            foreach (var cellInfo in rowGroup)
            {
                int colIndex = grid.Columns.IndexOf(cellInfo.Column);
                if (colIndex < 0)
                    continue;

                if (presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) is not DataGridCell cell)
                    continue;

                var topLeft = cell.TransformToAncestor(grid).Transform(new Point(0, 0));
                var cellRect = new Rect(topLeft, new Size(cell.ActualWidth, cell.ActualHeight));
                bounds = bounds == null ? cellRect : Rect.Union(bounds.Value, cellRect);
            }
        }

        // Si ninguna celda copiada está actualmente renderizada (se scrolleó fuera de vista),
        // no se dibuja nada — reaparece solo al volver a scrollear a esa zona, igual que Excel.
        state.Adorner.UpdateRect(bounds ?? Rect.Empty);
    }

    public static bool TryPasteIntoSelection(DataGrid grid, string clipboardText, Func<DataGridColumn, bool> isSpecialColumn)
    {
        var anchor = grid.CurrentCell;
        if (!anchor.IsValid || anchor.Column == null || isSpecialColumn(anchor.Column))
            return false;

        var selectedCells = grid.SelectedCells
            .Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly && !isSpecialColumn(c.Column))
            .ToList();

        var lines = clipboardText.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
        var grid2D = lines.Select(l => l.Split('\t')).ToArray();

        var visibleColumns = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly && !isSpecialColumn(c))
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        // Mapear items y columnas a índices para resolución O(1)
        var indexByItem = new Dictionary<object, int>(grid.Items.Count);
        for (int i = 0; i < grid.Items.Count; i++)
        {
            var it = grid.Items[i];
            if (it != null && !indexByItem.ContainsKey(it))
                indexByItem[it] = i;
        }

        var indexByCol = new Dictionary<DataGridColumn, int>(visibleColumns.Count);
        for (int i = 0; i < visibleColumns.Count; i++)
        {
            indexByCol[visibleColumns[i]] = i;
        }

        if (selectedCells.Count > 1)
        {
            var selectedPairs = selectedCells
                .Select(c => (
                    Row: (c.Item != null && indexByItem.TryGetValue(c.Item, out int rIdx)) ? rIdx : -1,
                    Col: (c.Column != null && indexByCol.TryGetValue(c.Column, out int cIdx)) ? cIdx : -1))
                .Where(p => p.Row >= 0 && p.Col >= 0)
                .ToList();

            if (selectedPairs.Count > 1)
            {
                int rowMin = selectedPairs.Min(p => p.Row);
                int colMin = selectedPairs.Min(p => p.Col);

                var affectedItems = selectedPairs.Select(p => grid.Items[p.Row]).Where(it => it != null).Distinct().ToList();
                foreach (var item in affectedItems)
                    DataGridExcelNavigation.BeginBatchUpdate(item);

                try
                {
                    foreach (var (row, col) in selectedPairs)
                    {
                        int sourceRow = (row - rowMin) % grid2D.Length;
                        var sourceLine = grid2D[sourceRow];
                        if (sourceLine.Length == 0)
                            continue;
                        int sourceCol = (col - colMin) % sourceLine.Length;
                        DataGridExcelNavigation.SetCellText(grid.Items[row], visibleColumns[col], sourceLine[sourceCol].Trim());
                    }
                }
                finally
                {
                    foreach (var item in affectedItems)
                        DataGridExcelNavigation.EndBatchUpdate(item);
                }

                return true;
            }
        }

        int anchorColIndex = anchor.Column != null && indexByCol.TryGetValue(anchor.Column, out int acIdx) ? acIdx : -1;
        int anchorRowIndex = anchor.Item != null && indexByItem.TryGetValue(anchor.Item, out int arIdx) ? arIdx : -1;
        if (anchorColIndex < 0 || anchorRowIndex < 0)
            return false;

        var targetItems = new List<object>();
        for (int r = 0; r < grid2D.Length; r++)
        {
            int targetRow = anchorRowIndex + r;
            if (targetRow >= grid.Items.Count)
                break;
            var it = grid.Items[targetRow];
            if (it != null)
                targetItems.Add(it);
        }

        foreach (var it in targetItems)
            DataGridExcelNavigation.BeginBatchUpdate(it);

        try
        {
            for (int r = 0; r < grid2D.Length; r++)
            {
                int targetRow = anchorRowIndex + r;
                if (targetRow >= grid.Items.Count)
                    break;

                var item = grid.Items[targetRow];
                for (int c = 0; c < grid2D[r].Length; c++)
                {
                    int targetCol = anchorColIndex + c;
                    if (targetCol >= visibleColumns.Count)
                        break;

                    DataGridExcelNavigation.SetCellText(item, visibleColumns[targetCol], grid2D[r][c].Trim());
                }
            }
        }
        finally
        {
            foreach (var it in targetItems)
                DataGridExcelNavigation.EndBatchUpdate(it);
        }

        return true;
    }
}
