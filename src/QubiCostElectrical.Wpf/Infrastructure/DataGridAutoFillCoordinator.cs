using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Controlador integral de Auto-Relleno estilo Microsoft Excel (Fill Handle) para DataGrid.
/// Proporciona:
/// 1. Cuadrito verde de relleno estético y nítido (Fill Handle) en la esquina inferior derecha.
/// 2. Arrastre bidireccional fluido: a la derecha (incremento) y a la izquierda (decremento/continuación).
/// 3. Arrastre vertical: hacia abajo y hacia arriba.
/// 4. Badge flotante tipo Excel junto al cursor que muestra en tiempo real el valor resultante (ej. "207").
/// 5. Atajos rápidos Ctrl+R y Ctrl+D, y doble clic en el controlador.
/// </summary>
public sealed class DataGridAutoFillCoordinator
{
    private readonly DataGrid _grid;
    private readonly Action? _onFilled;
    private DataGridAutoFillAdorner? _adorner;

    private bool _isDragging;
    private Point _dragStartPoint;
    private List<object> _sourceRows = new();
    private List<DataGridColumn> _sourceCols = new();
    private int _minRowIndex;
    private int _maxRowIndex;
    private int _minColIndex;
    private int _maxColIndex;

    private bool _isDraggingHorizontal;
    private int _targetHoverRowIndex = -1;
    private int _targetHoverColIndex = -1;

    public static DataGridAutoFillCoordinator Attach(DataGrid grid, Action? onFilled = null)
    {
        return new DataGridAutoFillCoordinator(grid, onFilled);
    }

    private DataGridAutoFillCoordinator(DataGrid grid, Action? onFilled)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _onFilled = onFilled;

        _grid.SelectedCellsChanged += OnSelectedCellsChanged;
        _grid.LayoutUpdated += OnLayoutUpdated;
        _grid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        _grid.PreviewMouseMove += OnPreviewMouseMove;
        _grid.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        _grid.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void EnsureAdorner()
    {
        if (_adorner != null) return;
        var layer = AdornerLayer.GetAdornerLayer(_grid);
        if (layer == null) return;

        _adorner = new DataGridAutoFillAdorner(_grid);
        layer.Add(_adorner);
    }

    private void OnSelectedCellsChanged(object? sender, SelectedCellsChangedEventArgs e)
    {
        if (!_isDragging)
            UpdateAdorner();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_isDragging)
            UpdateAdorner();
    }

    public void UpdateAdorner()
    {
        if (DataGridExcelNavigation.IsEditing(_grid))
        {
            if (_adorner != null) _adorner.SelectionBounds = Rect.Empty;
            return;
        }

        var validCells = _grid.SelectedCells
            .Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly)
            .ToList();

        if (validCells.Count == 0)
        {
            if (_adorner != null) _adorner.SelectionBounds = Rect.Empty;
            return;
        }

        EnsureAdorner();
        if (_adorner == null) return;

        var bounds = ComputeCellsBounds(_grid, validCells);
        if (bounds == null || bounds.Value.IsEmpty)
        {
            _adorner.SelectionBounds = Rect.Empty;
        }
        else
        {
            _adorner.SelectionBounds = bounds.Value;
            _adorner.ShowHandle = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(_grid);

        if (_isDragging)
        {
            UpdateDragGhost(p);
            Mouse.OverrideCursor = Cursors.Cross;
            e.Handled = true;
            return;
        }

        if (_adorner != null && !_adorner.HandleRect.IsEmpty && !DataGridExcelNavigation.IsEditing(_grid))
        {
            var h = _adorner.HandleRect;
            // Zona de interacción ergonómica alrededor del cuadrito (15x15 px)
            var hitBox = new Rect(h.X - 5, h.Y - 5, h.Width + 10, h.Height + 10);
            if (hitBox.Contains(p))
            {
                Mouse.OverrideCursor = Cursors.Cross;
                return;
            }
        }

        if (Mouse.OverrideCursor == Cursors.Cross)
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataGridExcelNavigation.IsEditing(_grid)) return;
        if (_adorner == null || _adorner.HandleRect.IsEmpty) return;

        var p = e.GetPosition(_grid);
        var h = _adorner.HandleRect;
        var hitBox = new Rect(h.X - 5, h.Y - 5, h.Width + 10, h.Height + 10);

        if (hitBox.Contains(p))
        {
            if (e.ClickCount == 2)
            {
                ExecuteDoubleClickFillDown();
                e.Handled = true;
                return;
            }

            if (CaptureSourceSelection())
            {
                _isDragging = true;
                _dragStartPoint = p;
                _grid.CaptureMouse();
                Mouse.OverrideCursor = Cursors.Cross;
                e.Handled = true;
            }
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        _grid.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;

        if (_adorner != null)
        {
            _adorner.DragGhostBounds = Rect.Empty;
            _adorner.PreviewTooltipText = null;
        }

        e.Handled = true;

        ApplyAutoFill();
        UpdateAdorner();
        _onFilled?.Invoke();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataGridExcelNavigation.IsEditing(_grid)) return;

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.R or Key.D)
        {
            if (ExecuteFillShortcut(vertical: e.Key == Key.D))
            {
                e.Handled = true;
                UpdateAdorner();
                _onFilled?.Invoke();
            }
        }
    }

    private bool CaptureSourceSelection()
    {
        var validCells = _grid.SelectedCells
            .Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly)
            .ToList();

        if (validCells.Count == 0) return false;

        var itemsSet = validCells.Select(c => c.Item).Distinct().ToHashSet();
        var colsSet = validCells.Select(c => c.Column).Distinct().ToHashSet();

        _sourceRows = _grid.Items.Cast<object>().Where(itemsSet.Contains).ToList();
        _sourceCols = _grid.Columns.Where(colsSet.Contains).OrderBy(c => c.DisplayIndex).ToList();

        if (_sourceRows.Count == 0 || _sourceCols.Count == 0) return false;

        _minRowIndex = _grid.Items.IndexOf(_sourceRows[0]);
        _maxRowIndex = _grid.Items.IndexOf(_sourceRows[^1]);
        _minColIndex = _sourceCols[0].DisplayIndex;
        _maxColIndex = _sourceCols[^1].DisplayIndex;

        return true;
    }

    private void UpdateDragGhost(Point p)
    {
        if (_adorner == null) return;

        var (hoverRow, hoverCol) = FindCellAtPoint(p);
        if (hoverRow < 0 && hoverCol == null)
            return;

        int targetRow = hoverRow >= 0 ? hoverRow : _maxRowIndex;
        int targetCol = hoverCol != null ? hoverCol.DisplayIndex : _maxColIndex;

        double dx = p.X - _dragStartPoint.X;
        double dy = p.Y - _dragStartPoint.Y;

        // Decisión de eje horizontal vs vertical
        bool preferHorizontal = Math.Abs(dx) >= Math.Abs(dy) || (_sourceRows.Count == 1 && Math.Abs(dx) > 10);
        if (_sourceCols.Count == 1 && Math.Abs(dy) > 15 && Math.Abs(dy) > Math.Abs(dx))
            preferHorizontal = false;

        _isDraggingHorizontal = preferHorizontal;
        _targetHoverRowIndex = targetRow;
        _targetHoverColIndex = targetCol;

        Rect? ghost = null;
        string? tooltipText = null;

        if (preferHorizontal)
        {
            if (targetCol > _maxColIndex)
            {
                // Arrastre hacia la derecha (incrementar)
                var targetCols = _grid.Columns
                    .Where(c => c.DisplayIndex > _maxColIndex && c.DisplayIndex <= targetCol && !c.IsReadOnly)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                if (targetCols.Count > 0)
                {
                    ghost = ComputeGridCellRangeBounds(_sourceRows, targetCols);
                    var sampleSource = _sourceCols.Select(c => DataGridCellAccessor.GetCellText(_sourceRows[0], c)).ToList();
                    var previewVals = AutoFillSeriesGenerator.GenerateNextValues(sampleSource, targetCols.Count, isHorizontal: true, isBackward: false);
                    tooltipText = previewVals.Count > 0 ? previewVals[^1] : null;
                }
            }
            else if (targetCol < _minColIndex)
            {
                // Arrastre hacia la izquierda (decrementar / continuar hacia atrás)
                var targetCols = _grid.Columns
                    .Where(c => c.DisplayIndex >= targetCol && c.DisplayIndex < _minColIndex && !c.IsReadOnly)
                    .OrderByDescending(c => c.DisplayIndex)
                    .ToList();

                if (targetCols.Count > 0)
                {
                    ghost = ComputeGridCellRangeBounds(_sourceRows, targetCols);
                    var sampleSource = _sourceCols.Select(c => DataGridCellAccessor.GetCellText(_sourceRows[0], c)).ToList();
                    var previewVals = AutoFillSeriesGenerator.GenerateNextValues(sampleSource, targetCols.Count, isHorizontal: true, isBackward: true);
                    tooltipText = previewVals.Count > 0 ? previewVals[^1] : null;
                }
            }
        }
        else
        {
            if (targetRow > _maxRowIndex)
            {
                // Arrastre hacia abajo
                int start = _maxRowIndex + 1;
                int end = Math.Min(_grid.Items.Count - 1, targetRow);
                var targetRows = new List<object>();
                for (int r = start; r <= end; r++) targetRows.Add(_grid.Items[r]);

                if (targetRows.Count > 0)
                {
                    ghost = ComputeGridCellRangeBounds(targetRows, _sourceCols);
                    var sampleSource = _sourceRows.Select(r => DataGridCellAccessor.GetCellText(r, _sourceCols[0])).ToList();
                    var previewVals = AutoFillSeriesGenerator.GenerateNextValues(sampleSource, targetRows.Count, isHorizontal: false, isBackward: false);
                    tooltipText = previewVals.Count > 0 ? previewVals[^1] : null;
                }
            }
            else if (targetRow < _minRowIndex)
            {
                // Arrastre hacia arriba
                int start = Math.Max(0, targetRow);
                int end = _minRowIndex - 1;
                var targetRows = new List<object>();
                for (int r = start; r <= end; r++) targetRows.Add(_grid.Items[r]);

                if (targetRows.Count > 0)
                {
                    ghost = ComputeGridCellRangeBounds(targetRows, _sourceCols);
                    var sampleSource = _sourceRows.Select(r => DataGridCellAccessor.GetCellText(r, _sourceCols[0])).ToList();
                    var previewVals = AutoFillSeriesGenerator.GenerateNextValues(sampleSource, targetRows.Count, isHorizontal: false, isBackward: true);
                    tooltipText = previewVals.Count > 0 ? previewVals[^1] : null;
                }
            }
        }

        _adorner.DragGhostBounds = ghost ?? Rect.Empty;
        _adorner.PreviewTooltipText = tooltipText;
        _adorner.TooltipPosition = p;
    }

    private void ApplyAutoFill()
    {
        if (_sourceRows.Count == 0 || _sourceCols.Count == 0) return;

        if (_isDraggingHorizontal)
        {
            if (_targetHoverColIndex > _maxColIndex)
            {
                var targetCols = _grid.Columns
                    .Where(c => c.DisplayIndex > _maxColIndex && c.DisplayIndex <= _targetHoverColIndex && !c.IsReadOnly)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                if (targetCols.Count > 0)
                {
                    foreach (var rowItem in _sourceRows)
                    {
                        var sourceValues = _sourceCols.Select(c => DataGridCellAccessor.GetCellText(rowItem, c)).ToList();
                        var newValues = AutoFillSeriesGenerator.GenerateNextValues(sourceValues, targetCols.Count, isHorizontal: true, isBackward: false);
                        for (int i = 0; i < targetCols.Count; i++)
                        {
                            DataGridCellAccessor.SetCellText(rowItem, targetCols[i], newValues[i]);
                        }
                    }

                    SelectCombinedRange(_sourceRows, _sourceCols.Concat(targetCols));
                }
            }
            else if (_targetHoverColIndex < _minColIndex && _targetHoverColIndex >= 0)
            {
                var targetCols = _grid.Columns
                    .Where(c => c.DisplayIndex >= _targetHoverColIndex && c.DisplayIndex < _minColIndex && !c.IsReadOnly)
                    .OrderByDescending(c => c.DisplayIndex)
                    .ToList();

                if (targetCols.Count > 0)
                {
                    foreach (var rowItem in _sourceRows)
                    {
                        var sourceValues = _sourceCols.Select(c => DataGridCellAccessor.GetCellText(rowItem, c)).ToList();
                        var newValues = AutoFillSeriesGenerator.GenerateNextValues(sourceValues, targetCols.Count, isHorizontal: true, isBackward: true);
                        for (int i = 0; i < targetCols.Count; i++)
                        {
                            DataGridCellAccessor.SetCellText(rowItem, targetCols[i], newValues[i]);
                        }
                    }

                    SelectCombinedRange(_sourceRows, targetCols.Concat(_sourceCols));
                }
            }
        }
        else
        {
            if (_targetHoverRowIndex > _maxRowIndex)
            {
                int start = _maxRowIndex + 1;
                int end = Math.Min(_grid.Items.Count - 1, _targetHoverRowIndex);
                var targetRows = new List<object>();
                for (int r = start; r <= end; r++) targetRows.Add(_grid.Items[r]);

                if (targetRows.Count > 0)
                {
                    foreach (var col in _sourceCols)
                    {
                        var sourceValues = _sourceRows.Select(r => DataGridCellAccessor.GetCellText(r, col)).ToList();
                        var newValues = AutoFillSeriesGenerator.GenerateNextValues(sourceValues, targetRows.Count, isHorizontal: false, isBackward: false);
                        for (int i = 0; i < targetRows.Count; i++)
                        {
                            DataGridCellAccessor.SetCellText(targetRows[i], col, newValues[i]);
                        }
                    }

                    SelectCombinedRange(_sourceRows.Concat(targetRows), _sourceCols);
                }
            }
            else if (_targetHoverRowIndex < _minRowIndex && _targetHoverRowIndex >= 0)
            {
                int start = _minRowIndex - 1;
                int end = Math.Max(0, _targetHoverRowIndex);
                var targetRows = new List<object>();
                for (int r = start; r >= end; r--) targetRows.Add(_grid.Items[r]);

                if (targetRows.Count > 0)
                {
                    foreach (var col in _sourceCols)
                    {
                        var sourceValues = _sourceRows.Select(r => DataGridCellAccessor.GetCellText(r, col)).ToList();
                        var newValues = AutoFillSeriesGenerator.GenerateNextValues(sourceValues, targetRows.Count, isHorizontal: false, isBackward: true);
                        for (int i = 0; i < targetRows.Count; i++)
                        {
                            DataGridCellAccessor.SetCellText(targetRows[i], col, newValues[i]);
                        }
                    }

                    SelectCombinedRange(targetRows.Concat(_sourceRows), _sourceCols);
                }
            }
        }
    }

    private void ExecuteDoubleClickFillDown()
    {
        if (!CaptureSourceSelection()) return;
        if (_sourceRows.Count == 0 || _sourceCols.Count == 0) return;

        int lastRowIndex = _maxRowIndex;
        int maxRows = _grid.Items.Count;
        if (lastRowIndex >= maxRows - 1) return;

        int checkColIndex = Math.Max(0, _sourceCols[0].DisplayIndex - 1);
        var checkCol = _grid.Columns.FirstOrDefault(c => c.DisplayIndex == checkColIndex);
        int targetRow = -1;

        if (checkCol != null)
        {
            for (int r = lastRowIndex + 1; r < maxRows; r++)
            {
                string text = DataGridCellAccessor.GetCellText(_grid.Items[r], checkCol);
                if (string.IsNullOrWhiteSpace(text)) break;
                targetRow = r;
            }
        }

        if (targetRow <= lastRowIndex)
            targetRow = Math.Min(maxRows - 1, lastRowIndex + 5);

        _targetHoverRowIndex = targetRow;
        _isDraggingHorizontal = false;
        ApplyAutoFill();
        UpdateAdorner();
        _onFilled?.Invoke();
    }

    private bool ExecuteFillShortcut(bool vertical)
    {
        var validCells = _grid.SelectedCells
            .Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly)
            .ToList();

        if (validCells.Count < 2) return false;

        var items = _grid.Items.Cast<object>().Where(it => validCells.Any(c => c.Item == it)).ToList();
        var cols = _grid.Columns.Where(col => validCells.Any(c => c.Column == col)).OrderBy(c => c.DisplayIndex).ToList();

        if (vertical)
        {
            if (items.Count < 2) return false;
            foreach (var col in cols)
            {
                string seed = DataGridCellAccessor.GetCellText(items[0], col);
                var source = new List<string> { seed };
                if (items.Count > 2)
                {
                    string second = DataGridCellAccessor.GetCellText(items[1], col);
                    if (!string.IsNullOrWhiteSpace(second))
                        source.Add(second);
                }

                int targetCount = items.Count - source.Count;
                if (targetCount > 0)
                {
                    var newValues = AutoFillSeriesGenerator.GenerateNextValues(source, targetCount, isHorizontal: false);
                    for (int i = 0; i < targetCount; i++)
                        DataGridCellAccessor.SetCellText(items[source.Count + i], col, newValues[i]);
                }
            }
            return true;
        }
        else
        {
            if (cols.Count < 2) return false;
            foreach (var rowItem in items)
            {
                string seed = DataGridCellAccessor.GetCellText(rowItem, cols[0]);
                var source = new List<string> { seed };
                if (cols.Count > 2)
                {
                    string second = DataGridCellAccessor.GetCellText(rowItem, cols[1]);
                    if (!string.IsNullOrWhiteSpace(second))
                        source.Add(second);
                }

                int targetCount = cols.Count - source.Count;
                if (targetCount > 0)
                {
                    var newValues = AutoFillSeriesGenerator.GenerateNextValues(source, targetCount, isHorizontal: true);
                    for (int i = 0; i < targetCount; i++)
                        DataGridCellAccessor.SetCellText(rowItem, cols[source.Count + i], newValues[i]);
                }
            }
            return true;
        }
    }

    private void SelectCombinedRange(IEnumerable<object> rows, IEnumerable<DataGridColumn> cols)
    {
        _grid.SelectedCells.Clear();
        var rList = rows.Distinct().ToList();
        var cList = cols.Distinct().ToList();

        foreach (var r in rList)
        {
            foreach (var c in cList)
            {
                _grid.SelectedCells.Add(new DataGridCellInfo(r, c));
            }
        }
    }

    private Rect? ComputeGridCellRangeBounds(IEnumerable<object> rows, IEnumerable<DataGridColumn> cols)
    {
        var cells = new List<DataGridCellInfo>();
        foreach (var r in rows)
        {
            foreach (var c in cols)
            {
                cells.Add(new DataGridCellInfo(r, c));
            }
        }
        return ComputeCellsBounds(_grid, cells);
    }

    private static Rect? ComputeCellsBounds(DataGrid grid, IEnumerable<DataGridCellInfo> cells)
    {
        Rect? bounds = null;
        var cellsByItem = cells.GroupBy(c => c.Item);

        foreach (var rowGroup in cellsByItem)
        {
            if (grid.ItemContainerGenerator.ContainerFromItem(rowGroup.Key) is not DataGridRow row)
                continue;

            var presenter = DataGridExcelNavigation.FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) continue;

            foreach (var cellInfo in rowGroup)
            {
                int colIndex = grid.Columns.IndexOf(cellInfo.Column);
                if (colIndex < 0) continue;

                if (presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) is not DataGridCell cell)
                    continue;

                if (cell.ActualWidth <= 0 || cell.ActualHeight <= 0)
                    continue;

                var topLeft = cell.TransformToAncestor(grid).Transform(new Point(0, 0));
                var cellRect = new Rect(topLeft, new Size(cell.ActualWidth, cell.ActualHeight));
                bounds = bounds == null ? cellRect : Rect.Union(bounds.Value, cellRect);
            }
        }
        return bounds;
    }

    private (int rowIndex, DataGridColumn? column) FindCellAtPoint(Point p)
    {
        var (hitRow, hitCol) = HitTestCell(_grid, p);
        if (hitCol != null)
            return (hitRow >= 0 ? hitRow : _minRowIndex, hitCol);

        // Fallback por proximidad geométrica de columnas si el hit no cayó directo en DataGridCell
        DataGridColumn? bestCol = null;
        double bestDist = double.MaxValue;

        var visibleCols = _grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
        if (visibleCols.Count == 0) return (-1, null);

        int testRowIdx = Math.Clamp(hitRow >= 0 ? hitRow : _minRowIndex, 0, Math.Max(0, _grid.Items.Count - 1));
        object? testRowItem = _grid.Items.Count > 0 ? _grid.Items[testRowIdx] : null;

        if (testRowItem != null)
        {
            foreach (var col in visibleCols)
            {
                var rect = GetCellRectFast(_grid, testRowItem, col);
                if (rect.HasValue)
                {
                    if (p.X >= rect.Value.Left && p.X <= rect.Value.Right)
                        return (testRowIdx, col);

                    double dist = p.X < rect.Value.Left ? (rect.Value.Left - p.X) : (p.X - rect.Value.Right);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCol = col;
                    }
                }
            }
        }

        return (testRowIdx, bestCol ?? visibleCols[0]);
    }

    private static Rect? GetCellRectFast(DataGrid grid, object item, DataGridColumn column)
    {
        if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            return null;

        var presenter = DataGridExcelNavigation.FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null) return null;

        int colIndex = grid.Columns.IndexOf(column);
        if (colIndex < 0) return null;

        if (presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) is not DataGridCell cell)
            return null;

        if (cell.ActualWidth <= 0 || cell.ActualHeight <= 0) return null;

        var topLeft = cell.TransformToAncestor(grid).Transform(new Point(0, 0));
        return new Rect(topLeft, new Size(cell.ActualWidth, cell.ActualHeight));
    }

    private static (int rowIndex, DataGridColumn? column) HitTestCell(DataGrid grid, Point p)
    {
        var hit = VisualTreeHelper.HitTest(grid, p);
        if (hit == null) return (-1, null);

        DependencyObject? current = hit.VisualHit;
        DataGridCell? cell = null;
        DataGridRow? row = null;

        while (current != null && current != grid)
        {
            if (cell == null && current is DataGridCell c)
                cell = c;
            if (row == null && current is DataGridRow r)
                row = r;
            if (cell != null && row != null)
                break;
            current = VisualTreeHelper.GetParent(current);
        }

        if (row != null && cell != null)
        {
            int rowIndex = row.GetIndex();
            return (rowIndex, cell.Column);
        }

        return (-1, null);
    }
}
