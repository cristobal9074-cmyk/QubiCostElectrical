using System;
using System.Collections.Generic;
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
    public static bool TryParseFlexibleDecimal(string? raw, out decimal result) =>
        DataGridCellAccessor.TryParseFlexibleDecimal(raw, out result);

    public static string GetCellText(object? dataContext, DataGridColumn? column) =>
        DataGridCellAccessor.GetCellText(dataContext, column);

    public static void SetCellText(object? dataContext, DataGridColumn? column, string value) =>
        DataGridCellAccessor.SetCellText(dataContext, column, value);

    public static void ClearSelection(DataGrid grid) =>
        DataGridCellAccessor.ClearSelection(grid);

    public static void CopySelectionToClipboard(DataGrid grid) =>
        DataGridCellAccessor.CopySelectionToClipboard(grid);

    internal static void BeginBatchUpdate(object? item) =>
        DataGridCellAccessor.BeginBatchUpdate(item);

    internal static void EndBatchUpdate(object? item) =>
        DataGridCellAccessor.EndBatchUpdate(item);

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
        grid.PreparingCellForEdit += DataGridCellEditorHandler.OnPreparingCellForEdit;
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

        DataGridCellEditorHandler.LastEditInitiatedByChar = true;
        DataGridCellEditorHandler.PendingCharEdit = true;
        try
        {
            if (!grid.BeginEdit())
            {
                DataGridCellEditorHandler.LastEditInitiatedByChar = false;
                DataGridCellEditorHandler.PendingCharEdit = false;
                return false;
            }
        }
        catch
        {
            DataGridCellEditorHandler.LastEditInitiatedByChar = false;
            DataGridCellEditorHandler.PendingCharEdit = false;
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
            textBox.PreviewTextInput -= DataGridCellEditorHandler.OnEditorPreviewTextInput;
            textBox.PreviewTextInput += DataGridCellEditorHandler.OnEditorPreviewTextInput;
            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DataGridCellEditorHandler.PendingCharEdit || textBox.SelectionLength > 0)
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
                innerBox.PreviewTextInput -= DataGridCellEditorHandler.OnEditorPreviewTextInput;
                innerBox.PreviewTextInput += DataGridCellEditorHandler.OnEditorPreviewTextInput;
                innerBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (DataGridCellEditorHandler.PendingCharEdit || innerBox.SelectionLength > 0)
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
                DataGridCellEditorHandler.LastEditInitiatedByF2 = true;
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
                    if (count is >= 2 and <= 50 && DataGridCellAccessor.FillSelection(grid, vertical: e.Key == Key.D)) { e.Handled = true; return; }
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
    public static void MoveToRowDataEdge(DataGrid grid, DataGridColumn column, object? currentRowItem, int direction)
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

        if (targetRow < 0)
        {
            if (wrapRows && grid.Items.Count > 0)
            {
                targetRow = grid.Items.Count - 1;
                targetIndex = columns.Count - 1;
            }
            else
            {
                return;
            }
        }
        else if (targetRow >= grid.Items.Count)
        {
            if (wrapRows && grid.Items.Count > 0)
            {
                targetRow = 0;
                targetIndex = 0;
            }
            else
            {
                return;
            }
        }

        FocusCell(grid, targetRow, columns[targetIndex]);
    }

    public static void EnsureActiveCell(DataGrid grid)
    {
        if (grid == null || grid.Items.Count == 0) return;

        if (grid.CurrentCell.IsValid && grid.CurrentCell.Column != null && grid.CurrentCell.Item != null && grid.Items.Contains(grid.CurrentCell.Item))
        {
            int rIdx = grid.Items.IndexOf(grid.CurrentCell.Item);
            if (rIdx >= 0)
            {
                FocusCell(grid, rIdx, grid.CurrentCell.Column);
                return;
            }
        }

        var targetRow = grid.SelectedItem ?? grid.Items[0];
        var targetCol = grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                     ?? grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (targetCol == null) return;

        int rowIdx = grid.Items.IndexOf(targetRow);
        if (rowIdx >= 0)
        {
            FocusCell(grid, rowIdx, targetCol);
        }
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

        // Si la columna es congelada o la primera desplazable inmediata tras el corte (ej. C1 al lado de CANT),
        // asegurarse de que el ScrollViewer horizontal no quede desfasado ocultando parte de la primera casilla.
        if (grid.FrozenColumnCount > 0 && column.DisplayIndex <= grid.FrozenColumnCount)
        {
            var sv = FindVisualChild<ScrollViewer>(grid);
            if (sv != null && sv.HorizontalOffset > 0)
            {
                sv.ScrollToHorizontalOffset(0);
            }
        }

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
                case Key.Left:
                    targetCol = FindColumnDataEdgeIndex(grid, columns, grid.CurrentCell.Item, colIndex, -1);
                    break;
                case Key.Right:
                    targetCol = FindColumnDataEdgeIndex(grid, columns, grid.CurrentCell.Item, colIndex, 1);
                    break;
                case Key.Up:
                    targetRow = FindRowDataEdgeIndex(grid, columns[colIndex], rowIndex, -1);
                    break;
                case Key.Down:
                    targetRow = FindRowDataEdgeIndex(grid, columns[colIndex], rowIndex, 1);
                    break;
            }
        }
        else
        {
            switch (key)
            {
                case Key.Left:
                    targetCol = Math.Max(0, colIndex - 1);
                    break;
                case Key.Right:
                    targetCol = Math.Min(columns.Count - 1, colIndex + 1);
                    break;
                case Key.Up:
                    targetRow = Math.Max(0, rowIndex - 1);
                    break;
                case Key.Down:
                    targetRow = Math.Min(grid.Items.Count - 1, rowIndex + 1);
                    break;
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

        var selection = new List<DataGridCellInfo>();
        for (int r = rowMin; r <= rowMax; r++)
        {
            var item = grid.Items[r];
            for (int c = colMin; c <= colMax; c++)
                selection.Add(new DataGridCellInfo(item, columns[c]));
        }

        var current = new DataGridCellInfo(targetItem, targetColumn);
        // Asignar CurrentCell deja una sola celda marcada. El rectángulo se vuelve a aplicar después.
        grid.CurrentCell = current;
        ApplyCellSelection(grid, selection);

        var realized = TryGetRealizedCell(grid, targetItem, targetColumn);
        if (realized != null)
        {
            realized.Focus();
            Keyboard.Focus(realized);
            ApplyCellSelection(grid, selection);
        }
        else
        {
            grid.ScrollIntoView(targetItem, targetColumn);
            TryFocus(grid, targetItem, targetColumn, 4, selection, current);
        }
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
        => TryFocus(grid, item, column, retryCount, null, default);

    private static void TryFocus(
        DataGrid grid,
        object item,
        DataGridColumn column,
        int retryCount,
        List<DataGridCellInfo>? keepSelection,
        DataGridCellInfo keepCurrent)
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
                            cell.Focus();
                            Keyboard.Focus(cell);
                            RestoreSelection(grid, keepSelection, keepCurrent);
                            return;
                        }
                    }
                }
            }

            if (retryCount > 0)
                TryFocus(grid, item, column, retryCount - 1, keepSelection, keepCurrent);
        }), DispatcherPriority.Render);
    }

    private static void RestoreSelection(DataGrid grid, List<DataGridCellInfo>? keepSelection, DataGridCellInfo keepCurrent)
    {
        if (keepSelection == null || keepSelection.Count <= 1)
            return;

        // Si la grilla ya avanzó a una selección más amplia (ej. el usuario continuó extendiendo con Shift),
        // no revertir con la instantánea anterior.
        int curCount = grid.SelectionUnit == DataGridSelectionUnit.FullRow ? grid.SelectedItems.Count : grid.SelectedCells.Count;
        if (curCount > keepSelection.Count)
            return;

        if (keepCurrent.IsValid)
            grid.CurrentCell = keepCurrent;
        ApplyCellSelection(grid, keepSelection);
    }

    private static void ApplyCellSelection(DataGrid grid, IReadOnlyList<DataGridCellInfo> selection)
    {
        if (grid.SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            if (grid.SelectionMode == DataGridSelectionMode.Extended)
            {
                grid.SelectedItems.Clear();
                var distinctItems = selection.Select(c => c.Item).Where(it => it != null).Distinct();
                foreach (var item in distinctItems)
                {
                    grid.SelectedItems.Add(item);
                }
            }
            return;
        }

        grid.SelectedCells.Clear();
        foreach (var cell in selection)
        {
            if (cell.IsValid)
                grid.SelectedCells.Add(cell);
        }
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

    public static T? FindParent<T>(DependencyObject child) where T : class
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
