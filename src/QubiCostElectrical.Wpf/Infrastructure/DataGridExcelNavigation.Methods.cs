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
        var all = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
        if (_columnScopes.TryGetValue(grid, out var box) && box.Predicate != null
            && (currentColumn == null || box.Predicate(currentColumn)))
        {
            var scoped = all.Where(box.Predicate).ToList();
            if (scoped.Count > 0)
                return scoped;
        }
        return all;
    }

    // Marca visual de "esto es lo que estás copiando" (estilo Excel) por grilla: un adorno
    // con marco punteado animado sobre el rectángulo copiado. Se actualiza en cada Ctrl+C/X y
    // se limpia al pegar, al presionar Escape, o al iniciar la edición de otra celda.
    private static readonly ConditionalWeakTable<DataGrid, CopyMarkerState> _copyMarkers = new();

    private sealed class CopyMarkerState
    {
        public CopyMarkerAdorner? Adorner;
        public List<DataGridCellInfo> Cells = new();
        public bool LayoutHooked;
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

        if (!grid.BeginEdit())
            return false;

        var cell = GetCurrentCell(grid);
        // Forzar el layout: BeginEdit deja IsEditing=true de inmediato, pero la plantilla de
        // edición (y su TextBox) puede no estar generada todavía en el mismo tick.
        cell?.UpdateLayout();
        var textBox = cell != null ? FindVisualChild<TextBox>(cell) : null;
        if (textBox == null)
            return false;

        textBox.Focus();
        textBox.Text = typedText;
        textBox.CaretIndex = textBox.Text.Length;
        return true;
    }

    public static void HandlePreviewKeyDown(DataGrid grid, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
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
                grid.BeginEdit();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter or Key.Return or Key.Tab)
            {
                var currentColumn = grid.CurrentCell.Column;
                if (grid.CurrentCell.IsValid && currentColumn != null)
                {
                    if (e.Key is Key.Enter or Key.Return)
                    {
                        MoveToRowOffset(grid, currentColumn, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
                    }
                    else
                    {
                        MoveToColumnOffset(grid, currentColumn, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1, wrapRows: true);
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

            // Ctrl+D / Ctrl+R: rellenar hacia abajo / hacia la derecha (estilo Excel). Si las
            // dos primeras celdas de la secuencia forman una progresión numérica (ej. 1, 2),
            // continúa la serie; si no, repite el primer valor — el mismo "autorrelleno" del
            // tirador de Excel, pero accesible por teclado sin necesitar arrastrar el mouse.
            // Ctrl+D solo se consume si hay algo real que rellenar (2+ filas en una misma
            // columna seleccionadas); si el usuario solo tiene una celda activa, no hace nada
            // aquí y el atajo sigue disponible para "Duplicar fila" más abajo en la vista.
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.D or Key.R)
            {
                if (FillSelection(grid, vertical: e.Key == Key.D))
                {
                    e.Handled = true;
                    return;
                }
            }

            // Delete / Backspace: borrar el contenido de toda la selección de inmediato
            if (e.Key is Key.Delete or Key.Back)
            {
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
        if (e.EditingElement is not TextBox textBox)
            return;

        textBox.PreviewKeyDown -= OnEditorPreviewKeyDown;
        textBox.PreviewKeyDown += OnEditorPreviewKeyDown;

        // Si la edición empezó tecleando un carácter (no F2 ni doble clic), WPF ya insertó
        // ese carácter en el TextBox y dejó el cursor detrás. Llamar SelectAll() aquí
        // seleccionaba ese primer carácter recién tecleado, así que la SIGUIENTE tecla lo
        // reemplazaba en vez de continuar escribiendo — el bug de "escribo 1, luego 0, y
        // queda solo 0". Solo se selecciona todo cuando la edición NO vino de una tecla
        // (F2, doble clic, o inicio programático), donde sí se quiere reemplazar el valor.
        if (e.EditingEventArgs is KeyEventArgs or TextCompositionEventArgs)
            textBox.CaretIndex = textBox.Text.Length;
        else
            textBox.SelectAll();
    }

    private static void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var grid = FindParent<DataGrid>(textBox);
        var cell = FindParent<DataGridCell>(textBox);
        if (grid == null || cell?.Column == null)
            return;

        // Enter: confirmar y mover a la fila de abajo
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitCellAndRow(grid);
            MoveToRowOffset(grid, cell.Column, 1);
            e.Handled = true;
            return;
        }

        // Tab / Shift+Tab: confirmar y mover columna izquierda/derecha, saltando a la fila
        // siguiente/anterior al llegar al final — igual que Excel.
        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            if (backward)
                MoveToColumnOffset(grid, cell.Column, -1, wrapRows: true);
            else
                MoveToColumnOffset(grid, cell.Column, 1, wrapRows: true);
            e.Handled = true;
            return;
        }

        // Up / Down en edición: confirmar y mover arriba/abajo. Con Ctrl, salta al borde del
        // bloque de datos de esa columna (estilo Excel) en vez de una fila a la vez — la fila
        // activa se captura ANTES de confirmar la edición, no se relee después.
        if (e.Key is Key.Up or Key.Down)
        {
            object? activeRow = cell.DataContext;
            bool ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            CommitCellAndRow(grid);
            if (ctrlPressed)
                MoveToRowDataEdge(grid, cell.Column, activeRow, e.Key == Key.Down ? 1 : -1);
            else
                MoveToRowOffset(grid, cell.Column, e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        // Left / Right en edición: confirmar y mover a la celda izquierda/derecha, igual que
        // Excel — el usuario escribe el dato y sigue navegando con flechas sin apretar Enter.
        // A diferencia de Tab, la flecha NO salta de fila al llegar al final/principio de la
        // fila (en Excel eso solo lo hace Tab) — se queda en el borde.
        if (e.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            MoveToColumnOffset(grid, cell.Column, e.Key == Key.Right ? 1 : -1, wrapRows: false);
            e.Handled = true;
            return;
        }
    }

    // Confirma la celda y, además, la fila. CommitEdit(Cell) por sí solo puede dejar
    // abierta la transacción de edición a nivel de fila del ICollectionView subyacente
    // (IEditableCollectionView.IsEditingItem permanece en true), lo que hace fallar
    // cualquier CollectionView.Refresh() posterior mientras esa fila siga "activa".
    // Al cambiar de fila (Enter, Flecha arriba/abajo) confirmamos también a nivel de
    // fila para dejar la transacción completamente cerrada.
    private static void CommitCellAndRow(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private static void MoveToRowOffset(DataGrid grid, DataGridColumn column, int rowOffset)
    {
        // CurrentCell.Item, no CurrentItem (el cursor de la vista de colección, que puede
        // desincronizarse de la celda realmente activa — ver comentario en MoveToRowDataEdge).
        int rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
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

    internal static void MoveToColumnOffset(DataGrid grid, DataGridColumn currentColumn, int columnOffset, bool wrapRows)
    {
        // CurrentCell.Item, no CurrentItem — mismo motivo que en MoveToRowOffset.
        int rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
        if (rowIndex < 0)
            return;

        var columns = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
            .OrderBy(c => c.DisplayIndex)
            .ToList();
        if (columns.Count == 0)
            return;

        int currentIndex = columns.IndexOf(currentColumn);
        if (currentIndex < 0)
            return;

        int targetIndex = currentIndex + columnOffset;
        int targetRow = rowIndex;

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
        if (grid.SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            grid.SelectedItem = item;
        }
        else
        {
            grid.SelectedCells.Clear();
            grid.SelectedCells.Add(new DataGridCellInfo(item, column));
        }

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
        var realizedCell = TryGetRealizedCell(grid, item, column);
        if (realizedCell != null)
        {
            realizedCell.Focus();
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
        var columns = GetNavigableColumns(grid, grid.CurrentCell.Column);
        if (columns.Count == 0 || grid.Items.Count == 0)
            return;

        grid.SelectedCells.Clear();
        foreach (var item in grid.Items)
        {
            foreach (var column in columns)
                grid.SelectedCells.Add(new DataGridCellInfo(item, column));
        }
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

        if (prop.PropertyType == typeof(string))
        {
            prop.SetValue(dataContext, value);
        }
        else if (prop.PropertyType == typeof(decimal))
        {
            if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                prop.SetValue(dataContext, d);
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, 0m);
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
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (cells.Count == 0)
            return false;

        bool filledAny = false;

        if (vertical)
        {
            foreach (var group in cells.GroupBy(c => c.Column))
            {
                var ordered = group
                    .Select(c => (Item: c.Item, RowIndex: grid.Items.IndexOf(c.Item)))
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

        return filledAny;
    }

    private static bool FillAlongSequence(IReadOnlyList<(object Item, DataGridColumn Column)> ordered)
    {
        if (ordered.Count < 2)
            return false;

        string first = GetCellText(ordered[0].Item, ordered[0].Column);
        string second = GetCellText(ordered[1].Item, ordered[1].Column);

        bool firstIsNumber = decimal.TryParse(first.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1);
        bool secondIsNumber = decimal.TryParse(second.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2);
        bool isSeries = firstIsNumber && secondIsNumber;

        if (isSeries)
        {
            decimal step = v2 - v1;
            for (int i = 2; i < ordered.Count; i++)
            {
                decimal value = v1 + step * i;
                SetCellText(ordered[i].Item, ordered[i].Column, value.ToString(CultureInfo.InvariantCulture));
            }
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
        if (cells.Count == 0)
            return;

        var cellsByRow = cells.GroupBy(c => c.Item).ToList();

        foreach (var group in cellsByRow)
        {
            var rowItem = group.Key;
            var beginMethod = rowItem?.GetType().GetMethod("BeginBatchUpdate");
            var endMethod = rowItem?.GetType().GetMethod("EndBatchUpdate");

            if (beginMethod != null)
            {
                beginMethod.Invoke(rowItem, null);
            }

            try
            {
                foreach (var c in group)
                {
                    SetCellText(c.Item, c.Column, string.Empty);
                }
            }
            finally
            {
                if (endMethod != null)
                {
                    endMethod.Invoke(rowItem, null);
                }
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
    }

    // ── Marca visual de "esto es lo que estás copiando" (estilo Excel) ─────────────────
    private static void ShowCopyMarker(DataGrid grid, List<DataGridCellInfo> cells)
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
        foreach (var cellInfo in state.Cells)
        {
            var row = grid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item) as DataGridRow;
            if (row == null)
                continue;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
                continue;

            int colIndex = grid.Columns.IndexOf(cellInfo.Column);
            if (colIndex < 0)
                continue;

            if (presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) is not DataGridCell cell)
                continue;

            var topLeft = cell.TransformToAncestor(grid).Transform(new Point(0, 0));
            var cellRect = new Rect(topLeft, new Size(cell.ActualWidth, cell.ActualHeight));
            bounds = bounds == null ? cellRect : Rect.Union(bounds.Value, cellRect);
        }

        // Si ninguna celda copiada está actualmente renderizada (se scrolleó fuera de vista),
        // no se dibuja nada — reaparece solo al volver a scrollear a esa zona, igual que Excel.
        state.Adorner.UpdateRect(bounds ?? Rect.Empty);
    }

    // Pega texto TSV en la selección/celda activa para columnas "genéricas" (no manejadas por
    // lógica de negocio especial, ej. la matriz de sectores). Si isSpecialColumn indica que el
    // ancla cae en una columna con lógica propia (Código/Descripción/Cantidad), no hace nada y
    // devuelve false para que el llamador use su propio flujo de pegado.
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

        // Pegar sobre una selección de VARIAS celdas (rectángulo, no solo la celda ancla):
        // repite ("tile") el bloque copiado tantas veces como haga falta para cubrir TODO el
        // rectángulo seleccionado — así funciona Excel al pegar un valor único (o un bloque más
        // chico) sobre una selección más grande, en vez de pegar el bloque una sola vez a partir
        // del ancla e ignorar el resto de las celdas marcadas.
        if (selectedCells.Count > 1)
        {
            var selectedPairs = selectedCells
                .Select(c => (Row: grid.Items.IndexOf(c.Item), Col: visibleColumns.IndexOf(c.Column)))
                .Where(p => p.Row >= 0 && p.Col >= 0)
                .ToList();

            if (selectedPairs.Count > 1)
            {
                int rowMin = selectedPairs.Min(p => p.Row);
                int colMin = selectedPairs.Min(p => p.Col);

                foreach (var (row, col) in selectedPairs)
                {
                    int sourceRow = (row - rowMin) % grid2D.Length;
                    var sourceLine = grid2D[sourceRow];
                    if (sourceLine.Length == 0)
                        continue;
                    int sourceCol = (col - colMin) % sourceLine.Length;
                    SetCellText(grid.Items[row], visibleColumns[col], sourceLine[sourceCol].Trim());
                }

                return true;
            }
        }

        int anchorColIndex = visibleColumns.IndexOf(anchor.Column);
        int anchorRowIndex = grid.Items.IndexOf(anchor.Item);
        if (anchorColIndex < 0 || anchorRowIndex < 0)
            return false;

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

                SetCellText(item, visibleColumns[targetCol], grid2D[r][c].Trim());
            }
        }

        return true;
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
                            cell.Focus();
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

    private static bool IsEditing(DataGrid grid)
    {
        var cell = GetCurrentCell(grid);
        return cell?.IsEditing == true;
    }

    private static DataGridCell? GetCurrentCell(DataGrid grid)
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

    private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
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

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
