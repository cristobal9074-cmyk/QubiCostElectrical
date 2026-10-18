using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static partial class DataGridExcelNavigation
{
    private static bool _lastEditInitiatedByF2;
    private static bool _lastEditInitiatedByChar;
    private static bool _pendingCharEdit;

    public static bool TryParseFlexibleDecimal(string? raw, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string clean = raw.Trim();
        clean = clean.Replace("$", "")
                     .Replace("€", "")
                     .Replace("UF", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("CLP", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("%", "")
                     .Trim();

        if (string.IsNullOrWhiteSpace(clean))
            return false;

        int lastDot = clean.LastIndexOf('.');
        int lastComma = clean.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
            clean = lastComma > lastDot ? clean.Replace(".", "").Replace(',', '.') : clean.Replace(",", "");
        else if (lastComma >= 0)
            clean = clean.Replace(',', '.');
        else if (lastDot >= 0 && clean.Count(c => c == '.') > 1)
            clean = clean.Replace(".", "");

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    public static void SetColumnScope(DataGrid grid, Func<DataGridColumn, bool>? predicate)
    {
        if (predicate == null)
        {
            _columnScopes.Remove(grid);
            return;
        }
        var box = _columnScopes.GetOrCreateValue(grid);
        box.Predicate = predicate;
    }

    private static List<DataGridColumn> GetNavigableColumns(DataGrid grid, DataGridColumn? currentColumn)
    {
        var all = grid.Columns.Where(c => c.Visibility == Visibility.Visible && c.Header as string != "#").OrderBy(c => c.DisplayIndex).ToList();
        if (_columnScopes.TryGetValue(grid, out var box) && box.Predicate != null
            && (currentColumn == null || box.Predicate(currentColumn)))
        {
            var scoped = all.Where(box.Predicate).ToList();
            if (scoped.Count > 0)
                return scoped;
        }
        return all;
    }

    public static void Attach(DataGrid grid)
    {
        grid.PreparingCellForEdit += OnPreparingCellForEdit;
        // Como en Excel: empezar a escribir en otra celda cancela el modo "copiado".
        grid.BeginningEdit += (_, _) => ClearCopyMarker(grid);
    }

    // A diferencia de un DataGridTextColumn nativo, un DataGridTemplateColumn NO entra en
    // modo edición automáticamente al escribir sobre una celda seleccionada — limitación
    // conocida de WPF, no un bug de esta app. Sin esto, cualquier columna de plantilla con un
    // TextBox de edición (ej. Sist./Alim./Ducto Ø en Cubic. SC) obliga a F2/doble clic siempre,
    // lento para cargar datos seguidos a pura tecla. Uso: conectar PreviewTextInput del DataGrid
    // a este método (además de PreviewKeyDown → HandlePreviewKeyDown, que sigue manejando
    // Tab/Enter/flechas dentro de la edición ya iniciada). No se llama desde Attach() a
    // propósito: es opt-in por vista, así Cubicación/Itemizado (que no la necesitan, sus
    // columnas editables son DataGridTextColumn nativas) no cambian de comportamiento.
    public static bool TryBeginEditFromChar(DataGrid grid, string typedText)
    {
        if (string.IsNullOrEmpty(typedText) || char.IsControl(typedText[0]))
            return false;
        if (IsEditing(grid))
            return false;

        var column = grid.CurrentCell.Column;
        if (column == null || column.IsReadOnly || grid.IsReadOnly)
            return false;

        _lastEditInitiatedByChar = true;
        _pendingCharEdit = true;
        try
        {
            if (!grid.BeginEdit())
            {
                _lastEditInitiatedByChar = false;
                _pendingCharEdit = false;
                return false;
            }
        }
        catch
        {
            _lastEditInitiatedByChar = false;
            _pendingCharEdit = false;
            throw;
        }

        var cell = GetCurrentCell(grid);
        // Forzar el layout: BeginEdit deja IsEditing=true de inmediato, pero la plantilla de
        // edición (y su TextBox) puede no estar generada todavía en el mismo tick.
        cell?.UpdateLayout();
        if (cell != null && FindVisualChild<TextBox>(cell) is { } textBox)
        {
            textBox.Focus();
            textBox.SetCurrentValue(TextBox.TextProperty, typedText);
            textBox.CaretIndex = textBox.Text.Length;
            textBox.SelectionLength = 0;
            textBox.PreviewTextInput -= OnEditorPreviewTextInput;
            textBox.PreviewTextInput += OnEditorPreviewTextInput;
            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_pendingCharEdit || textBox.SelectionLength > 0)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                    textBox.SelectionLength = 0;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
            return true;
        }
        if (cell != null && FindVisualChild<ComboBox>(cell) is { } comboBox)
        {
            comboBox.Focus();
            if (FindVisualChild<TextBox>(comboBox) is { } innerBox)
            {
                innerBox.SetCurrentValue(TextBox.TextProperty, typedText);
                innerBox.CaretIndex = typedText.Length;
                innerBox.SelectionLength = 0;
                innerBox.PreviewTextInput -= OnEditorPreviewTextInput;
                innerBox.PreviewTextInput += OnEditorPreviewTextInput;
                innerBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_pendingCharEdit || innerBox.SelectionLength > 0)
                    {
                        innerBox.CaretIndex = innerBox.Text.Length;
                        innerBox.SelectionLength = 0;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                comboBox.SetCurrentValue(ComboBox.TextProperty, typedText);
            }
            comboBox.IsDropDownOpen = true;
            return true;
        }

        return false;
    }

    public static void HandlePreviewKeyDown(DataGrid grid, KeyEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && FindParent<DataGridColumnHeader>(dep) != null)
            return;

        if (e.Key == Key.Escape)
        {
            if (e.OriginalSource is TextBox || (e.OriginalSource is DependencyObject d && FindParent<TextBox>(d) != null))
                return;

            grid.CancelEdit();
            ClearCopyMarker(grid);
            e.Handled = true;
            return;
        }

        bool editing = IsEditing(grid);

        if (!editing)
        {
            if (e.Key == Key.F2)
            {
                _lastEditInitiatedByF2 = true;
                if (grid.CurrentCell.Column == null)
                {
                    var firstEditableCol = grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                        ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                    if (firstEditableCol != null && grid.SelectedItem != null)
                        grid.CurrentCell = new DataGridCellInfo(grid.SelectedItem, firstEditableCol);
                }
                grid.BeginEdit();
                e.Handled = true;
                return;
            }

            // Ctrl+C: copiar celdas seleccionadas a portapapeles (formato TSV / Excel)
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopySelectionToClipboard(grid);
                e.Handled = true;
                return;
            }

            // Ctrl+V: si el dueño declaró HandlePasteByHost, su pegado con lógica de negocio manda
            // (código → material, fórmulas, sectores) y el evento se deja sin manejar.
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && !grid.IsReadOnly)
            {
                if (GetHandlePasteByHost(grid))
                    return;

                string clipText = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                if (!string.IsNullOrEmpty(clipText) && TryPasteIntoSelection(grid, clipText))
                    e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter or Key.Return or Key.Tab)
            {
                if (!grid.CurrentCell.IsValid || grid.CurrentCell.Column == null)
                {
                    if (grid.Items.Count > 0)
                    {
                        var targetRow = grid.SelectedItem ?? grid.Items[0];
                        var targetCol = grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                                     ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                        if (targetCol != null)
                        {
                            grid.CurrentCell = new DataGridCellInfo(targetRow, targetCol);
                            DataGridSelection.SeleccionarFila(grid, targetRow, targetCol);
                        }
                    }
                }

                var currentColumn = grid.CurrentCell.Column
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                    ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                object? currentItem = grid.CurrentCell.Item ?? grid.SelectedItem;

                if (currentColumn != null && currentItem != null)
                {
                    if (e.Key is Key.Enter or Key.Return)
                    {
                        MoveToRowOffset(grid, currentColumn, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1, currentItem);
                    }
                    else
                    {
                        MoveToColumnOffset(grid, currentColumn, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1, wrapRows: true, currentItem);
                    }
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+A: seleccionar todas las celdas visibles (Excel style)
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SelectAll(grid);
                e.Handled = true;
                return;
            }

            // Ctrl+D / Ctrl+R: rellenar hacia abajo / hacia la derecha (estilo Excel).
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.D or Key.R)
            {
                if (grid.SelectionUnit == DataGridSelectionUnit.FullRow)
                {
                    if (grid.SelectedItems.Count > 1) { e.Handled = true; return; }
                }
                else
                {
                    int count = grid.SelectedCells.Count;
                    if (count is >= 2 and <= 50 && FillSelection(grid, vertical: e.Key == Key.D)) { e.Handled = true; return; }
                    if (count > 1 || grid.SelectedItems.Count > 1) { e.Handled = true; return; }
                }
            }

            // Ctrl+B / Ctrl+F: Buscar en la grilla (bloquea ToggleBold nativo sobre celdas seleccionadas)
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.B or Key.F)
            {
                e.Handled = true;
                if (ApplicationCommands.Find.CanExecute(null, grid))
                    ApplicationCommands.Find.Execute(null, grid);
                else if (FindParent<Controls.IInGridSearchable>(grid) is { } searchable)
                    searchable.OpenGridSearch();
                return;
            }

            // Suprimir comandos de texto enriquecido nativos de WPF que bloquean la UI en selecciones masivas
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.I or Key.U)
            {
                e.Handled = true;
                return;
            }

            // Delete / Backspace: borrar el contenido de toda la selección de inmediato.
            // Celda viva (TextBox embebido en la celda, p. ej. fórmula de Plantillas): el
            // foco está en el editor de texto — que borre su propio contenido; la fila
            // entera (Descripción incluida) jamás.
            if (e.Key is Key.Delete or Key.Back)
            {
                if (e.OriginalSource is TextBox)
                {
                    return;
                }

                if (GetHandleDeleteByHost(grid))
                {
                    return;
                }

                if (grid.SelectedCells.Any(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly))
                {
                    ClearSelection(grid);
                    e.Handled = true;
                    return;
                }
            }

            // Teclas con flechas mueven / extienden la selección (estilo Excel).
            // Depende solo de grid.CurrentCell (dato puro), NO del contenedor visual de la
            // celda: justo después de un Shift+Flecha que arma una selección grande, el
            // contenedor de la celda vecina podía no estar realizado todavía (virtualización),
            // así que el chequeo con el contenedor visual fallaba, e.Handled quedaba en false,
            // y el evento caía al manejo nativo de WPF — que con una selección multi-celda
            // armada a mano saltaba a la última casilla de la columna vecina en vez de mantener
            // la misma fila. Por eso "moverse al lado tras seleccionar con Shift" se sentía roto.
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down &&
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.None)
            {
                // Celda viva (TextBox embebido en la celda): flechas mueven el cursor del
                // texto, no navegan la grilla.
                if (e.OriginalSource is TextBox)
                {
                    return;
                }

                if (!grid.CurrentCell.IsValid || grid.CurrentCell.Column == null)
                {
                    if (grid.Items.Count > 0)
                    {
                        var targetRow = grid.SelectedItem ?? grid.Items[0];
                        var targetCol = grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                                     ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                        if (targetCol != null)
                        {
                            grid.CurrentCell = new DataGridCellInfo(targetRow, targetCol);
                            DataGridSelection.SeleccionarFila(grid, targetRow, targetCol);
                        }
                    }
                }

                if (grid.CurrentCell.IsValid && grid.CurrentCell.Column != null)
                {
                    e.Handled = true;
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                    if (shift)
                    {
                        ExtendSelection(grid, e.Key, toEdge: ctrl);
                    }
                    else if (ctrl)
                    {
                        MoveToEdge(grid, e.Key);
                    }
                    else
                    {
                        int offsetCol = e.Key == Key.Right ? 1 : (e.Key == Key.Left ? -1 : 0);
                        int offsetRow = e.Key == Key.Down ? 1 : (e.Key == Key.Up ? -1 : 0);

                        if (offsetCol != 0)
                        {
                            MoveToColumnOffset(grid, grid.CurrentCell.Column, offsetCol, wrapRows: false);
                        }
                        else if (offsetRow != 0)
                        {
                            MoveToRowOffset(grid, grid.CurrentCell.Column, offsetRow);
                        }
                    }
                }
            }

            // Home / End → primera / última columna del alcance activo (en Cubicación, con una
            // casilla de sector seleccionada, eso es la primera/última casilla de sector, no
            // Código/Descripción/Unidad/Cantidad — mismo alcance que ya usa Shift+flecha/Ctrl+E
            // para no arrastrar datos del material al operar cantidades).
            // Ctrl+Home / Ctrl+End → primera celda de la grilla / última celda de la última fila
            // (igual que Excel).
            if (e.Key is Key.Home or Key.End &&
                (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
            {
                // Celda viva (TextBox embebido): Home/End = inicio/fin de la línea del texto.
                if (e.OriginalSource is TextBox)
                {
                    return;
                }

                e.Handled = true;
                if (grid.CurrentCell.IsValid)
                {
                    bool isCtrl = Keyboard.Modifiers == ModifierKeys.Control;
                    // Ctrl+Home/Ctrl+End es "ir a A1"/"ir a la última celda" de Excel: ignora el
                    // alcance de columnas a propósito. Home/End sin modificador respeta el
                    // alcance activo (p. ej. las casillas de sector en Cubicación).
                    var visibleColumns = isCtrl
                        ? grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList()
                        : GetNavigableColumns(grid, grid.CurrentCell.Column);
                    if (visibleColumns.Count > 0 && grid.Items.Count > 0)
                    {
                        bool toStart = e.Key == Key.Home;
                        var targetCol = toStart ? visibleColumns.First() : visibleColumns.Last();
                        int rowIndex;
                        if (isCtrl)
                            rowIndex = toStart ? 0 : grid.Items.Count - 1;
                        else
                            rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);

                        if (rowIndex >= 0)
                            FocusCell(grid, rowIndex, targetCol);
                    }
                }
            }
        }
    }

    private static void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox textBox)
        {
            textBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
            textBox.PreviewKeyDown += OnEditorPreviewKeyDown;
            textBox.PreviewTextInput -= OnEditorPreviewTextInput;
            textBox.PreviewTextInput += OnEditorPreviewTextInput;

            if (_lastEditInitiatedByChar || _pendingCharEdit)
            {
                _lastEditInitiatedByChar = false;
                textBox.CaretIndex = textBox.Text.Length;
                textBox.SelectionLength = 0;
            }
            else if (_lastEditInitiatedByF2)
            {
                _lastEditInitiatedByF2 = false;
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

                if (FindVisualChild<TextBox>(comboBox) is { } tb)
                {
                    tb.PreviewKeyDown -= OnEditorPreviewKeyDown;
                    tb.PreviewKeyDown += OnEditorPreviewKeyDown;
                    tb.PreviewTextInput -= OnEditorPreviewTextInput;
                    tb.PreviewTextInput += OnEditorPreviewTextInput;
                    if (_pendingCharEdit)
                    {
                        tb.CaretIndex = tb.Text.Length;
                        tb.SelectionLength = 0;
                    }
                }
            }
        }
        else if (e.EditingElement is FrameworkElement fe)
        {
            if (FindVisualChild<TextBox>(fe) is { } innerBox)
            {
                innerBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
                innerBox.PreviewKeyDown += OnEditorPreviewKeyDown;
                innerBox.PreviewTextInput -= OnEditorPreviewTextInput;
                innerBox.PreviewTextInput += OnEditorPreviewTextInput;
                if (_lastEditInitiatedByChar || _pendingCharEdit)
                {
                    _lastEditInitiatedByChar = false;
                    innerBox.CaretIndex = innerBox.Text.Length;
                    innerBox.SelectionLength = 0;
                }
                else if (_lastEditInitiatedByF2)
                {
                    _lastEditInitiatedByF2 = false;
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

    private static void OnEditableComboBoxLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            cb.DropDownOpened -= OnComboBoxDropDownOpened;
            cb.DropDownOpened += OnComboBoxDropDownOpened;

            if (FindVisualChild<TextBox>(cb) is { } tb)
            {
                tb.PreviewKeyDown -= OnEditorPreviewKeyDown;
                tb.PreviewKeyDown += OnEditorPreviewKeyDown;
                tb.PreviewTextInput -= OnEditorPreviewTextInput;
                tb.PreviewTextInput += OnEditorPreviewTextInput;
                if (_pendingCharEdit)
                {
                    tb.CaretIndex = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
        }
    }

    private static void OnComboBoxDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox cb)
        {
            if (FindVisualChild<TextBox>(cb) is { } tb)
            {
                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_pendingCharEdit || tb.SelectionLength == tb.Text.Length)
                    {
                        tb.CaretIndex = tb.Text.Length;
                        tb.SelectionLength = 0;
                    }
                }), DispatcherPriority.Input);
            }
        }
    }

    private static void OnEditorPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (_pendingCharEdit)
            {
                _pendingCharEdit = false;
                if (tb.SelectionLength > 0)
                {
                    tb.CaretIndex = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
            else if (tb.SelectionLength > 0 && tb.SelectionLength == tb.Text.Length && tb.Text.Length > 0)
            {
                // Si WPF seleccionó todo el texto debido a foco o apertura de popup,
                // evitamos que el tecleo de texto sobrescriba lo escrito.
                tb.CaretIndex = tb.Text.Length;
                tb.SelectionLength = 0;
            }
        }
    }

    private static void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var parentCombo = FindParent<ComboBox>(textBox);
        if (parentCombo != null)
        {
            // Si el desplegable del ComboBox está abierto, permitir navegar opciones con Up/Down o cerrar con Escape
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

        if (_pendingCharEdit)
        {
            if (e.Key is Key.Back or Key.Delete or Key.Enter or Key.Return or Key.Tab or Key.Escape or Key.Up or Key.Down or Key.Left or Key.Right)
            {
                _pendingCharEdit = false;
            }
            else
            {
                _pendingCharEdit = false;
                if (textBox.SelectionLength > 0)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                    textBox.SelectionLength = 0;
                }
            }
        }

        var grid = FindParent<DataGrid>(textBox);
        var cell = FindParent<DataGridCell>(textBox);
        if (grid == null)
            return;

        DataGridColumn? column = cell?.Column ?? grid.CurrentCell.Column;
        object? activeRow = cell?.DataContext ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        if (column == null || activeRow == null)
            return;

        // Bloquear atajos globales dentro del editor de celda
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.B or Key.F or Key.D) { e.Handled = true; return; }

        // Enter: confirmar y mover a la fila de abajo (o arriba con Shift)
        if (e.Key is Key.Enter or Key.Return)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            int offset = backward ? -1 : 1;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                MoveToRowOffset(grid, column, offset, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        // Tab / Shift+Tab: confirmar y mover columna izquierda/derecha, saltando a la fila
        // siguiente/anterior al llegar al final — igual que Excel.
        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                MoveToColumnOffset(grid, column, backward ? -1 : 1, wrapRows: true, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        // Up / Down en edición: confirmar y mover arriba/abajo.
        // Si el usuario está escribiendo una fórmula abierta (ej. '=C1*' o '='), no confirmar
        // para permitir el Point Mode de selección de celdas con flechas.
        if (e.Key is Key.Up or Key.Down)
        {
            if (IsFormulaOpenInEditor(textBox.Text))
                return;

            bool ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            SyncEditorSourceBeforeCommit(textBox, parentCombo, column, activeRow);
            CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ctrlPressed)
                    MoveToRowDataEdge(grid, column, activeRow, e.Key == Key.Down ? 1 : -1);
                else
                    MoveToRowOffset(grid, column, e.Key == Key.Down ? 1 : -1, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        // Left / Right en edición: confirmar y mover a la celda izquierda/derecha
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
                    MoveToColumnOffset(grid, column, e.Key == Key.Right ? 1 : -1, wrapRows: false, activeRow);
                }), DispatcherPriority.Input);
                e.Handled = true;
                return;
            }
        }
    }

    private static void SyncEditorSourceBeforeCommit(TextBox textBox, ComboBox? parentCombo, DataGridColumn column, object? activeRow)
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

    private static bool IsFormulaOpenInEditor(string? text)
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

    private static void OnComboBoxEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen)
            return;

        var grid = FindParent<DataGrid>(comboBox);
        var cell = FindParent<DataGridCell>(comboBox);
        if (grid == null)
            return;

        DataGridColumn? column = cell?.Column ?? grid.CurrentCell.Column;
        object? activeRow = cell?.DataContext ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        if (column == null || activeRow == null)
            return;

        if (e.Key is Key.Enter or Key.Return)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CommitCellAndRow(grid);
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                MoveToRowOffset(grid, column, backward ? -1 : 1, activeRow);
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
                MoveToColumnOffset(grid, column, backward ? -1 : 1, wrapRows: true, activeRow);
            }), DispatcherPriority.Input);
            e.Handled = true;
            return;
        }
    }

    // Confirma la celda y, además, la fila.
    internal static void CommitCellAndRow(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    internal static void MoveToRowOffset(DataGrid grid, DataGridColumn column, int rowOffset, object? currentRowItem = null)
    {
        object? item = currentRowItem ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        int rowIndex = item != null ? grid.Items.IndexOf(item) : -1;
        if (rowIndex < 0)
            rowIndex = grid.SelectedIndex;
        if (rowIndex < 0)
            return;

        int targetRow = rowIndex + rowOffset;
        if (targetRow < 0 || targetRow >= grid.Items.Count)
            return;

        FocusCell(grid, targetRow, column);
    }

    // Ctrl+Arriba/Abajo estilo Excel real: salta al borde del bloque de datos ENTRE FILAS de
    // esa misma columna (a la próxima celda con contenido si la actual está vacía, o al final
    // del bloque contiguo si la actual tiene contenido y sigue habiendo datos a continuación) —
    // misma regla que Ctrl+Izquierda/Derecha, aplicada verticalmente. currentRowItem se recibe
    private static int FindRowDataEdgeIndex(DataGrid grid, DataGridColumn column, int currentRow, int direction)
    {
        int max = grid.Items.Count;
        if (currentRow < 0 || currentRow >= max)
            return currentRow;

        bool HasContent(int i) => i >= 0 && i < max && !string.IsNullOrWhiteSpace(GetCellText(grid.Items[i], column));

        int next = currentRow + direction;
        if (next < 0) return 0;
        if (next >= max) return max - 1;

        if (HasContent(currentRow) && HasContent(next))
        {
            int i = next;
            while (i + direction >= 0 && i + direction < max && HasContent(i + direction))
                i += direction;
            return i;
        }
        else
        {
            int j = next;
            while (j >= 0 && j < max && !HasContent(j))
                j += direction;
            return j < 0 ? 0 : j >= max ? max - 1 : j;
        }
    }

    private static int FindColumnDataEdgeIndex(DataGrid grid, List<DataGridColumn> columns, object rowItem, int currentCol, int direction)
    {
        int max = columns.Count;
        if (currentCol < 0 || currentCol >= max)
            return currentCol;

        bool HasContent(int c) => c >= 0 && c < max && !string.IsNullOrWhiteSpace(GetCellText(rowItem, columns[c]));

        int next = currentCol + direction;
        if (next < 0) return 0;
        if (next >= max) return max - 1;

        if (HasContent(currentCol) && HasContent(next))
        {
            int i = next;
            while (i + direction >= 0 && i + direction < max && HasContent(i + direction))
                i += direction;
            return i;
        }
        else
        {
            int j = next;
            while (j >= 0 && j < max && !HasContent(j))
                j += direction;
            return j < 0 ? 0 : j >= max ? max - 1 : j;
        }
    }

    // explícito (no se relee grid.CurrentItem) para no depender de que el cursor de la vista de
    // colección siga sincronizado con la celda que se acaba de editar.
    private static void MoveToRowDataEdge(DataGrid grid, DataGridColumn column, object? currentRowItem, int direction)
    {
        int currentRow = currentRowItem != null ? grid.Items.IndexOf(currentRowItem) : -1;
        if (currentRow < 0)
            return;

        int target = FindRowDataEdgeIndex(grid, column, currentRow, direction);
        FocusCell(grid, target, column);
    }

    internal static void MoveToColumnOffset(DataGrid grid, DataGridColumn currentColumn, int columnOffset, bool wrapRows, object? currentRowItem = null)
    {
        object? item = currentRowItem ?? grid.CurrentCell.Item ?? grid.SelectedItem;
        int rowIndex = item != null ? grid.Items.IndexOf(item) : -1;
        if (rowIndex < 0)
            rowIndex = grid.SelectedIndex;
        if (rowIndex < 0)
            return;

        var columns = wrapRows
            ? grid.Columns.Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly).OrderBy(c => c.DisplayIndex).ToList()
            : GetNavigableColumns(grid, currentColumn);
        if (columns.Count == 0)
            return;

        int targetRow = rowIndex;
        int targetIndex;

        int currentIndex = columns.IndexOf(currentColumn);
        if (currentIndex < 0)
        {
            // La columna actual es de solo lectura (no está en columns).
            if (columnOffset > 0)
            {
                // Avanzando (Tab o Flecha Derecha): buscar la primera columna editable a la derecha
                targetIndex = columns.FindIndex(c => c.DisplayIndex > currentColumn.DisplayIndex);
                if (targetIndex < 0)
                {
                    if (!wrapRows)
                        return;
                    targetRow++;
                    targetIndex = 0;
                }
            }
            else if (columnOffset < 0)
            {
                // Retrocediendo (Shift+Tab o Flecha Izquierda): buscar la última columna editable a la izquierda
                targetIndex = columns.FindLastIndex(c => c.DisplayIndex < currentColumn.DisplayIndex);
                if (targetIndex < 0)
                {
                    if (!wrapRows)
                        return;
                    targetRow--;
                    targetIndex = columns.Count - 1;
                }
            }
            else
            {
                targetIndex = 0;
            }
        }
        else
        {
            targetIndex = currentIndex + columnOffset;

            if (targetIndex < 0)
            {
                if (!wrapRows)
                    return;
                targetRow--;
                targetIndex = columns.Count - 1;
            }
            else if (targetIndex >= columns.Count)
            {
                if (!wrapRows)
                    return;
                targetRow++;
                targetIndex = 0;
            }
        }

        if (targetRow < 0 || targetRow >= grid.Items.Count)
            return;

        FocusCell(grid, targetRow, columns[targetIndex]);
    }

    public static void FocusCell(DataGrid grid, int rowIndex, DataGridColumn column)
    {
        // Defensivo: con columnas que se ocultan/muestran por Visibility (en vez de
        // agregarse/quitarse de Columns), WPF puede dejar el DisplayIndex de la columna
        // "current" en -1 tras un cambio de modo — CurrentCell = ... dispara
        // DataGrid.ScrollCellIntoView -> DataGridCellsPanel.BringIndexIntoView(-1), que lanza
        // ArgumentOutOfRangeException y tira abajo toda la UI. Sin esto, cualquier fila/columna
        // fuera de rango revienta la app entera en vez de simplemente no mover el foco.
        if (rowIndex < 0 || rowIndex >= grid.Items.Count)
            return;
        if (column == null || column.DisplayIndex < 0 || column.Visibility != Visibility.Visible)
            return;

        var item = grid.Items[rowIndex];
        DataGridSelection.SeleccionarFila(grid, item, column);

        grid.CurrentCell = new DataGridCellInfo(item, column);

        // Cualquier movimiento de celda "simple" (sin Shift) reinicia el ancla de selección,
        // para que el próximo Shift+Flecha extienda el rectángulo desde aquí.
        _anchors.Remove(grid);
        _anchors.Add(grid, new AnchorBox { Anchor = grid.CurrentCell });

        // Si la celda destino YA está realizada en pantalla (el caso normal al moverse de a una
        // celda con las flechas dentro de la ventana visible), enfocarla directo y listo — antes
        // se llamaba SIEMPRE a ScrollIntoView + un foco asíncrono con reintentos por Dispatcher,
        // lo que fuerza un relayout/scroll en CADA tecla aunque la celda ya estuviera a la
        // vista, y se sentía como un "recargue" visible en vez de una navegación fluida. Solo se
        // trae a la vista (con el costo de scroll + foco diferido) cuando realmente hace falta.
        grid.Focus();
        Keyboard.Focus(grid);

        var realizedCell = TryGetRealizedCell(grid, item, column);
        if (realizedCell != null)
        {
            realizedCell.IsSelected = true;
            realizedCell.Focus();
            Keyboard.Focus(realizedCell);
            return;
        }

        grid.ScrollIntoView(item, column);
        TryFocus(grid, item, column, 4);
    }

    private static DataGridCell? TryGetRealizedCell(DataGrid grid, object item, DataGridColumn column)
    {
        var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
        if (row == null)
            return null;

        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null)
            return null;

        int colIndex = grid.Columns.IndexOf(column);
        if (colIndex < 0)
            return null;

        return presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as DataGridCell;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo?> _beginBatchCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo?> _endBatchCache = new();

    internal static void BeginBatchUpdate(object? item)
    {
        if (item is { } it)
            _beginBatchCache.GetOrAdd(it.GetType(), t => t.GetMethod("BeginBatchUpdate"))?.Invoke(it, null);
    }

    internal static void EndBatchUpdate(object? item)
    {
        if (item is { } it)
            _endBatchCache.GetOrAdd(it.GetType(), t => t.GetMethod("EndBatchUpdate"))?.Invoke(it, null);
    }

    private static void SelectAll(DataGrid grid)
    {
        if (grid.SelectionUnit == DataGridSelectionUnit.FullRow)
            grid.SelectAll();
        else
            grid.SelectAllCells();
    }

    // Extiende el rectángulo de selección desde el ancla fija hasta una celda un paso más
    // allá en la dirección de la flecha (o hasta el borde de la grilla si toEdge=true, para
    // Ctrl+Shift+Flecha).
    private static void ExtendSelection(DataGrid grid, Key key, bool toEdge)
    {
        var columns = GetNavigableColumns(grid, grid.CurrentCell.Column);
        if (columns.Count == 0 || grid.Items.Count == 0)
            return;

        int rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
        int colIndex = columns.IndexOf(grid.CurrentCell.Column);
        if (rowIndex < 0 || colIndex < 0)
            return;

        int targetRow = rowIndex;
        int targetCol = colIndex;

        if (toEdge)
        {
            switch (key)
            {
                case Key.Left: targetCol = FindColumnDataEdgeIndex(grid, columns, grid.CurrentCell.Item, colIndex, -1); break;
                case Key.Right: targetCol = FindColumnDataEdgeIndex(grid, columns, grid.CurrentCell.Item, colIndex, 1); break;
                case Key.Up: targetRow = FindRowDataEdgeIndex(grid, grid.CurrentCell.Column, rowIndex, -1); break;
                case Key.Down: targetRow = FindRowDataEdgeIndex(grid, grid.CurrentCell.Column, rowIndex, 1); break;
            }
        }
        else
        {
            switch (key)
            {
                case Key.Left: targetCol = Math.Max(0, colIndex - 1); break;
                case Key.Right: targetCol = Math.Min(columns.Count - 1, colIndex + 1); break;
                case Key.Up: targetRow = Math.Max(0, rowIndex - 1); break;
                case Key.Down: targetRow = Math.Min(grid.Items.Count - 1, rowIndex + 1); break;
            }
        }

        var targetItem = grid.Items[targetRow];
        var targetColumn = columns[targetCol];

        var anchorBox = _anchors.GetOrCreateValue(grid);
        if (!anchorBox.Anchor.IsValid)
            anchorBox.Anchor = grid.CurrentCell;

        int anchorRow = grid.Items.IndexOf(anchorBox.Anchor.Item);
        int anchorCol = columns.IndexOf(anchorBox.Anchor.Column);
        if (anchorRow < 0) anchorRow = rowIndex;
        if (anchorCol < 0) anchorCol = colIndex;

        int rowMin = Math.Min(anchorRow, targetRow), rowMax = Math.Max(anchorRow, targetRow);
        int colMin = Math.Min(anchorCol, targetCol), colMax = Math.Max(anchorCol, targetCol);

        grid.SelectedCells.Clear();
        for (int r = rowMin; r <= rowMax; r++)
        {
            var item = grid.Items[r];
            for (int c = colMin; c <= colMax; c++)
                grid.SelectedCells.Add(new DataGridCellInfo(item, columns[c]));
        }

        grid.CurrentCell = new DataGridCellInfo(targetItem, targetColumn);
        grid.ScrollIntoView(targetItem, targetColumn);
        TryFocus(grid, targetItem, targetColumn, 4);
    }

    // Ctrl+Flecha (sin Shift): salta al borde de la grilla en esa dirección, como Excel.
    private static void MoveToEdge(DataGrid grid, Key key)
    {
        var columns = GetNavigableColumns(grid, grid.CurrentCell.Column);
        if (columns.Count == 0 || grid.Items.Count == 0)
            return;

        object currentItem = grid.CurrentCell.Item;
        int rowIndex = grid.Items.IndexOf(currentItem);
        int colIndex = columns.IndexOf(grid.CurrentCell.Column);
        if (rowIndex < 0 || colIndex < 0)
            return;

        switch (key)
        {
            case Key.Left:
                {
                    int target = FindColumnDataEdgeIndex(grid, columns, currentItem, colIndex, -1);
                    FocusCell(grid, rowIndex, columns[target]);
                    break;
                }
            case Key.Right:
                {
                    int target = FindColumnDataEdgeIndex(grid, columns, currentItem, colIndex, 1);
                    FocusCell(grid, rowIndex, columns[target]);
                    break;
                }
            // Estilo Excel real: salta al borde del bloque de datos de esa columna (a la
            // próxima fila con contenido, o al final del bloque contiguo), no al literal
            // primer/último registro de la grilla.
            case Key.Up: MoveToRowDataEdge(grid, columns[colIndex], currentItem, -1); break;
            case Key.Down: MoveToRowDataEdge(grid, columns[colIndex], currentItem, 1); break;
        }
    }

    // ── Lectura/escritura genérica de celdas por reflexión ─────────────────────────────
    // Soporta tanto propiedades simples (string/decimal) como cualquier indexador
    // "NombreDeLista[n]" (ej. "SectorValues[n]" en la matriz de sectores de Cubicación,
    // "Valores[n]" en la Hoja de Cubicación) — en ese caso, escribir en la lista dispara el
    // mismo pipeline de negocio (recálculo, undo, validación) que ya usa la edición manual de
    // celda, porque esas listas son ObservableCollection<string> observadas por el ViewModel.
    public static string GetCellText(object? dataContext, DataGridColumn? column)
    {
        if (dataContext == null || column == null)
            return string.Empty;

        if (!TryGetBoundPropertyName(column, out var propertyName))
            return string.Empty;

        if (TryGetIndexedList(dataContext, propertyName, out var list, out var index))
            return list != null && index >= 0 && index < list.Count ? list[index] ?? "" : "";

        var prop = GetCachedProperty(dataContext.GetType(), propertyName);
        return prop?.GetValue(dataContext)?.ToString() ?? "";
    }

    public static void SetCellText(object? dataContext, DataGridColumn? column, string value)
    {
        if (dataContext == null || column == null)
            return;

        if (!TryGetBoundPropertyName(column, out var propertyName))
            return;

        if (TryGetIndexedList(dataContext, propertyName, out var list, out var index))
        {
            if (list != null && index >= 0 && index < list.Count)
                list[index] = value;
            return;
        }

        var prop = GetCachedProperty(dataContext.GetType(), propertyName);
        if (prop == null || !prop.CanWrite)
            return;

        Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (propType == typeof(string))
        {
            prop.SetValue(dataContext, value);
        }
        else if (propType == typeof(decimal))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, d);
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(decimal?) ? null : 0m);
        }
        else if (propType == typeof(double))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, (double)d);
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(double?) ? null : 0.0);
        }
        else if (propType == typeof(int))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, (int)Math.Round(d));
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(int?) ? null : 0);
        }
        else if (propType == typeof(bool))
        {
            if (bool.TryParse(value, out var b))
                prop.SetValue(dataContext, b);
            else if (value is "1" or "si" or "sí" or "true" or "yes" or "TRUE" or "SI")
                prop.SetValue(dataContext, true);
            else if (value is "0" or "no" or "false" or "FALSE" or "NO")
                prop.SetValue(dataContext, false);
        }
    }

    // DataGridBoundColumn.Binding o ClipboardContentBinding (TemplateColumns de conductor/ducto).
    private static bool TryGetBoundPropertyName(DataGridColumn column, out string propertyName)
    {
        propertyName = "";
        Binding? binding = null;
        if (column is DataGridBoundColumn boundColumn)
            binding = boundColumn.Binding as Binding;
        binding ??= column.ClipboardContentBinding as Binding;
        if (binding?.Path?.Path is not { Length: > 0 } path)
            return false;
        propertyName = path;
        return true;
    }

    // Cachea las búsquedas de PropertyInfo por (tipo, nombre) — sin esto, copiar/pegar/limpiar
    // sobre una fila con muchos sectores repite Type.GetProperty por cada celda tocada, lo que
    // se siente lento en filas anchas.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), System.Reflection.PropertyInfo?> _propertyCache = new();

    private static System.Reflection.PropertyInfo? GetCachedProperty(Type type, string name)
        => _propertyCache.GetOrAdd((type, name), key => key.Item1.GetProperty(key.Item2));

    private static bool TryGetIndexedList(object dataContext, string propertyPath, out ObservableCollection<string>? list, out int index)
    {
        list = null;
        index = -1;
        int bracketIndex = propertyPath.IndexOf('[');
        if (bracketIndex <= 0)
            return false;

        string listPropertyName = propertyPath.Substring(0, bracketIndex);
        list = GetCachedProperty(dataContext.GetType(), listPropertyName)?.GetValue(dataContext) as ObservableCollection<string>;
        index = ParseIndexerIndex(propertyPath);
        return true;
    }

    private static int ParseIndexerIndex(string propertyPath)
    {
        var parts = propertyPath.Split('[', ']');
        return parts.Length > 1 && int.TryParse(parts[1], out int index) ? index : -1;
    }

    // ── Rellenar (Ctrl+D / Ctrl+R, estilo "fill handle" de Excel) ──────────────────────
    // Devuelve true si al menos un grupo tenía 2+ celdas y se aplicó el relleno.
    private static bool FillSelection(DataGrid grid, bool vertical)
    {
        if (grid.SelectedCells.Count is < 2 or > 50)
            return false;
        int totalRows = grid.Items.Count;
        if (totalRows <= 1)
            return false;
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (cells.Count < 2)
            return false;

        var distinctRows = cells.Select(c => c.Item).Distinct().ToList();
        var distinctCols = cells.Select(c => c.Column).Distinct().ToList();

        // SALVAGUARDA ESTRICTA ANTICONGELAMIENTO:
        // Si toda la grilla está seleccionada (ej. tras Ctrl+A), o si la selección abarca todas las filas,
        // o si involucra más de 2 columnas distintas o más de 50 celdas, NO aplicar autorrelleno.
        // Previene la destrucción masiva de datos y cuelgues del Dispatcher.
        if (distinctRows.Count >= totalRows || distinctCols.Count > 2 || cells.Count > 50)
            return false;

        bool filledAny = false;
        var indexByItem = new Dictionary<object, int>(totalRows);
        for (int i = 0; i < totalRows; i++)
            if (grid.Items[i] is { } it && !indexByItem.ContainsKey(it)) indexByItem[it] = i;

        var affectedItems = distinctRows;
        foreach (var item in affectedItems)
            BeginBatchUpdate(item);

        try
        {
            if (vertical)
            {
                foreach (var group in cells.GroupBy(c => c.Column))
                {
                    var ordered = group
                        .Select(c => (Item: c.Item, RowIndex: (c.Item != null && indexByItem.TryGetValue(c.Item, out int idx)) ? idx : -1))
                        .Where(t => t.RowIndex >= 0)
                        .OrderBy(t => t.RowIndex)
                        .Select(t => (t.Item, group.Key))
                        .ToList();
                    if (FillAlongSequence(ordered))
                        filledAny = true;
                }
            }
            else
            {
                foreach (var group in cells.GroupBy(c => c.Item))
                {
                    var ordered = group
                        .OrderBy(c => c.Column.DisplayIndex)
                        .Select(c => (group.Key, c.Column))
                        .ToList();
                    if (FillAlongSequence(ordered))
                        filledAny = true;
                }
            }
        }
        finally
        {
            foreach (var item in affectedItems)
                EndBatchUpdate(item);
        }

        return filledAny;
    }

    private static bool FillAlongSequence(IReadOnlyList<(object Item, DataGridColumn Column)> ordered)
    {
        if (ordered.Count < 2) return false;
        string first = GetCellText(ordered[0].Item, ordered[0].Column);
        string second = GetCellText(ordered[1].Item, ordered[1].Column);
        bool firstIsNumber = decimal.TryParse(first.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1);
        bool secondIsNumber = decimal.TryParse(second.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2);

        if (firstIsNumber && secondIsNumber)
        {
            decimal step = v2 - v1;
            for (int i = 2; i < ordered.Count; i++)
                SetCellText(ordered[i].Item, ordered[i].Column, (v1 + step * i).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            string seed = !string.IsNullOrWhiteSpace(first) ? first : second;
            for (int i = 1; i < ordered.Count; i++)
                SetCellText(ordered[i].Item, ordered[i].Column, seed);
        }
        return true;
    }

    // ── Copiar / pegar estilo Excel ─────────────────────────────────────────────────────
    public static void ClearSelection(DataGrid grid)
    {
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (cells.Count == 0) return;

        foreach (var group in cells.GroupBy(c => c.Item))
        {
            var rowItem = group.Key;
            BeginBatchUpdate(rowItem);
            try
            {
                foreach (var c in group) SetCellText(c.Item, c.Column, string.Empty);
            }
            finally
            {
                EndBatchUpdate(rowItem);
            }
        }
    }

    public static void CopySelectionToClipboard(DataGrid grid)
    {
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null).ToList();
        if (cells.Count == 0)
            return;

        // Mapear item→índice una sola vez; hacer grid.Items.IndexOf() por celda sería O(n·m)
        // en una selección grande.
        var indexByItem = new Dictionary<object, int>(grid.Items.Count);
        for (int i = 0; i < grid.Items.Count; i++)
            indexByItem[grid.Items[i]] = i;

        var rows = cells.Select(c => indexByItem.TryGetValue(c.Item, out var idx) ? idx : -1)
            .Where(i => i >= 0).Distinct().OrderBy(i => i).ToList();
        var columns = cells.Select(c => c.Column).Distinct().OrderBy(c => c.DisplayIndex).ToList();
        if (rows.Count == 0 || columns.Count == 0)
            return;

        var sb = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            var item = grid.Items[rows[r]];
            if (r > 0)
                sb.Append('\n');
            sb.Append(string.Join('\t', columns.Select(col => GetCellText(item, col))));
        }

        Clipboard.SetText(sb.ToString());
        ShowCopyMarker(grid, cells);
        ShellNotificationService.Instance.ShowInfo(
            "Portapapeles",
            cells.Count == 1 ? "1 celda copiada al portapapeles." : $"{cells.Count:N0} celdas copiadas al portapapeles.");
    }

    // ── Marca visual de "esto es lo que estás copiando" y pegado estilo Excel ────────
    private static void ShowCopyMarker(DataGrid grid, List<DataGridCellInfo> cells) => DataGridClipboardSupport.ShowCopyMarker(grid, cells);

    public static void ClearCopyMarker(DataGrid grid) => DataGridClipboardSupport.ClearCopyMarker(grid);

    public static bool TryPasteIntoSelection(DataGrid grid, string clipboardText, Func<DataGridColumn, bool>? isSpecialColumn = null) =>
        DataGridClipboardSupport.TryPasteIntoSelection(grid, clipboardText, isSpecialColumn ?? (_ => false));

    public static TextBox? FindEditingTextBox(DataGrid grid)
    {
        var cell = GetCurrentCell(grid);
        cell?.UpdateLayout();
        return cell != null ? FindVisualChild<TextBox>(cell) : null;
    }

    private static void TryFocus(DataGrid grid, object item, DataGridColumn column, int retryCount)
    {
        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row != null)
            {
                var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                if (presenter != null)
                {
                    int colIndex = grid.Columns.IndexOf(column);
                    if (colIndex >= 0)
                    {
                        var cell = presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as DataGridCell;
                        if (cell != null)
                        {
                            cell.IsSelected = true;
                            cell.Focus();
                            Keyboard.Focus(cell);
                            return;
                        }
                    }
                }
            }

            if (retryCount > 0)
            {
                TryFocus(grid, item, column, retryCount - 1);
            }
        }), DispatcherPriority.Render);
    }

    internal static bool IsEditing(DataGrid grid)
    {
        var cell = GetCurrentCell(grid);
        return cell?.IsEditing == true;
    }

    internal static DataGridCell? GetCurrentCell(DataGrid grid)
    {
        var cellInfo = grid.CurrentCell;
        if (!cellInfo.IsValid) return null;

        var row = grid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item) as DataGridRow;
        if (row == null) return null;

        int colIndex = grid.Columns.IndexOf(cellInfo.Column);
        if (colIndex < 0) return null;

        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null) return null;

        var cell = presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as DataGridCell;
        if (cell == null)
        {
            grid.ScrollIntoView(row.Item, grid.Columns[colIndex]);
            cell = presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as DataGridCell;
        }
        return cell;
    }

    internal static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T match)
                return match;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : class
    {
        var current = child;
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelperSafe.GetParentSafe(current);
        }

        return null;
    }
}
