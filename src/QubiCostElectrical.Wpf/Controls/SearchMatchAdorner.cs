using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Controls;

/// <summary>
/// Adorner visual de alto rendimiento que resalta la celda activa coincidente
/// con un recuadro luminoso ámbar/dorado estilo VS Code y Excel.
/// </summary>
internal sealed class SearchMatchAdorner : Adorner
{
    private static readonly Pen OutlinePen = new(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.5) { DashStyle = DashStyles.Solid };
    private static readonly Brush HighlightFill = new SolidColorBrush(Color.FromArgb(45, 245, 158, 11));

    static SearchMatchAdorner()
    {
        OutlinePen.Freeze();
        HighlightFill.Freeze();
    }

    private readonly DataGrid _grid;
    private object? _targetItem;
    private DataGridColumn? _targetColumn;

    public SearchMatchAdorner(DataGrid grid) : base(grid)
    {
        _grid = grid;
        IsHitTestVisible = false;
    }

    public void SetMatch(object? item, DataGridColumn? column)
    {
        _targetItem = item;
        _targetColumn = column;
        InvalidateVisual();
    }

    public void Clear()
    {
        _targetItem = null;
        _targetColumn = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_targetItem == null || _targetColumn == null)
            return;

        var row = _grid.ItemContainerGenerator.ContainerFromItem(_targetItem) as DataGridRow;
        if (row == null)
            return;

        int colIndex = _grid.Columns.IndexOf(_targetColumn);
        if (colIndex < 0)
            return;

        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null)
            return;

        if (presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) is not DataGridCell cell || !cell.IsVisible)
            return;

        try
        {
            var transform = cell.TransformToAncestor(_grid);
            var rect = transform.TransformBounds(new Rect(0, 0, cell.ActualWidth, cell.ActualHeight));

            // Evitar dibujar si está fuera del área visible de la grilla
            var gridBounds = new Rect(0, 0, _grid.ActualWidth, _grid.ActualHeight);
            rect.Intersect(gridBounds);

            if (rect.Width > 0 && rect.Height > 0)
            {
                dc.DrawRoundedRectangle(HighlightFill, OutlinePen, rect, 3, 3);
            }
        }
        catch (Exception)
        {
            // Ocurre si la celda fue removida del visual tree durante un layout pass o no está conectada
        }
    }

    private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
