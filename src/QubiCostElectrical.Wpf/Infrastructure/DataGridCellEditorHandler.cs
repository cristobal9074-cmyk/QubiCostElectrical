using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Gestiona los eventos y el ciclo de vida de los editores embebidos (TextBox, ComboBox)
/// dentro de las celdas de un DataGrid en edición.
/// </summary>
internal static class DataGridCellEditorHandler
{
    public static bool LastEditInitiatedByF2 { get; set; }
    public static bool LastEditInitiatedByChar { get; set; }
    public static bool PendingCharEdit { get; set; }

    public static void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (sender is not DataGrid grid || DataGridExcelNavigation.GetHandleCellEditorByHost(grid) || grid.DataContext?.GetType().Name == "HojaCubicacionItemVm")
            return;

        if (e.EditingElement is TextBox textBox)
        {
            textBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
            textBox.PreviewKeyDown += OnEditorPreviewKeyDown;
            textBox.PreviewTextInput -= OnEditorPreviewTextInput;
            textBox.PreviewTextInput += OnEditorPreviewTextInput;

            if (LastEditInitiatedByChar || PendingCharEdit)
            {
                LastEditInitiatedByChar = false;
                textBox.CaretIndex = textBox.Text.Length;
                textBox.SelectionLength = 0;
            }
            else if (LastEditInitiatedByF2)
            {
                LastEditInitiatedByF2 = false;
                textBox.CaretIndex = textBox.Text.Length;
                textBox.SelectionLength = 0;
            }
            else if (e.EditingEventArgs is KeyEventArgs or TextCompositionEventArgs)
            {
                textBox.CaretIndex = textBox.Text.Length;
                textBox.SelectionLength = 0;
            }
            else
            {
                textBox.SelectAll();
            }
        }
        else if (e.EditingElement is ComboBox comboBox)
        {
            comboBox.PreviewKeyDown -= OnComboBoxEditorPreviewKeyDown;
            comboBox.PreviewKeyDown += OnComboBoxEditorPreviewKeyDown;

            if (comboBox.IsEditable)
            {
                comboBox.Loaded -= OnEditableComboBoxLoaded;
                comboBox.Loaded += OnEditableComboBoxLoaded;
                comboBox.DropDownOpened -= OnComboBoxDropDownOpened;
                comboBox.DropDownOpened += OnComboBoxDropDownOpened;

                if (DataGridExcelNavigation.FindVisualChild<TextBox>(comboBox) is { } tb)
                {
                    tb.PreviewKeyDown -= OnEditorPreviewKeyDown;
                    tb.PreviewKeyDown += OnEditorPreviewKeyDown;
                    tb.PreviewTextInput -= OnEditorPreviewTextInput;
                    tb.PreviewTextInput += OnEditorPreviewTextInput;
                    if (PendingCharEdit)
                    {
                        tb.CaretIndex = tb.Text.Length;
                        tb.SelectionLength = 0;
                    }
                }
            }
        }
        else if (e.EditingElement is FrameworkElement fe)
        {
            if (DataGridExcelNavigation.FindVisualChild<TextBox>(fe) is { } innerBox)
            {
                innerBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
                innerBox.PreviewKeyDown += OnEditorPreviewKeyDown;
                innerBox.PreviewTextInput -= OnEditorPreviewTextInput;
                innerBox.PreviewTextInput += OnEditorPreviewTextInput;
                if (LastEditInitiatedByChar || PendingCharEdit)
                {
                    LastEditInitiatedByChar = false;
                    innerBox.CaretIndex = innerBox.Text.Length;
                    innerBox.SelectionLength = 0;
                }
                else if (LastEditInitiatedByF2)
                {
                    LastEditInitiatedByF2 = false;
                    innerBox.CaretIndex = innerBox.Text.Length;
                    innerBox.SelectionLength = 0;
                }
                else if (e.EditingEventArgs is KeyEventArgs or TextCompositionEventArgs)
                {
                    innerBox.CaretIndex = innerBox.Text.Length;
                    innerBox.SelectionLength = 0;
                }
                else
                {
                    innerBox.SelectAll();
                }
            }
        }
    }

    public static void OnEditableComboBoxLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            cb.DropDownOpened -= OnComboBoxDropDownOpened;
            cb.DropDownOpened += OnComboBoxDropDownOpened;

            if (DataGridExcelNavigation.FindVisualChild<TextBox>(cb) is { } tb)
            {
                tb.PreviewKeyDown -= OnEditorPreviewKeyDown;
                tb.PreviewKeyDown += OnEditorPreviewKeyDown;
                tb.PreviewTextInput -= OnEditorPreviewTextInput;
                tb.PreviewTextInput += OnEditorPreviewTextInput;
                if (PendingCharEdit)
                {
                    tb.CaretIndex = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
        }
    }

    public static void OnComboBoxDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox cb)
        {
            if (DataGridExcelNavigation.FindVisualChild<TextBox>(cb) is { } tb)
            {
                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (PendingCharEdit || tb.SelectionLength == tb.Text.Length)
                    {
                        tb.CaretIndex = tb.Text.Length;
                        tb.SelectionLength = 0;
                    }
                }), DispatcherPriority.Input);
            }
        }
    }

    public static void OnEditorPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (PendingCharEdit)
            {
                PendingCharEdit = false;
                if (tb.SelectionLength > 0)
                {
                    tb.CaretIndex = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
            else if (tb.SelectionLength > 0 && tb.SelectionLength == tb.Text.Length && tb.Text.Length > 0)
            {
                tb.CaretIndex = tb.Text.Length;
                tb.SelectionLength = 0;
            }
        }
    }

    public static void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var parentCombo = DataGridExcelNavigation.FindParent<ComboBox>(textBox);
        if (parentCombo != null)
        {
            if (parentCombo.IsDropDownOpen && e.Key is Key.Up or Key.Down)
            {
                return;
            }
            if (parentCombo.IsDropDownOpen && e.Key == Key.Escape)
            {
                parentCombo.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (PendingCharEdit)
        {
            if (e.Key is Key.Back or Key.Delete or Key.Enter or Key.Return or Key.Tab or Key.Escape or Key.Up or Key.Down or Key.Left or Key.Right)
            {
                PendingCharEdit = false;
            }
            else
            {
                PendingCharEdit = false;
                if (textBox.SelectionLength > 0)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                    textBox.SelectionLength = 0;
                }
            }
        }

        var grid = DataGridExcelNavigation.FindParent<DataGrid>(textBox);
        var cell = DataGridExcelNavigation.FindParent<DataGridCell>(textBox);
        if (grid == null)
            return;

        DataGridColumn? column = cell?.Column ?? grid.CurrentCell.Column;
        object? activeRow = cell?.DataContext ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        if (column == null || activeRow == null)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.B or Key.F or Key.D) { e.Handled = true; return; }

        if (e.Key is Key.Enter or Key.Return)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            int offset = backward ? -1 : 1;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            DataGridExcelNavigation.CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridExcelNavigation.MoveToRowOffset(grid, column, offset, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridExcelNavigation.MoveToColumnOffset(grid, column, backward ? -1 : 1, wrapRows: true, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            if (IsFormulaOpenInEditor(textBox.Text))
                return;

            bool ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            DataGridExcelNavigation.CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ctrlPressed)
                    DataGridExcelNavigation.MoveToRowDataEdge(grid, column, activeRow, e.Key == Key.Down ? 1 : -1);
                else
                    DataGridExcelNavigation.MoveToRowOffset(grid, column, e.Key == Key.Down ? 1 : -1, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (IsFormulaOpenInEditor(textBox.Text))
                return;

            if (textBox.SelectionLength == textBox.Text.Length && textBox.Text.Length > 0)
            {
                if (e.Key == Key.Left)
                {
                    textBox.Select(0, 0);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Right)
                {
                    textBox.Select(textBox.Text.Length, 0);
                    e.Handled = true;
                    return;
                }
            }

            bool canNavigate = (e.Key == Key.Left && textBox.SelectionStart == 0 && textBox.SelectionLength == 0)
                || (e.Key == Key.Right && textBox.SelectionStart == textBox.Text.Length && textBox.SelectionLength == 0);

            if (canNavigate)
            {
                SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DataGridExcelNavigation.MoveToColumnOffset(grid, column, e.Key == Key.Right ? 1 : -1, wrapRows: false, activeRow);
                }), DispatcherPriority.Input);
                e.Handled = true;
                return;
            }
        }
    }

    public static void SyncEditorSourceBeforeCommit(TextBox textBox, ComboBox? parentCombo, DataGridColumn column, object? activeRow)
    {
        try
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (parentCombo != null)
            {
                if (!string.Equals(parentCombo.Text, textBox.Text, StringComparison.Ordinal))
                {
                    parentCombo.Text = textBox.Text;
                }
                parentCombo.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                parentCombo.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.UpdateSource();
            }

            if (activeRow is Features.CubicacionSc.CircuitoScRowVm scRow && scRow.CanEdit)
            {
                Features.CubicacionSc.CubicacionScView.CommitCircuitoCellEditor(textBox, parentCombo, column, scRow);
            }
        }
        catch
        {
            // Silencioso ante errores de binding o validaciones transitorias
        }
    }

    public static bool IsFormulaOpenInEditor(string? text)
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

    public static void OnComboBoxEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen)
            return;

        var grid = DataGridExcelNavigation.FindParent<DataGrid>(comboBox);
        var cell = DataGridExcelNavigation.FindParent<DataGridCell>(comboBox);
        if (grid == null)
            return;

        DataGridColumn? column = cell?.Column ?? grid.CurrentCell.Column;
        object? activeRow = cell?.DataContext ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        if (column == null || activeRow == null)
            return;

        if (e.Key is Key.Enter or Key.Return)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            DataGridExcelNavigation.CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridExcelNavigation.MoveToRowOffset(grid, column, backward ? -1 : 1, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridExcelNavigation.MoveToColumnOffset(grid, column, backward ? -1 : 1, wrapRows: true, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }
    }
}
