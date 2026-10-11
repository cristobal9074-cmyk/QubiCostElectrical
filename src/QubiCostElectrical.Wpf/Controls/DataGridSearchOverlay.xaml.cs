using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public sealed record DataGridSearchMatch(object Item, DataGridColumn Column, int RowIndex, int ColumnIndex, string Value);

public partial class DataGridSearchOverlay : UserControl
{
    public static readonly DependencyProperty TargetGridProperty = DependencyProperty.Register(
        nameof(TargetGrid), typeof(DataGrid), typeof(DataGridSearchOverlay),
        new PropertyMetadata(null, OnTargetGridChanged));

    public DataGrid? TargetGrid
    {
        get => (DataGrid?)GetValue(TargetGridProperty);
        set => SetValue(TargetGridProperty, value);
    }

    private readonly List<DataGridSearchMatch> _matches = new();
    private int _currentIndex = -1;
    private SearchMatchAdorner? _adorner;

    public bool IsOpen => Visibility == Visibility.Visible;

    public DataGridSearchOverlay()
    {
        InitializeComponent();
    }

    private static void OnTargetGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGridSearchOverlay overlay) return;

        if (e.OldValue is DataGrid oldGrid)
        {
            oldGrid.PreviewKeyDown -= overlay.OnTargetGridPreviewKeyDown;
            oldGrid.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(overlay.OnTargetGridScrollChanged));
        }

        if (e.NewValue is DataGrid newGrid)
        {
            newGrid.PreviewKeyDown += overlay.OnTargetGridPreviewKeyDown;
            newGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(overlay.OnTargetGridScrollChanged), handledEventsToo: true);
        }
    }

    private void OnTargetGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            Close();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.F || e.Key == Key.B) && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Open();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F3)
        {
            if (IsOpen)
            {
                bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                if (backward) FindPrevious();
                else FindNext();
            }
            else
            {
                Open();
            }
            e.Handled = true;
        }
    }

    private void OnTargetGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _adorner?.InvalidateVisual();
    }

    public void Open()
    {
        Visibility = Visibility.Visible;
        UpdateLayout();
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            RunSearch();
        }

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(() =>
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
                SearchBox.SelectAll();
            }));
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        _adorner?.Clear();
        if (TargetGrid != null)
        {
            TargetGrid.Focus();
            if (TargetGrid.SelectedItem != null && TargetGrid.CurrentCell.IsValid)
            {
                TargetGrid.ScrollIntoView(TargetGrid.SelectedItem, TargetGrid.CurrentCell.Column);
            }
        }
    }

    public void FindNext()
    {
        if (_matches.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _matches.Count;
        NavigateToCurrentMatch();
    }

    public void FindPrevious()
    {
        if (_matches.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _matches.Count) % _matches.Count;
        NavigateToCurrentMatch();
    }

    private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RunSearch();
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (backward) FindPrevious();
            else FindNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F3)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (backward) FindPrevious();
            else FindNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            FindNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            FindPrevious();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnPrevClick(object sender, RoutedEventArgs e) => FindPrevious();
    private void OnNextClick(object sender, RoutedEventArgs e) => FindNext();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void RunSearch()
    {
        _matches.Clear();
        _currentIndex = -1;

        string query = SearchBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query) || TargetGrid == null)
        {
            MatchCountText.Text = "0 / 0";
            _adorner?.Clear();
            return;
        }

        var visibleColumns = TargetGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        for (int r = 0; r < TargetGrid.Items.Count; r++)
        {
            var item = TargetGrid.Items[r];
            if (item == CollectionView.NewItemPlaceholder) continue;

            for (int c = 0; c < visibleColumns.Count; c++)
            {
                var col = visibleColumns[c];
                string text = DataGridExcelNavigation.GetCellText(item, col);
                if (!string.IsNullOrWhiteSpace(text) &&
                    text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    _matches.Add(new DataGridSearchMatch(item, col, r, c, text));
                }
            }
        }

        if (_matches.Count > 0)
        {
            // Ubicar la coincidencia más cercana a la selección actual de la grilla
            object? currentItem = TargetGrid.SelectedItem ?? TargetGrid.CurrentCell.Item;
            int foundIndex = 0;
            if (currentItem != null)
            {
                int matchIdx = _matches.FindIndex(m => ReferenceEquals(m.Item, currentItem));
                if (matchIdx >= 0) foundIndex = matchIdx;
            }

            _currentIndex = foundIndex;
            NavigateToCurrentMatch();
        }
        else
        {
            MatchCountText.Text = "Sin resultados";
            _adorner?.Clear();
        }
    }

    private void NavigateToCurrentMatch()
    {
        if (_currentIndex < 0 || _currentIndex >= _matches.Count || TargetGrid == null)
            return;

        var match = _matches[_currentIndex];
        MatchCountText.Text = $"{_currentIndex + 1} / {_matches.Count}";

        try
        {
            if (TargetGrid.SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                TargetGrid.SelectedItem = match.Item;
                if (match.RowIndex >= 0 && match.RowIndex < TargetGrid.Items.Count)
                {
                    TargetGrid.SelectedIndex = match.RowIndex;
                }
            }
            else
            {
                TargetGrid.SelectedCells.Clear();
                var cellInfo = new DataGridCellInfo(match.Item, match.Column);
                TargetGrid.SelectedCells.Add(cellInfo);
            }

            TargetGrid.CurrentCell = new DataGridCellInfo(match.Item, match.Column);
            TargetGrid.ScrollIntoView(match.Item, match.Column);
            TargetGrid.UpdateLayout();

            var adorner = EnsureAdorner();
            adorner?.SetMatch(match.Item, match.Column);

            TargetGrid.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => _adorner?.InvalidateVisual()));
        }
        catch (Exception ex)
        {
            AppLog.Warning("DataGridSearch", $"Error al navegar a celda coincidente: {ex.Message}");
        }
    }

    private SearchMatchAdorner? EnsureAdorner()
    {
        if (TargetGrid == null) return null;
        if (_adorner != null) return _adorner;

        var layer = AdornerLayer.GetAdornerLayer(TargetGrid);
        if (layer == null) return null;

        _adorner = new SearchMatchAdorner(TargetGrid);
        layer.Add(_adorner);
        return _adorner;
    }
}
