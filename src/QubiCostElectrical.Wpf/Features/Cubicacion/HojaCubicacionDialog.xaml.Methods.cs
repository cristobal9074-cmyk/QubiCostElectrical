using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class HojaCubicacionDialog : Window
{
    public static (decimal? PickedValue, List<HojaCubicacionItemVm> Items) PickValue(
        Window? owner, IEnumerable<HojaCubicacionItemVm>? existingItems)
    {
        var dialog = new HojaCubicacionDialog(existingItems) { Owner = owner, IsPickMode = true };
        dialog.ApplyPickModeChrome();
        dialog.ShowDialog();
        return (dialog.PickedValue, dialog.Items.ToList());
    }

    public HojaCubicacionDialog() : this(null) { }

    public HojaCubicacionDialog(IEnumerable<HojaCubicacionItemVm>? existingItems)
    {
        InitializeComponent();
        ItemsHost.ItemsSource = items;

        var previos = existingItems?.ToList();
        if (previos != null && previos.Count > 0)
        {
            foreach (var existente in previos)
            {
                AttachItem(existente);
                items.Add(existente);
            }
        }
        else
        {
            var primerItem = new HojaCubicacionItemVm("", "Nuevo ítem", "ml", DefaultColumnCount);
            AttachItem(primerItem);
            items.Add(primerItem);
        }

        RefreshGrandTotal();
    }

    private void ApplyPickModeChrome()
    {
        Title = "Hoja de Cubicación — Elegir cantidad para presupuesto";
        PickModeBanner.Visibility = Visibility.Visible;
    }

    private void AttachItem(HojaCubicacionItemVm item)
    {
        item.PropertyChanged += OnItemPropertyChanged;
    }

    private void DetachItem(HojaCubicacionItemVm item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        if (itemGrids.TryGetValue(item, out var grid))
            loadedGrids.Remove(grid);
        itemGrids.Remove(item);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        foreach (var item in items)
            item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not HojaCubicacionItemVm item) return;

        if (e.PropertyName == nameof(HojaCubicacionItemVm.Subtotal))
            RefreshGrandTotal();
        else if (e.PropertyName == nameof(HojaCubicacionItemVm.ColumnCount) && itemGrids.TryGetValue(item, out var grid))
            BuildColumns(grid, item);
    }

    private void RefreshGrandTotal()
    {
        decimal total = 0m;
        foreach (var item in items)
            total += item.Subtotal;
        string fmt = total % 1 == 0 ? total.ToString("N0", CultureInfo.CurrentCulture) : total.ToString("#,##0.##", CultureInfo.CurrentCulture);
        GrandTotalRun.Text = fmt;
        if (HeaderGrandTotalRun != null)
            HeaderGrandTotalRun.Text = fmt;
    }

    private void OnItemGridLoaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (grid.DataContext is not HojaCubicacionItemVm item) return;

        itemGrids[item] = grid;

        if (loadedGrids.Add(grid))
            BuildColumns(grid, item);

        grid.PreviewKeyDown -= OnItemGridPreviewKeyDown;
        grid.PreviewKeyDown += OnItemGridPreviewKeyDown;
        grid.PreviewTextInput -= OnItemGridPreviewTextInput;
        grid.PreviewTextInput += OnItemGridPreviewTextInput;
        grid.CurrentCellChanged -= OnItemGridCurrentCellChanged;
        grid.CurrentCellChanged += OnItemGridCurrentCellChanged;

        grid.PreviewMouseLeftButtonDown -= OnItemGridPreviewMouseLeftButtonDown;
        grid.PreviewMouseLeftButtonDown += OnItemGridPreviewMouseLeftButtonDown;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (grid.Items.Count > 0 && grid.Columns.Count > 2)
                DataGridExcelNavigation.FocusCell(grid, 0, grid.Columns[2]);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private sealed class FormulaPointModeState
    {
        public DataGrid Grid { get; set; } = null!;
        public HojaCubicacionItemVm Item { get; set; } = null!;
        public TextBox Editor { get; set; } = null!;
        public int EditRowIndex { get; set; }
        public int EditColIndex { get; set; }
        public int TargetRowIndex { get; set; }
        public int TargetColIndex { get; set; }
        public int PrefixLength { get; set; }
        public bool IsActive { get; set; }
        public bool IsFormulaBar { get; set; }
    }

    private FormulaPointModeState? _pointMode;

    private void HandlePointModeArrowKey(
        DataGrid grid,
        HojaCubicacionItemVm item,
        TextBox editor,
        Key key,
        ModifierKeys modifiers)
    {
        if (_pointMode == null || !_pointMode.IsActive || _pointMode.Grid != grid || _pointMode.Editor != editor)
        {
            var cell = FindAncestor<DataGridCell>(editor);
            var row = FindAncestor<DataGridRow>(editor);
            int originRow = row != null ? grid.Items.IndexOf(row.Item) : grid.SelectedIndex;
            int originCol = cell != null ? grid.Columns.IndexOf(cell.Column) : -1;

            if (originRow < 0) originRow = grid.SelectedIndex >= 0 ? grid.SelectedIndex : 0;
            if (originCol < 2)
            {
                if (grid.CurrentCell.IsValid && grid.CurrentCell.Column != null)
                    originCol = grid.Columns.IndexOf(grid.CurrentCell.Column);
                if (originCol < 2) originCol = 2; // Por defecto columna C
            }

            _pointMode = new FormulaPointModeState
            {
                Grid = grid,
                Item = item,
                Editor = editor,
                EditRowIndex = originRow,
                EditColIndex = originCol,
                TargetRowIndex = originRow,
                TargetColIndex = originCol,
                PrefixLength = editor.Text.Length,
                IsActive = true,
                IsFormulaBar = editor.Name == "FormulaBarInput"
            };
        }

        int targetRow = _pointMode.TargetRowIndex;
        int targetCol = _pointMode.TargetColIndex;

        switch (key)
        {
            case Key.Left:
                targetCol--;
                break;
            case Key.Right:
                targetCol++;
                break;
            case Key.Up:
                targetRow--;
                break;
            case Key.Down:
                targetRow++;
                break;
        }

        if (targetCol < 2) targetCol = 2;

        int maxDataColIndex = grid.Columns.Count - 2;
        if (targetCol > maxDataColIndex)
        {
            item.AddColumn();
            BuildColumns(grid, item);
            grid.UpdateLayout();
            maxDataColIndex = grid.Columns.Count - 2;
        }

        if (targetRow < 0) targetRow = 0;
        if (targetRow >= grid.Items.Count)
        {
            item.AddFila();
            grid.UpdateLayout();
        }

        _pointMode.TargetRowIndex = targetRow;
        _pointMode.TargetColIndex = targetCol;

        string colHeader = grid.Columns[targetCol].Header?.ToString() ?? "";
        if (string.IsNullOrEmpty(colHeader) || colHeader == "Descripción" || colHeader == "Suma")
        {
            colHeader = HojaCubicacionColumns.DataColumnLetter(targetCol - 2);
        }

        int rowNum = targetRow + 1;
        string cellRef = $"{colHeader}{rowNum}";

        string prefix = _pointMode.Editor.Text.Length >= _pointMode.PrefixLength
            ? _pointMode.Editor.Text.Substring(0, _pointMode.PrefixLength)
            : _pointMode.Editor.Text;

        _pointMode.Editor.Text = prefix + cellRef;
        _pointMode.Editor.CaretIndex = _pointMode.Editor.Text.Length;

        if (targetRow >= 0 && targetRow < grid.Items.Count && targetCol >= 0 && targetCol < grid.Columns.Count)
        {
            var rowItem = grid.Items[targetRow];
            var colObj = grid.Columns[targetCol];
            grid.ScrollIntoView(rowItem, colObj);
            grid.SelectedCells.Clear();
            grid.SelectedCells.Add(new DataGridCellInfo(rowItem, colObj));
        }

        _pointMode.Editor.Focus();

        var itemRoot = FindAncestor<Border>(grid);
        if (itemRoot != null)
        {
            var previewBlock = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarPreview");
            UpdateFormulaPreview(previewBlock, _pointMode.Editor.Text);

            if (!_pointMode.IsFormulaBar)
            {
                var formulaInput = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
                if (formulaInput != null)
                {
                    formulaInput.Text = _pointMode.Editor.Text;
                }
            }
        }
    }

    private void OnItemGridPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        if (string.IsNullOrEmpty(e.Text)) return;

        // Si estamos en Point Mode activo y el usuario escribe un operador o carácter:
        if (_pointMode != null && _pointMode.IsActive && _pointMode.Grid == grid)
        {
            char ch = e.Text[0];
            bool isOp = ch is '+' or '-' or '*' or '/' or '^' or '%' or '(' or ')' or ',' or ';' or ':';

            if (isOp || char.IsLetterOrDigit(ch))
            {
                _pointMode.IsActive = false;

                var ed = _pointMode.Editor;
                int pos = ed.CaretIndex >= 0 ? ed.CaretIndex : ed.Text.Length;
                ed.Text = ed.Text.Insert(pos, e.Text);
                ed.CaretIndex = pos + e.Text.Length;

                var itemRoot = FindAncestor<Border>(grid);
                if (itemRoot != null)
                {
                    var previewBlock = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarPreview");
                    UpdateFormulaPreview(previewBlock, ed.Text);
                    if (!_pointMode.IsFormulaBar)
                    {
                        var formulaInput = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
                        if (formulaInput != null) formulaInput.Text = ed.Text;
                    }
                }

                e.Handled = true;
                return;
            }
        }

        if (char.IsControl(e.Text[0])) return;
        if (DataGridExcelNavigation.IsEditing(grid)) return;

        var col = grid.CurrentCell.Column;
        if (col == null || col.IsReadOnly || grid.IsReadOnly) return;

        if (grid.BeginEdit())
        {
            var cell = DataGridExcelNavigation.GetCurrentCell(grid);
            cell?.UpdateLayout();
            var textBox = cell != null ? DataGridExcelNavigation.FindVisualChild<TextBox>(cell) : null;
            if (textBox != null)
            {
                textBox.Focus();
                textBox.Text = e.Text;
                textBox.CaretIndex = textBox.Text.Length;
                e.Handled = true;
            }
        }
    }

    private void OnItemGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not HojaCubicacionItemVm item) return;

        TextBox? editor = Keyboard.FocusedElement as TextBox;

        // Escape mientras se edita celda o en Point Mode
        if (e.Key == Key.Escape)
        {
            if (_pointMode != null && _pointMode.IsActive && _pointMode.Grid == grid)
            {
                var ed = _pointMode.Editor;
                if (ed.Text.Length >= _pointMode.PrefixLength)
                {
                    ed.Text = ed.Text.Substring(0, _pointMode.PrefixLength);
                    ed.CaretIndex = ed.Text.Length;
                }
                _pointMode.IsActive = false;
                e.Handled = true;
                return;
            }

            if (DataGridExcelNavigation.IsEditing(grid))
            {
                _pointMode = null;
                grid.CancelEdit();
                e.Handled = true;
                return;
            }
        }

        // Backspace mientras se está en Point Mode activo: revertir a la posición previa
        if (e.Key == Key.Back && _pointMode != null && _pointMode.IsActive && _pointMode.Grid == grid)
        {
            var ed = _pointMode.Editor;
            if (ed.Text.Length >= _pointMode.PrefixLength)
            {
                ed.Text = ed.Text.Substring(0, _pointMode.PrefixLength);
                ed.CaretIndex = ed.Text.Length;
            }
            _pointMode.IsActive = false;
            e.Handled = true;
            return;
        }

        // F2: Activar edición in-situ
        if (editor == null && e.Key == Key.F2)
        {
            if (grid.CurrentCell.Column != null && !grid.CurrentCell.Column.IsReadOnly)
            {
                grid.BeginEdit();
                e.Handled = true;
                return;
            }
        }

        // Flechas del teclado en edición (Point Mode tipo Excel)
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && editor != null)
        {
            bool isPointActive = _pointMode != null && _pointMode.IsActive && _pointMode.Grid == grid;
            bool isOpenFormula = IsFormulaOpenForCellReference(editor.Text);

            if (isPointActive || isOpenFormula)
            {
                HandlePointModeArrowKey(grid, item, editor, e.Key, Keyboard.Modifiers);
                e.Handled = true;
                return;
            }
        }

        // Delete / Backspace cuando no se edita texto: borrar celdas seleccionadas
        if (editor == null && e.Key is Key.Delete or Key.Back)
        {
            if (grid.SelectedCells.Any(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly))
            {
                DataGridExcelNavigation.ClearSelection(grid);
                e.Handled = true;
                return;
            }
        }

        // Enter / Shift+Enter: confirmar y mover arriba/abajo
        if (e.Key is Key.Enter or Key.Return)
        {
            if (_pointMode != null && _pointMode.Grid == grid)
            {
                _pointMode.IsActive = false;
                _pointMode = null;
            }

            if (DataGridExcelNavigation.IsEditing(grid))
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                item.RefreshAllDisplays();
                RefreshGrandTotal();

                var currentColumn = grid.CurrentCell.Column
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                object? currentItem = grid.CurrentCell.Item ?? grid.SelectedItem;

                if (currentColumn != null && currentItem != null)
                {
                    int offset = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
                    grid.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DataGridExcelNavigation.MoveToRowOffset(grid, currentColumn, offset, currentItem);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                e.Handled = true;
                return;
            }

            if (editor == null)
            {
                var currentColumn = grid.CurrentCell.Column
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                object? currentItem = grid.CurrentCell.Item ?? grid.SelectedItem;

                if (currentColumn != null && currentItem != null)
                {
                    int offset = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
                    DataGridExcelNavigation.MoveToRowOffset(grid, currentColumn, offset, currentItem);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Tab / Shift+Tab: avanzar / retroceder columna
        if (e.Key == Key.Tab)
        {
            if (_pointMode != null && _pointMode.Grid == grid)
            {
                _pointMode.IsActive = false;
                _pointMode = null;
            }

            if (DataGridExcelNavigation.IsEditing(grid))
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                item.RefreshAllDisplays();
                RefreshGrandTotal();

                var currentColumn = grid.CurrentCell.Column
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                object? currentItem = grid.CurrentCell.Item ?? grid.SelectedItem;

                if (currentColumn != null && currentItem != null)
                {
                    int offset = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
                    grid.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DataGridExcelNavigation.MoveToColumnOffset(grid, currentColumn, offset, wrapRows: true, currentItem);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                e.Handled = true;
                return;
            }

            if (editor == null)
            {
                var currentColumn = grid.CurrentCell.Column
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                object? currentItem = grid.CurrentCell.Item ?? grid.SelectedItem;

                if (currentColumn != null && currentItem != null)
                {
                    int offset = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
                    DataGridExcelNavigation.MoveToColumnOffset(grid, currentColumn, offset, wrapRows: true, currentItem);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Agregar/quitar fila con Ctrl+Plus / Ctrl+Minus
        if (editor == null && Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key is Key.Add or Key.OemPlus)
        {
            item.AddFila();
            e.Handled = true;
            return;
        }

        if (editor == null && Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key is Key.Subtract or Key.OemMinus &&
            grid.CurrentItem is HojaCubicacionFilaVm filaActual)
        {
            item.RemoveFila(filaActual);
            e.Handled = true;
            return;
        }

        // Ctrl+C / Ctrl+X / Ctrl+V
        if (editor == null && Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.X)
        {
            DataGridExcelNavigation.CopySelectionToClipboard(grid);
            DataGridExcelNavigation.ClearCopyMarker(grid);
            if (e.Key == Key.X)
                DataGridExcelNavigation.ClearSelection(grid);
            e.Handled = true;
            return;
        }

        if (editor == null && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            if (!string.IsNullOrEmpty(text))
                DataGridExcelNavigation.TryPasteIntoSelection(grid, text, _ => false);
            e.Handled = true;
            return;
        }
    }

    // ── Barra de fórmulas conectada en tiempo real ──
    private void OnItemGridCurrentCellChanged(object? sender, EventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var itemRoot = FindAncestor<Border>(grid);
        if (itemRoot == null) return;

        var refText = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarRef");
        var inputBox = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
        var previewBlock = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarPreview");
        if (refText == null || inputBox == null) return;

        var cellInfo = grid.CurrentCell;
        if (!cellInfo.IsValid || cellInfo.Column == null)
        {
            refText.Text = "";
            inputBox.Text = "";
            inputBox.Tag = null;
            inputBox.IsEnabled = false;
            if (previewBlock != null) previewBlock.Text = "";
            return;
        }

        int rowNumber = grid.Items.IndexOf(cellInfo.Item) + 1;
        refText.Text = $"{cellInfo.Column.Header}{rowNumber}";
        inputBox.Tag = cellInfo;
        inputBox.IsEnabled = !cellInfo.Column.IsReadOnly;
        string cellText = DataGridExcelNavigation.GetCellText(cellInfo.Item, cellInfo.Column);
        inputBox.Text = cellText;

        UpdateFormulaPreview(previewBlock, cellText);
    }

    private void OnFormulaBarTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var itemRoot = FindAncestor<Border>(box);
        if (itemRoot == null) return;
        var previewBlock = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarPreview");
        UpdateFormulaPreview(previewBlock, box.Text);
    }

    private static void UpdateFormulaPreview(TextBlock? previewBlock, string raw)
    {
        if (previewBlock == null) return;
        string text = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(text))
        {
            previewBlock.Text = "";
            return;
        }

        try
        {
            var singleRow = new List<string> { text };
            var eval = HojaCubicacionFormulaEngine.EvaluateRow(singleRow);
            if (eval.Cells.Count > 0 && eval.Cells[0].Success)
            {
                previewBlock.Text = $"= {eval.Cells[0].DisplayText}";
                previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Green");
            }
            else if (eval.Cells.Count > 0)
            {
                previewBlock.Text = eval.Cells[0].DisplayText;
                previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Red");
            }
        }
        catch
        {
            previewBlock.Text = "¡ERROR!";
            previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Red");
        }
    }

    private void OnFormulaBarKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Escape) return;
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
        {
            CommitFormulaBar(box);
            if (box.Tag is DataGridCellInfo cellInfo && cellInfo.IsValid)
            {
                var itemRoot = FindAncestor<Border>(box);
                var grid = itemRoot != null ? FindDescendantByName<DataGrid>(itemRoot, null) : null;
                if (grid != null && cellInfo.Column != null)
                {
                    DataGridExcelNavigation.MoveToRowOffset(grid, cellInfo.Column, 1, cellInfo.Item);
                }
            }
        }
        else if (e.Key == Key.Escape)
        {
            // Restaurar valor original
            if (box.Tag is DataGridCellInfo cellInfo && cellInfo.IsValid && cellInfo.Column != null)
                box.Text = DataGridExcelNavigation.GetCellText(cellInfo.Item, cellInfo.Column);
        }

        e.Handled = true;
        Keyboard.ClearFocus();
    }

    private void OnFormulaBarPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;

        var itemRoot = FindAncestor<Border>(box);
        if (itemRoot == null) return;
        var grid = FindDescendantByName<DataGrid>(itemRoot, null);
        if (grid == null || grid.DataContext is not HojaCubicacionItemVm item) return;

        bool isPointActive = _pointMode != null && _pointMode.IsActive && _pointMode.Editor == box;
        bool isOpenFormula = IsFormulaOpenForCellReference(box.Text);

        if (isPointActive || isOpenFormula)
        {
            HandlePointModeArrowKey(grid, item, box, e.Key, Keyboard.Modifiers);
            e.Handled = true;
        }
    }

    private void OnFormulaBarLostFocus(object sender, RoutedEventArgs e) => CommitFormulaBar((TextBox)sender);

    private void CommitFormulaBar(TextBox box)
    {
        if (box.Tag is not DataGridCellInfo cellInfo || !cellInfo.IsValid || cellInfo.Column == null)
            return;
        DataGridExcelNavigation.SetCellText(cellInfo.Item, cellInfo.Column, box.Text);
        var itemRoot = FindAncestor<Border>(box);
        var grid = itemRoot != null ? FindDescendantByName<DataGrid>(itemRoot, null) : null;
        if (grid?.DataContext is HojaCubicacionItemVm item)
        {
            item.RefreshAllDisplays();
            RefreshGrandTotal();
        }
    }

    private void OnFxButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var itemRoot = FindAncestor<Border>(btn);
        if (itemRoot == null) return;
        var inputBox = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
        if (inputBox == null || !inputBox.IsEnabled) return;

        var menu = new ContextMenu();

        void AddMenuItem(string header, string template, int cursorOffset)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) =>
            {
                inputBox.Focus();
                inputBox.Text = template;
                inputBox.CaretIndex = cursorOffset;
            };
            menu.Items.Add(mi);
        }

        AddMenuItem("SUMA(rango) — Sumar serie o rango continuo", "=SUMA(C:E)", 6);
        AddMenuItem("PROMEDIO(rango) — Promedio de celdas", "=PROMEDIO(C:E)", 10);
        AddMenuItem("MIN(rango) — Valor mínimo", "=MIN(C:E)", 5);
        AddMenuItem("MAX(rango) — Valor máximo", "=MAX(C:E)", 5);
        AddMenuItem("REDONDEAR(celda; dec) — Redondeo con decimales", "=REDONDEAR(C; 2)", 11);
        AddMenuItem("PORCENTAJE (%) — ej. C*15%", "* 15%", 0);
        AddMenuItem("CONTAR(rango) — Contar celdas con datos", "=CONTAR(C:E)", 8);

        menu.PlacementTarget = btn;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // ── Shift+clic: selección rectangular ──
    private void OnItemGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;

        if (IsPickMode && Keyboard.Modifiers == ModifierKeys.None)
        {
            var hitCell = HitTestCell(grid, e.GetPosition(grid));
            if (hitCell?.Item is HojaCubicacionFilaVm fila &&
                hitCell.Value.Column?.Header is string header && header == "Suma" &&
                !string.IsNullOrWhiteSpace(fila.SumaDisplay) &&
                decimal.TryParse(fila.SumaDisplay.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal picked))
            {
                fila.IsPicked = true;
                if (fila.Owner != null)
                    fila.Owner.IsPicked = fila.Owner.Filas.Where(f => !f.IsLabelRow).All(f => f.IsPicked);
                PickedValue = picked;
                DialogResult = true;
                Close();
                e.Handled = true;
                return;
            }
        }

        // Point-and-Click estilo Excel (con Mouse o Teclado): Si se está editando una fórmula abierta en la celda o en la barra fx,
        // hacer clic en otra celda inserta su dirección (ej. D1 o H2) en vez de perder la fórmula.
        TextBox? targetEditor = Keyboard.FocusedElement as TextBox;
        if (targetEditor == null)
        {
            var itemRootNode = FindAncestor<Border>(grid);
            targetEditor = itemRootNode != null ? FindDescendantByName<TextBox>(itemRootNode, "FormulaBarInput") : null;
        }

        if (targetEditor != null && targetEditor.IsEnabled && Keyboard.Modifiers == ModifierKeys.None)
        {
            string currentFormula = targetEditor.Text;
            bool isOpen = IsFormulaOpenForCellReference(currentFormula) || (_pointMode != null && _pointMode.IsActive && _pointMode.Editor == targetEditor);
            if (isOpen)
            {
                var hit = HitTestCell(grid, e.GetPosition(grid));
                if (hit != null && hit.Value.Column != null && hit.Value.Item != null)
                {
                    string colH = hit.Value.Column.Header?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(colH) && colH != "Descripción" && colH != "Suma")
                    {
                        int rNum = grid.Items.IndexOf(hit.Value.Item) + 1;
                        int cIdx = grid.Columns.IndexOf(hit.Value.Column);
                        int rIdx = grid.Items.IndexOf(hit.Value.Item);
                        string cellRef = $"{colH}{rNum}";

                        if (_pointMode == null || !_pointMode.IsActive || _pointMode.Editor != targetEditor)
                        {
                            _pointMode = new FormulaPointModeState
                            {
                                Grid = grid,
                                Item = (HojaCubicacionItemVm)grid.DataContext,
                                Editor = targetEditor,
                                EditRowIndex = rIdx,
                                EditColIndex = cIdx,
                                TargetRowIndex = rIdx,
                                TargetColIndex = cIdx,
                                PrefixLength = targetEditor.Text.Length,
                                IsActive = true,
                                IsFormulaBar = targetEditor.Name == "FormulaBarInput"
                            };
                        }
                        else
                        {
                            _pointMode.TargetRowIndex = rIdx;
                            _pointMode.TargetColIndex = cIdx;
                        }

                        string prefix = targetEditor.Text.Length >= _pointMode.PrefixLength
                            ? targetEditor.Text.Substring(0, _pointMode.PrefixLength)
                            : targetEditor.Text;

                        targetEditor.Text = prefix + cellRef;
                        targetEditor.CaretIndex = targetEditor.Text.Length;
                        targetEditor.Focus();

                        grid.SelectedCells.Clear();
                        grid.SelectedCells.Add(new DataGridCellInfo(hit.Value.Item, hit.Value.Column));

                        var itemRoot = FindAncestor<Border>(grid);
                        if (itemRoot != null)
                        {
                            var previewBlock = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarPreview");
                            UpdateFormulaPreview(previewBlock, targetEditor.Text);
                            if (!_pointMode.IsFormulaBar)
                            {
                                var formulaInput = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
                                if (formulaInput != null) formulaInput.Text = targetEditor.Text;
                            }
                        }

                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.Shift) return;

        var anchor = grid.CurrentCell;
        if (!anchor.IsValid) return;

        var target = HitTestCell(grid, e.GetPosition(grid));
        if (target == null) return;

        SelectCellRange(grid, anchor, target.Value);
        e.Handled = true;
    }

    private static DataGridCellInfo? HitTestCell(DataGrid grid, Point point)
    {
        var hit = VisualTreeHelper.HitTest(grid, point)?.VisualHit;
        if (hit == null) return null;

        var cell = hit as DataGridCell ?? FindAncestor<DataGridCell>(hit);
        if (cell?.Column == null) return null;

        var row = FindAncestor<DataGridRow>(cell);
        if (row == null) return null;

        return new DataGridCellInfo(row.Item, cell.Column);
    }

    private static void SelectCellRange(DataGrid grid, DataGridCellInfo anchor, DataGridCellInfo current)
    {
        var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
        int anchorRow = grid.Items.IndexOf(anchor.Item);
        int currentRow = grid.Items.IndexOf(current.Item);
        int anchorCol = columns.IndexOf(anchor.Column);
        int currentCol = columns.IndexOf(current.Column);
        if (anchorRow < 0 || currentRow < 0 || anchorCol < 0 || currentCol < 0) return;

        int rowMin = Math.Min(anchorRow, currentRow);
        int rowMax = Math.Max(anchorRow, currentRow);
        int colMin = Math.Min(anchorCol, currentCol);
        int colMax = Math.Max(anchorCol, currentCol);

        grid.SelectedCells.Clear();
        for (int r = rowMin; r <= rowMax; r++)
        {
            var item = grid.Items[r];
            for (int c = colMin; c <= colMax; c++)
                grid.SelectedCells.Add(new DataGridCellInfo(item, columns[c]));
        }

        grid.CurrentCell = current;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        return QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.FindAncestor<T>(child);
    }

    private static T? FindDescendantByName<T>(DependencyObject root, string? name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (name == null || match.Name == name))
                return match;
            var found = FindDescendantByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void BuildColumns(DataGrid grid, HojaCubicacionItemVm item)
    {
        grid.Columns.Clear();

        var hitTestableCellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource("DataGridCell.Excel"));
        hitTestableCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));

        var descripcionElementStyle = new Style(typeof(TextBlock));
        var placeholderTrigger = new MultiDataTrigger();
        placeholderTrigger.Conditions.Add(new Condition(new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), true));
        placeholderTrigger.Conditions.Add(new Condition(new Binding(nameof(HojaCubicacionFilaVm.Descripcion)) { Converter = new StringNotEmptyToBoolConverter() }, false));
        placeholderTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
        placeholderTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)Application.Current.FindResource("Brush.Muted")));
        descripcionElementStyle.Triggers.Add(placeholderTrigger);

        grid.Columns.Add(new DisplayBindingTextColumn
        {
            Header = "Descripción",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.Descripcion)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            DisplayBinding = new Binding(nameof(HojaCubicacionFilaVm.DescripcionDisplay)),
            Width = 180,
            CellStyle = hitTestableCellStyle,
            ElementStyle = descripcionElementStyle
        });

        var sumaColumn = new DataGridTextColumn
        {
            Header = "Suma",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.SumaDisplay)),
            Width = 76,
            IsReadOnly = true
        };
        var sumaCellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource("DataGridCell.Excel"));
        sumaCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, (Brush)Application.Current.FindResource("Brush.GreenSoft")));
        sumaCellStyle.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)Application.Current.FindResource("Brush.Green")));
        sumaCellStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));

        var noSumaForLabelRow = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), Value = true };
        noSumaForLabelRow.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        noSumaForLabelRow.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Transparent));
        sumaCellStyle.Triggers.Add(noSumaForLabelRow);

        var pickedTrigger = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsPicked)), Value = true };
        pickedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, (Brush)Application.Current.FindResource("Brush.BlueSoft")));
        pickedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)Application.Current.FindResource("Brush.Blue")));
        pickedTrigger.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "✓ Total transferido y llevado al cubicador"));
        sumaCellStyle.Triggers.Add(pickedTrigger);

        if (IsPickMode)
        {
            sumaCellStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            sumaCellStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Clic para usar este valor en presupuesto"));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, (Brush)Application.Current.FindResource("Brush.Green")));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            sumaCellStyle.Triggers.Add(hover);
        }
        sumaColumn.CellStyle = sumaCellStyle;
        grid.Columns.Add(sumaColumn);

        int columnCount = item.ColumnCount;
        for (int i = 0; i < columnCount; i++)
        {
            grid.Columns.Add(new DisplayBindingTextColumn
            {
                Header = HojaCubicacionColumns.DataColumnLetter(i),
                Binding = new Binding($"Valores[{i}]") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                DisplayBinding = new Binding($"ValoresDisplay[{i}]"),
                Width = 42,
                CellStyle = hitTestableCellStyle
            });
        }

        var deleteColumn = new DataGridTemplateColumn { Header = "", Width = 26 };
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "✕");
        factory.SetValue(Button.PaddingProperty, new Thickness(0));
        factory.SetValue(Button.FontSizeProperty, 10.0);
        factory.SetValue(Button.ToolTipProperty, "Quitar fila");

        var deleteButtonStyle = new Style(typeof(Button), (Style)Application.Current.FindResource("Button.Ghost"));
        deleteButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)Application.Current.FindResource("Brush.Muted")));
        var hideForLabelRow = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), Value = true };
        hideForLabelRow.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
        deleteButtonStyle.Triggers.Add(hideForLabelRow);
        factory.SetValue(Button.StyleProperty, deleteButtonStyle);

        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnRemoveRowClick));
        deleteColumn.CellTemplate = new DataTemplate { VisualTree = factory };
        grid.Columns.Add(deleteColumn);
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        var nuevo = new HojaCubicacionItemVm("", "Nuevo ítem", "ml", DefaultColumnCount);
        AttachItem(nuevo);
        items.Add(nuevo);
        RefreshGrandTotal();
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not HojaCubicacionItemVm item) return;
        if (items.Count <= 1) return;

        DetachItem(item);
        items.Remove(item);
        RefreshGrandTotal();
    }

    private void OnToggleCollapseClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.IsCollapsed = !item.IsCollapsed;
    }

    private void OnExpandAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in items) item.IsCollapsed = false;
    }

    private void OnCollapseAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in items) item.IsCollapsed = true;
    }

    private void OnAddRowClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.AddFila();
    }

    private void OnAddColumnClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.AddColumn();
    }

    private void OnRemoveRowClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not HojaCubicacionFilaVm fila) return;
        fila.Owner?.RemoveFila(fila);
    }

    private void OnToggleHelpClick(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBoxBase)
        {
            if (HelpPopup.IsOpen)
            {
                HelpPopup.IsOpen = false;
                e.Handled = true;
                return;
            }
            DialogResult = true;
        }
    }

    private static bool IsFormulaOpenForCellReference(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();
        if (!t.StartsWith("=") && !t.StartsWith("+"))
        {
            if (t.Length > 0)
            {
                char lastCh = t[^1];
                return lastCh is '+' or '-' or '*' or '/' or '^' or '(' or ',' or ';' or ':';
            }
            return false;
        }
        if (t.Length == 1) return true;
        char last = t[^1];
        return last is '=' or '+' or '-' or '*' or '/' or '^' or '%' or '(' or ',' or ';' or ':';
    }

    private void OnSubtotalBadgeClick(object sender, MouseButtonEventArgs e)
    {
        if (!IsPickMode) return;
        if (sender is FrameworkElement elem && elem.Tag is HojaCubicacionItemVm item && item.Subtotal > 0)
        {
            item.IsPicked = true;
            foreach (var f in item.Filas.Where(f => !f.IsLabelRow))
                f.IsPicked = true;

            PickedValue = item.Subtotal;
            DialogResult = true;
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (IsPickMode && PickedValue == null)
        {
            foreach (var kvp in itemGrids)
            {
                if (kvp.Value.SelectedItem is HojaCubicacionFilaVm selectedFila && !selectedFila.IsLabelRow)
                {
                    if (decimal.TryParse(selectedFila.SumaDisplay.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal picked))
                    {
                        selectedFila.IsPicked = true;
                        if (selectedFila.Owner != null)
                            selectedFila.Owner.IsPicked = selectedFila.Owner.Filas.Where(f => !f.IsLabelRow).All(f => f.IsPicked);
                        PickedValue = picked;
                        break;
                    }
                }
            }
        }
        DialogResult = true;
    }
}
