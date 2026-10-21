using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView
{
    // ── Flyout de pisos (celda "Ubicación / Pisos") ──────────────────────────
    // La celda misma es un TextBox libre (un solo piso); el botón de pisos de la esquina abre un
    // Popup anclado con las zonas del proyecto para cuando el circuito pasa por varios pisos.
    // Sin checkbox: cada zona se activa sola al escribirle un largo (ver ZonaPisoQuickItem).
    private void PisosBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanEdit) return;
        if (sender is not FrameworkElement { DataContext: CircuitoScRowVm vm }) return;
        OpenPisosFlyout(vm, Vm.ZonasProyecto);
    }

    // El flyout usa StaysOpen="True" (nada lo cierra por foco/click, para eliminar el parpadeo).
    // El cierre "al hacer clic afuera" lo damos manualmente: cuando el foco de teclado sale del
    // contenido del flyout hacia un elemento que NO está dentro de él, lo cerramos (y confirma).
    private void PisosFlyout_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not DependencyObject content) return;
        if (e.NewFocus is DependencyObject nuevo && IsInSameTree(nuevo, content)) return;
        ClosePisosFlyout();
    }

    // ¿'nodo' está dentro del subárbol visual de 'raiz'? (recorre padres visuales; funciona
    // dentro del árbol propio del Popup).
    private static bool IsInSameTree(DependencyObject? nodo, DependencyObject raiz)
    {
        while (nodo != null)
        {
            if (ReferenceEquals(nodo, raiz)) return true;
            nodo = QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.GetParentSafe(nodo);
        }
        return false;
    }

    // internal (no private): la lógica de abrir/cerrar el flyout queda separada del evento de
    // clic/tecla que la dispara, para poder invocarla directo sin simular eventos de teclado o
    // mouse reales de WPF (frágil de construir en pruebas).
    internal void OpenPisosFlyout(CircuitoScRowVm vm, IReadOnlyList<ZonaProyecto> zonasProyecto)
    {
        if (!Vm.CanEdit) return;
        activePisosCircuito = vm;
        vm.PopulateZonaPisos(zonasProyecto);
        vm.PisosPopupOpen = true;
    }

    // Cierra el flyout (Escape lo llama). No toca el árbol del Popup para nada — solo usa
    // activePisosCircuito, guardado al abrir.
    internal void ClosePisosFlyout()
    {
        if (activePisosCircuito != null)
            activePisosCircuito.PisosPopupOpen = false;
    }

    // Al abrir el flyout, enfoca el primer campo (L. Band de la primera zona) para poder
    // empezar a teclear de inmediato, sin tocar el mouse.
    private void PisosPopup_Opened(object sender, EventArgs e)
    {
        var popup = (Popup)sender;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (popup.Child == null) return;
            var first = FindDescendant<TextBox>(popup.Child);
            if (first == null) return;
            first.Focus();
            first.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    // Al cerrar el flyout (Esc, clic afuera, o el botón vuelve a apretarse) confirma las zonas
    // activas hacia el modelo del circuito. Usa activePisosCircuito (no el DataContext del
    // Popup) para no depender de la herencia de DataContext a través de la ventana aparte que
    // abre un Popup con AllowsTransparency="True".
    private void PisosPopup_Closed(object sender, EventArgs e) => CommitActivePisosFlyout();

    internal void CommitActivePisosFlyout()
    {
        activePisosCircuito?.CommitZonaPisos();
        activePisosCircuito = null;
    }

    // Navegación 100% teclado dentro del flyout: ↓/Enter baja a la misma columna de la
    // siguiente zona, ↑ sube, ←/→ cambia entre L.Band y L.PVC de la misma zona, Esc cierra el
    // flyout (y dispara PisosPopup_Closed → commit). Tab ya mueve L.Band → L.PVC → siguiente
    // zona solo, por el orden natural del árbol visual — no se intercepta.
    // Cada TextBox trae Tag="0" (L.Band) o "1" (L.PVC, ver XAML) para saber su columna sin
    // depender de la posición exacta dentro de la lista.
    private void PisosFlyout_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { ClosePisosFlyout(); CircuitosGrid?.Focus(); e.Handled = true; }
    }

    private void PisosPopupInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (e.Key == Key.Escape) { ClosePisosFlyout(); CircuitosGrid?.Focus(); e.Handled = true; return; }

        var itemsControl = FindAncestor<ItemsControl>(tb);
        if (itemsControl == null) return;

        var boxes = new List<TextBox>();
        CollectDescendants(itemsControl, boxes);
        int idx = boxes.IndexOf(tb);
        if (idx < 0) return;

        bool esColumnaBand = Equals(tb.Tag, "0");

        if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter)
        {
            int target = idx + (e.Key == Key.Up ? -2 : 2); // 2 columnas por fila (L.Band / L.PVC)
            FocusIfInRange(boxes, target);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && esColumnaBand && IsAtBoundary(tb, left: false))
        {
            FocusIfInRange(boxes, idx + 1); // L.Band → L.PVC de la misma zona
            e.Handled = true;
        }
        else if (e.Key == Key.Left && !esColumnaBand && IsAtBoundary(tb, left: true))
        {
            FocusIfInRange(boxes, idx - 1); // L.PVC → L.Band de la misma zona
            e.Handled = true;
        }
        // Left/Right en el borde opuesto (ej. Right sobre L.PVC) no hacen nada especial: se deja
        // el comportamiento nativo del TextBox (mover el caret dentro del número).
    }

    private static bool IsAtBoundary(TextBox tb, bool left)
    {
        if (tb.SelectionLength == tb.Text.Length) return true; // todo seleccionado (recién enfocado)
        return left ? tb.CaretIndex == 0 : tb.CaretIndex == tb.Text.Length;
    }

    private static void FocusIfInRange(List<TextBox> boxes, int index)
    {
        if (index < 0 || index >= boxes.Count) return;
        boxes[index].Focus();
        boxes[index].SelectAll();
    }

    private void PisosPopupInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
    }

    private void CircuitosGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (activePisosCircuito != null) return;
        if (!Vm.CanEdit) return;

        if (DataGridExcelNavigation.TryBeginEditFromChar(CircuitosGrid, e.Text))
            e.Handled = true;
    }

    private void UbicacionEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.CaretIndex = tb.Text.Length;
            tb.SelectionLength = 0;
            if (tb.DataContext is CircuitoScRowVm row && DataContext is CubicacionScViewModel scVm)
            {
                scVm.SelectedCircuito = row;
            }
        }
    }

    internal void UbicacionEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit)
        {
            if (!row.TienePisos) row.Ubicacion = textBox.Text;
        }
    }

    private void UbicacionEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var row = tb.DataContext as CircuitoScRowVm;
        if (row == null) return;

        bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        // Alt+P: toggle flyout de pisos
        if (isAlt && (e.SystemKey == Key.P || e.Key == Key.P))
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            if (activePisosCircuito == row && row.PisosPopupOpen)
                ClosePisosFlyout();
            else
                OpenPisosFlyout(row, Vm.ZonasProyecto);
            e.Handled = true;
            return;
        }

        // Alt+A: abrir artefactos
        if (isAlt && (e.SystemKey == Key.A || e.Key == Key.A))
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            if (DataContext is CubicacionScViewModel scVm && scVm.OpenArtefactosCommand.CanExecute(row))
            {
                scVm.OpenArtefactosCommand.Execute(row);
                e.Handled = true;
                return;
            }
        }

        // F4 o Alt+Down: abrir flyout de pisos
        if (e.Key == Key.F4 || (isAlt && (e.SystemKey == Key.Down || e.Key == Key.Down)))
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            OpenPisosFlyout(row, Vm.ZonasProyecto);
            e.Handled = true;
            return;
        }

        // Escape: cancelar edición y mantener foco en la celda
        if (e.Key == Key.Escape)
        {
            tb.Tag = CancelledEditTag;
            CircuitosGrid.CancelEdit(DataGridEditingUnit.Cell);
            e.Handled = true;
            return;
        }

        var cell = FindAncestor<DataGridCell>(tb);
        int rowIndex = CircuitosGrid.Items.IndexOf(row);

        // Enter: confirmar valor, bajar de fila (o subir con Shift)
        if (e.Key is Key.Enter or Key.Return)
        {
            if (!row.TienePisos && row.CanEdit) row.Ubicacion = tb.Text;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);

            int target = rowIndex + ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
            if (target >= 0 && target < CircuitosGrid.Items.Count && cell?.Column != null)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        // Tab: confirmar valor, avanzar columna (o retroceder con Shift)
        if (e.Key == Key.Tab)
        {
            if (!row.TienePisos && row.CanEdit) row.Ubicacion = tb.Text;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);

            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (cell?.Column != null)
            {
                DataGridExcelNavigation.MoveToColumnOffset(
                    CircuitosGrid, cell.Column, backward ? -1 : 1, wrapRows: true, row);
            }
            e.Handled = true;
            return;
        }

        // Up / Down: confirmar valor, subir/bajar fila
        if (e.Key is Key.Up or Key.Down)
        {
            if (!row.TienePisos && row.CanEdit) row.Ubicacion = tb.Text;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);

            int target = rowIndex + (e.Key == Key.Down ? 1 : -1);
            if (target >= 0 && target < CircuitosGrid.Items.Count && cell?.Column != null)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        // Left / Right en los bordes: confirmar y saltar a columna adyacente
        if (e.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None)
        {
            bool isAtLeft = tb.CaretIndex == 0 || tb.SelectionLength == tb.Text.Length;
            bool isAtRight = tb.CaretIndex == tb.Text.Length || tb.SelectionLength == tb.Text.Length;

            if ((e.Key == Key.Left && isAtLeft) || (e.Key == Key.Right && isAtRight))
            {
                if (!row.TienePisos && row.CanEdit) row.Ubicacion = tb.Text;
                CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);

                int offset = e.Key == Key.Right ? 1 : -1;
                if (cell?.Column != null)
                {
                    DataGridExcelNavigation.MoveToColumnOffset(
                        CircuitosGrid, cell.Column, offset, wrapRows: false, row);
                }
                e.Handled = true;
                return;
            }
        }
    }

    internal void EnsureActiveCellSelected()
    {
        if (CircuitosGrid.Items.Count == 0) return;

        if (CircuitosGrid.CurrentCell.IsValid && CircuitosGrid.CurrentCell.Column != null && CircuitosGrid.CurrentCell.Item != null && CircuitosGrid.Items.Contains(CircuitosGrid.CurrentCell.Item))
        {
            if (!CircuitosGrid.SelectedCells.Contains(CircuitosGrid.CurrentCell))
            {
                CircuitosGrid.SelectedCells.Clear();
                CircuitosGrid.SelectedCells.Add(CircuitosGrid.CurrentCell);
            }
            return;
        }

        var targetRow = (DataContext as CubicacionScViewModel)?.SelectedCircuito ?? CircuitosGrid.Items[0];
        var targetCol = CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !ReferenceEquals(c, NumeroColumn))
                     ?? CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (targetCol == null) return;

        var cellInfo = new DataGridCellInfo(targetRow, targetCol);
        CircuitosGrid.CurrentCell = cellInfo;
        CircuitosGrid.SelectedCells.Clear();
        CircuitosGrid.SelectedCells.Add(cellInfo);
        if (DataContext is CubicacionScViewModel scVm && targetRow is CircuitoScRowVm rowVm)
        {
            scVm.SelectedCircuito = rowVm;
        }
    }

    private void CircuitosGrid_CurrentCellChanged(object? sender, EventArgs e)
    {
        if (CircuitosGrid.CurrentCell.IsValid && CircuitosGrid.CurrentCell.Column != null)
        {
            if (CircuitosGrid.SelectedCells.Count == 0 ||
                (CircuitosGrid.SelectedCells.Count == 1 && !CircuitosGrid.SelectedCells.Contains(CircuitosGrid.CurrentCell)))
            {
                CircuitosGrid.SelectedCells.Clear();
                CircuitosGrid.SelectedCells.Add(CircuitosGrid.CurrentCell);
            }
        }
        if (DataContext is CubicacionScViewModel scVm)
            SyncSelectedCircuitoFromGrid(scVm);
    }

    // Clic con mouse estilo Excel:
    // - Cualquier clic sobre una casilla la marca inmediatamente con el recuadro azul.
    // - Si se estaba editando otra casilla, se confirman sus datos síncronamente antes de mover el foco (nunca se pierde lo escrito).
    // - Si se hace clic en espacio vacío de la grilla, no se borra la selección.
    // - Clic en N° selecciona todo el circuito (para Ctrl+C de fila completa).
    // - Doble clic en celda editable entra en modo edición.
    private void CircuitosGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.OriginalSource is not DependencyObject dep) return;
            var cell = FindAncestor<DataGridCell>(dep);
            var scVm = DataContext as CubicacionScViewModel;

            // Clic en espacio vacío de la grilla (ej. bajo las filas): mantener la casilla activa sin deseleccionar
            if (cell == null)
            {
                EnsureActiveCellSelected();
                CircuitosGrid.Focus();
                e.Handled = true;
                return;
            }

            if (cell.Column == null || cell.DataContext is not CircuitoScRowVm row) return;

            // Si el clic ocurre dentro de un editor activo (TextBox / ComboBox) de esta misma celda,
            // permitir posicionar el caret o abrir opciones nativamente sin interrumpir.
            if (cell.IsEditing || dep is TextBox || FindAncestor<TextBox>(dep) != null || dep is ComboBox || FindAncestor<ComboBox>(dep) != null)
            {
                return;
            }

            // Si la grilla estaba en edición de otra celda, confirmar sus datos síncronamente antes de cambiar
            if (IsCircuitosGridEditing())
            {
                if (Keyboard.FocusedElement is FrameworkElement focusedEditor)
                {
                    var tb = focusedEditor as TextBox ?? FindDescendant<TextBox>(focusedEditor);
                    var cb = FindAncestor<ComboBox>(focusedEditor) ?? focusedEditor as ComboBox;
                    var editingCell = FindAncestor<DataGridCell>(focusedEditor);
                    if (tb != null && editingCell?.Column != null && editingCell.DataContext is CircuitoScRowVm editingRow)
                    {
                        CommitCircuitoCellEditor(tb, cb, editingCell.Column, editingRow);
                    }
                }
                CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }

            // Clic en N° → selecciona toda la fila para Ctrl+C del circuito completo
            if (ReferenceEquals(cell.Column, NumeroColumn))
            {
                SelectEntireCircuitoRow(row);
                if (scVm != null)
                    scVm.SelectedCircuito = row;
                e.Handled = true;
                return;
            }

            // Clic en cualquier casilla (sea TemplateColumn o TextColumn): marcarla de inmediato
            var cellInfo = new DataGridCellInfo(row, cell.Column);
            CircuitosGrid.CurrentCell = cellInfo;
            CircuitosGrid.SelectedCells.Clear();
            CircuitosGrid.SelectedCells.Add(cellInfo);
            if (scVm != null)
                scVm.SelectedCircuito = row;

            // Si el clic fue en un botón interactivo (ej. Pisos o Artefactos): dejar que el botón procese su click
            if (FindAncestor<Button>(dep) != null)
            {
                return;
            }

            cell.Focus();
            CircuitosGrid.Focus();

            // Doble clic en celda editable: iniciar edición
            if (e.ClickCount == 2 && !cell.Column.IsReadOnly && scVm?.CanEdit == true && !scVm.IsGridReadOnly)
            {
                CircuitosGrid.BeginEdit();
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CubicacionScView] Clic en grilla circuitos: {ex.Message}");
        }
    }

    private void SelectEntireCircuitoRow(CircuitoScRowVm row)
    {
        CircuitosGrid.SelectedCells.Clear();
        foreach (var col in CircuitosGrid.Columns.Where(c => c.Visibility == Visibility.Visible))
            CircuitosGrid.SelectedCells.Add(new DataGridCellInfo(row, col));
        CircuitosGrid.CurrentCell = new DataGridCellInfo(row, NumeroColumn);
        CircuitosGrid.Focus();
    }

    private bool IsEntireCircuitoRowSelected(CircuitoScRowVm row)
    {
        var visible = CircuitosGrid.Columns.Where(c => c.Visibility == Visibility.Visible).ToList();
        if (visible.Count == 0) return false;
        var selectedForRow = CircuitosGrid.SelectedCells
            .Where(c => c.IsValid && ReferenceEquals(c.Item, row))
            .Select(c => c.Column)
            .Distinct()
            .Count();
        // Toda la fila, o al menos N° + la mayoría (por si hay columnas ocultas al vuelo).
        return selectedForRow >= visible.Count
            || (CircuitosGrid.CurrentCell.Column == NumeroColumn && selectedForRow >= Math.Max(2, visible.Count - 2));
    }

    private bool IsNumeroColumnFocused()
        => CircuitosGrid.CurrentCell.IsValid
           && ReferenceEquals(CircuitosGrid.CurrentCell.Column, NumeroColumn);

    private bool IsUbicacionColumnFocused()
        => CircuitosGrid.CurrentCell.IsValid
           && ReferenceEquals(CircuitosGrid.CurrentCell.Column, ColUbicacion);

    private bool IsArtefactosColumnFocused()
        => CircuitosGrid.CurrentCell.IsValid
           && ReferenceEquals(CircuitosGrid.CurrentCell.Column, ColArtefactos);

    // Navegación estilo Excel + atajos de circuito.
    // Ctrl+C en N°/fila = copia circuito · Ctrl+V = pega SOBRE el circuito activo (no agrega).
    // Ctrl+D = duplica (agrega fila), salvo rango vertical 2+ filas → relleno Excel.
    // Alt+P = abre/cierra flyout de Pisos · Alt+A = abre modal de Artefactos (Esc cierra ambos).
    private void CircuitosGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (activePisosCircuito != null)
        {
            if (e.Key == Key.Escape)
            {
                ClosePisosFlyout();
                CircuitosGrid?.Focus();
                e.Handled = true;
            }
            return;
        }

        bool editing = IsCircuitosGridEditing();

        if (e.Key == Key.Escape && Keyboard.FocusedElement is FrameworkElement focused)
        {
            var combo = FindAncestor<ComboBox>(focused);
            if (combo != null)
                _cancelledFilterCombos.Add(combo);
            else if (focused is TextBox textBox)
                textBox.Tag = CancelledEditTag;
        }

        var scVm = DataContext as CubicacionScViewModel;
        SyncSelectedCircuitoFromGrid(scVm);

        CircuitoScRowVm? GetTargetCircuito()
        {
            return scVm?.SelectedCircuito
                ?? CircuitosGrid.CurrentCell.Item as CircuitoScRowVm
                ?? CircuitosGrid.SelectedItem as CircuitoScRowVm;
        }

        // Atajos con Alt: Alt+P (Pisos / Zonas), Alt+A (Artefactos × Zona)
        bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (isAlt)
        {
            if (e.SystemKey == Key.P || e.Key == Key.P)
            {
                var c = GetTargetCircuito();
                if (c != null && Vm.CanEdit)
                {
                    if (activePisosCircuito == c && c.PisosPopupOpen)
                        ClosePisosFlyout();
                    else
                        OpenPisosFlyout(c, Vm.ZonasProyecto);

                    e.Handled = true;
                    return;
                }
            }

            if (e.SystemKey == Key.A || e.Key == Key.A)
            {
                var c = GetTargetCircuito();
                if (c != null && scVm != null && scVm.OpenArtefactosCommand.CanExecute(c))
                {
                    scVm.OpenArtefactosCommand.Execute(c);
                    e.Handled = true;
                    return;
                }
            }

            if ((e.SystemKey == Key.Down || e.Key == Key.Down) && IsUbicacionColumnFocused())
            {
                var c = GetTargetCircuito();
                if (c != null && Vm.CanEdit)
                {
                    OpenPisosFlyout(c, Vm.ZonasProyecto);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (!editing)
        {
            if (e.Key == Key.F4 && IsUbicacionColumnFocused())
            {
                var c = GetTargetCircuito();
                if (c != null && Vm.CanEdit)
                {
                    OpenPisosFlyout(c, Vm.ZonasProyecto);
                    e.Handled = true;
                    return;
                }
            }

            if ((e.Key is Key.Enter or Key.Space or Key.F4) && IsArtefactosColumnFocused())
            {
                var c = GetTargetCircuito();
                if (c != null && scVm != null && scVm.OpenArtefactosCommand.CanExecute(c))
                {
                    scVm.OpenArtefactosCommand.Execute(c);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (!editing && scVm != null && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool isPlus = e.Key is Key.Add or Key.OemPlus || (shift && e.Key is Key.OemPlus or Key.Oem1 or Key.OemQuestion);
            bool isMinus = !shift && e.Key is Key.Subtract or Key.OemMinus;

            // Ctrl +: Agregar circuito SC copiando todos los parámetros técnicos
            if (isPlus && scVm.AddCircuitoCommand.CanExecute(null))
            {
                scVm.AddCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl -: Eliminar circuito SC seleccionado
            if (isMinus && scVm.RemoveCircuitoCommand.CanExecute(null))
            {
                scVm.RemoveCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+D primero: duplicar circuito (solo si hay 1 fila activa, salvo relleno vertical acotado de 2+ filas).
            if (!shift && e.Key == Key.D)
            {
                if (CircuitosGrid.SelectedItems.Count <= 1 && CircuitosGrid.SelectedCells.Count <= 1 && scVm.DuplicateCircuitoCommand.CanExecute(null))
                {
                    scVm.DuplicateCircuitoCommand.Execute(null);
                    if (scVm.SelectedCircuito != null)
                        FocusCircuitoWithoutRowSelect(scVm.SelectedCircuito);
                    e.Handled = true;
                    return;
                }
                else if (HasVerticalMultiRowFillSelection())
                {
                    // Si hay rango vertical acotado, deja que HandlePreviewKeyDown haga el fill.
                }
                else
                {
                    e.Handled = true;
                    return;
                }
            }

            if (shift && e.Key == Key.C && scVm.CopyCircuitoCommand.CanExecute(null))
            {
                scVm.CopyCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+V / Ctrl+Shift+V: si hay circuito copiado, pega SOBRE el actual (no agrega).
            if (e.Key == Key.V && scVm.PasteCircuitoCommand.CanExecute(null))
            {
                scVm.PasteCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (!shift && e.Key is Key.C or Key.X)
            {
                var row = scVm.SelectedCircuito
                    ?? CircuitosGrid.CurrentCell.Item as CircuitoScRowVm;
                if (row != null && (IsNumeroColumnFocused() || IsEntireCircuitoRowSelected(row))
                    && scVm.CopyCircuitoCommand.CanExecute(null))
                {
                    scVm.SelectedCircuito = row;
                    scVm.CopyCircuitoCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Copia de celdas: limpia clipboard de circuito para que Ctrl+V pegue celdas.
                scVm.ClearCircuitoClipboard();
                DataGridExcelNavigation.CopySelectionToClipboard(CircuitosGrid);
                e.Handled = true;
                return;
            }
            if (!shift && e.Key == Key.V)
            {
                try
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text)
                        && DataGridExcelNavigation.TryPasteIntoSelection(CircuitosGrid, text, IsNonPasteableColumn))
                    {
                        scVm.NotifyGridCellsEdited();
                        e.Handled = true;
                        return;
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Portapapeles ocupado — ignorar
                }
            }
        }

        var fillKey = e.Key;
        var fillMods = Keyboard.Modifiers;
        var notifyFill = (fillMods & ModifierKeys.Control) == ModifierKeys.Control
                         && fillKey is Key.D or Key.R;
        if (notifyFill)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (e.Handled && scVm != null)
                    scVm.NotifyGridCellsEdited();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    // 2+ filas en la misma columna → Ctrl+D es relleno Excel, no duplicar.
    private bool HasVerticalMultiRowFillSelection()
    {
        var validCells = CircuitosGrid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (validCells.Count < 2 || CircuitosGrid.Items.Count <= 1) return false;
        var distinctRows = validCells.Select(c => c.Item).Distinct().ToList();
        if (distinctRows.Count >= CircuitosGrid.Items.Count) return false;

        return validCells
            .GroupBy(c => c.Column)
            .Any(g => g.Select(c => c.Item).Distinct().Count() >= 2);
    }

    // Tras duplicar/agregar: enfocar una celda sin marcar toda la fila.
    private void FocusCircuitoWithoutRowSelect(CircuitoScRowVm row)
    {
        CircuitosGrid.SelectedCells.Clear();
        var col = CircuitosGrid.Columns.FirstOrDefault(c =>
                      c.Visibility == Visibility.Visible && !c.IsReadOnly)
                  ?? CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (col == null) return;
        int index = CircuitosGrid.Items.IndexOf(row);
        if (index < 0) return;
        DataGridExcelNavigation.FocusCell(CircuitosGrid, index, col);
        var cellInfo = new DataGridCellInfo(row, col);
        if (!CircuitosGrid.SelectedCells.Contains(cellInfo))
        {
            CircuitosGrid.SelectedCells.Add(cellInfo);
        }
    }

    // Artefactos / TMF / N° no se pegan como texto libre.
    private static bool IsNonPasteableColumn(DataGridColumn column)
    {
        if (column.IsReadOnly) return true;
        var header = column.Header?.ToString() ?? "";
        return header.Contains("ARTEFACT", StringComparison.OrdinalIgnoreCase)
            || header.Contains("TMF", StringComparison.OrdinalIgnoreCase)
            || header is "N°" or "Nº";
    }

    // Con SelectionUnit=Cell la fila activa a veces no llega a SelectedItem — sincronizar
    // desde CurrentCell para que Ctrl+D / Duplicar / heredar conductor funcionen.
    private void SyncSelectedCircuitoFromGrid(CubicacionScViewModel? scVm)
    {
        if (scVm == null) return;
        if (CircuitosGrid.CurrentCell.Item is CircuitoScRowVm row
            && !ReferenceEquals(scVm.SelectedCircuito, row))
        {
            scVm.SelectedCircuito = row;
        }
    }

    private bool IsCircuitosGridEditing()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
            return false;
        if (FindAncestor<DataGrid>(focused) != CircuitosGrid)
            return false;
        var cell = FindAncestor<DataGridCell>(focused);
        if (cell != null)
            return cell.IsEditing;
        return focused is TextBox || FindAncestor<ComboBox>(focused) != null;
    }
}
