using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView : UserControl
{
    public CubicacionScView(CubicacionScViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DataGridExcelNavigation.Attach(CircuitosGrid);
        // Al empezar a editar, dejar solo esa celda seleccionada — evita el “fila entera azul”
        // si antes había un rango o un circuito completo marcado vía N°.
        CircuitosGrid.PreparingCellForEdit += (_, e) =>
        {
            CircuitosGrid.SelectedCells.Clear();
            if (e.Row.Item != null && e.Column != null)
                CircuitosGrid.SelectedCells.Add(new DataGridCellInfo(e.Row.Item, e.Column));
        };
    }

    private void OnMoreActionsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }

    // Circuito cuyo flyout de pisos está abierto ahora mismo (columna "Ubicación / Pisos"). Se
    // guarda al abrir (PisosBtn_Click), donde el DataContext SÍ es fiable — el botón vive en la
    // celda normal, fuera del Popup. De ahí en adelante, cerrar el flyout (Esc o clic afuera) usa
    // esta referencia directa en vez de buscar el DataContext desde dentro del Popup: un Popup
    // con AllowsTransparency="True" abre su contenido en una ventana Win32 aparte, y ni
    // VisualTreeHelper ni la herencia de DataContext cruzan esa frontera de forma confiable —
    // por eso el primer intento (buscar el ancestro Popup, y luego el DataContext del
    // ItemsControl) no cerraba nada: la condición nunca se cumplía y el flyout se quedaba abierto
    // en silencio.
    private CircuitoScRowVm? activePisosCircuito;

    // Sist./Alim./Ducto Ø/Conductor/Sección son DataGridTemplateColumn: a diferencia de una
    // columna de texto nativa, WPF no las pone en modo edición solo con escribir sobre la
    // celda seleccionada (hay que usar F2/doble clic) — limitación conocida, no algo
    // específico de esta app. Este handler intercepta el primer carácter tecleado y arranca
    // la edición a mano, ver DataGridExcelNavigation.TryBeginEditFromChar (encuentra el
    // TextBox interno tanto de un TextBox suelto como del PART_EditableTextBox de un ComboBox
    // editable, ambos son un TextBox real en el árbol visual).
    // ── Renombrar tablero (sidebar) ──────────────────────────────────────────
    private void TableroItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: CuadroScRowVm vm })
            vm.IsEditing = true;
    }

    private void TableroEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CuadroScRowVm vm })
            vm.IsEditing = true;
    }

    private void TableroNombre_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void TableroNombre_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: CuadroScRowVm vm })
            vm.IsEditing = false; // el binding LostFocus ya confirmó el nombre
    }

    private void TableroNombre_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not CuadroScRowVm vm) return;
        if (e.Key == Key.Enter)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            vm.IsEditing = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget(); // descarta lo tecleado
            vm.IsEditing = false;
            e.Handled = true;
        }
    }

    // ── Flyout de pisos (celda "Ubicación / Pisos") ──────────────────────────
    // La celda misma es un TextBox libre (un solo piso); el botón 🏢 de la esquina abre un
    // Popup anclado con las zonas del proyecto para cuando el circuito pasa por varios pisos.
    // Sin checkbox: cada zona se activa sola al escribirle un largo (ver ZonaPisoQuickItem).
    private void PisosBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CircuitoScRowVm vm }) return;
        if (DataContext is not CubicacionScViewModel scVm) return;
        OpenPisosFlyout(vm, scVm.ZonasProyecto);
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
            nodo = nodo is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(nodo)
                : LogicalTreeHelper.GetParent(nodo);
        }
        return false;
    }

    // internal (no private): la lógica de abrir/cerrar el flyout queda separada del evento de
    // clic/tecla que la dispara, para poder invocarla directo sin simular eventos de teclado o
    // mouse reales de WPF (frágil de construir en pruebas).
    internal void OpenPisosFlyout(CircuitoScRowVm vm, IReadOnlyList<ZonaProyecto> zonasProyecto)
    {
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
    private void PisosPopupInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (e.Key == Key.Escape)
        {
            ClosePisosFlyout();
            e.Handled = true;
            return;
        }

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
        // El flyout de pisos vive dentro de una celda del DataGrid, así que los eventos de tecla
        // TÚNELIZAN por el DataGrid antes de llegar a los TextBox del flyout. Sin esta guarda, al
        // escribir el primer número dentro del flyout, TryBeginEditFromChar arrancaba la edición
        // de la celda del DataGrid y robaba el foco → el popup (StaysOpen=False) se cerraba y
        // reabría (parpadeo). Mientras el flyout esté abierto, el DataGrid no toca el tecleo.
        if (activePisosCircuito != null) return;
        if (DataGridExcelNavigation.TryBeginEditFromChar(CircuitosGrid, e.Text))
            e.Handled = true;
    }

    private void CircuitosGrid_CurrentCellChanged(object? sender, EventArgs e)
    {
        if (DataContext is CubicacionScViewModel scVm)
            SyncSelectedCircuitoFromGrid(scVm);
    }

    // Clic en N° → selecciona todas las celdas de esa fila (circuito completo visible),
    // para que Ctrl+C copie el circuito entero. En el resto de columnas, selección normal.
    private void CircuitosGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject dep) return;
        var cell = FindAncestor<DataGridCell>(dep);
        if (cell?.Column == null || cell.DataContext is not CircuitoScRowVm row) return;
        if (!ReferenceEquals(cell.Column, NumeroColumn)) return;

        SelectEntireCircuitoRow(row);
        if (DataContext is CubicacionScViewModel scVm)
            scVm.SelectedCircuito = row;
        e.Handled = true;
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

    // Navegación estilo Excel + atajos de circuito.
    // Ctrl+C en N°/fila = copia circuito · Ctrl+V = pega SOBRE el circuito activo (no agrega).
    // Ctrl+D = duplica (agrega fila), salvo rango vertical 2+ filas → relleno Excel.
    private void CircuitosGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (activePisosCircuito != null) return;

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

        if (!editing && scVm != null && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Ctrl+D primero: duplicar circuito (salvo relleno vertical de 2+ filas).
            if (!shift && e.Key == Key.D)
            {
                if (!HasVerticalMultiRowFillSelection()
                    && scVm.DuplicateCircuitoCommand.CanExecute(null))
                {
                    scVm.DuplicateCircuitoCommand.Execute(null);
                    if (scVm.SelectedCircuito != null)
                        FocusCircuitoWithoutRowSelect(scVm.SelectedCircuito);
                    e.Handled = true;
                    return;
                }
                // Si hay rango vertical, deja que HandlePreviewKeyDown haga el fill.
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

        DataGridExcelNavigation.HandlePreviewKeyDown(CircuitosGrid, e);

        if (e.Handled
            && scVm != null
            && Keyboard.Modifiers == ModifierKeys.Control
            && e.Key is Key.D or Key.R)
        {
            scVm.NotifyGridCellsEdited();
        }
    }

    // 2+ filas en la misma columna → Ctrl+D es relleno Excel, no duplicar.
    private bool HasVerticalMultiRowFillSelection()
    {
        return CircuitosGrid.SelectedCells
            .Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly)
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
        return focused is TextBox || FindAncestor<ComboBox>(focused) != null;
    }

    internal const string CancelledEditTag = "cancelled";

    // Sist./Alim.: TextBox de texto libre con Binding Mode=OneWay (ver comentario en el
    // XAML) — el commit hacia el ViewModel se hace acá, a mano, en vez de confiar en
    // UpdateSourceTrigger=LostFocus de un binding TwoWay dentro de un DataGridTemplateColumn,
    // que no confirma de forma confiable al cerrar la celda con Tab/Enter. internal (no
    // private) para que CubicacionScViewCodeBehindTests pueda invocarlos directamente sin
    // reflection ni montar el DataGrid completo en modo edición.
    internal void TipoEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeEditor(sender, out TextBox textBox, out var row)) return;
        row.Tipo = textBox.Text;
    }

    internal void FaseEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeEditor(sender, out TextBox textBox, out var row)) return;
        row.Fase = textBox.Text;
    }

    // Ducto Ø, Conductor Band./PVC, Sección Band./PVC: ComboBox editable con lista filtrada
    // (ver FilterableCombo_Loaded/TextChanged) — mismo patrón de commit manual en LostFocus.
    internal void SeccionTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.SeccionTuberia = combo.Text;
    }

    internal void TipoTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.TipoTuberia = combo.Text;
    }

    internal void ConductorBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.ConductorDistribucion = combo.Text;
    }

    internal void SeccionBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.SeccionDistribucion = combo.Text;
    }

    internal void ConductorPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.ConductorDerivacion = combo.Text;
    }

    internal void SeccionPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryConsumeFilterableCombo(sender, out ComboBox combo, out var row)) return;
        row.SeccionDerivacion = combo.Text;
    }

    // Tag del ComboBox = nombre de propiedad a confirmar (ConductorDistribucion, etc.).
    // El catálogo maestro va en ComboMasters para no pelear con Escape/cancel.
    private void FilterableCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var combo = (ComboBox)sender;
        if (!ComboMasters.TryGetValue(combo, out _) && combo.ItemsSource is System.Collections.IEnumerable items)
            ComboMasters.Add(combo, items.Cast<string>().ToList());

        // Enter/Tab/flechas con la misma semántica Excel que las celdas TextBox.
        var editor = FindDescendant<TextBox>(combo);
        if (editor == null) return;
        editor.PreviewKeyDown -= FilterableComboEditor_PreviewKeyDown;
        editor.PreviewKeyDown += FilterableComboEditor_PreviewKeyDown;
    }

    // Filtra el desplegable a las opciones que CONTIENEN lo tecleado. Solo reasigna ItemsSource
    // si la lista filtrada cambió (evita tirones al teclear rápido).
    private void FilterableCombo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not ComboBox combo || !ComboMasters.TryGetValue(combo, out var master))
            return;

        var query = combo.Text ?? "";
        var editorBox = FindDescendant<TextBox>(combo);
        int caret = editorBox?.CaretIndex ?? query.Length;

        var filtered = string.IsNullOrEmpty(query)
            ? master
            : master.Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (combo.ItemsSource is not List<string> current
            || current.Count != filtered.Count
            || !current.SequenceEqual(filtered, StringComparer.Ordinal))
        {
            combo.ItemsSource = filtered;
        }

        if (combo.Text != query)
        {
            combo.Text = query;
            if (editorBox != null)
                editorBox.CaretIndex = Math.Min(caret, query.Length);
        }

        combo.IsDropDownOpen = filtered.Count > 0 && combo.IsKeyboardFocusWithin;
    }

    // Un Enter confirma y baja de fila; Tab avanza de celda — sin "doble Enter".
    private void FilterableComboEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var combo = FindAncestor<ComboBox>(tb);
        if (combo == null) return;

        // Con el desplegable abierto, ↑↓ navegan la lista nativa.
        if (combo.IsDropDownOpen && e.Key is Key.Up or Key.Down)
            return;

        if (e.Key is Key.Left or Key.Right)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key == Key.Left && !(tb.CaretIndex == 0 || tb.SelectionLength == tb.Text.Length))
                return;
            if (e.Key == Key.Right && !(tb.CaretIndex == tb.Text.Length || tb.SelectionLength == tb.Text.Length))
                return;
        }
        else if (e.Key is not (Key.Enter or Key.Return or Key.Tab or Key.Up or Key.Down))
        {
            return;
        }

        var cell = FindAncestor<DataGridCell>(combo);
        if (cell?.Column == null) return;

        ResolveFilterableComboText(combo);
        ApplyFilterableComboToRow(combo);

        if (e.Key is Key.Enter or Key.Return)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int rowIndex = CircuitosGrid.Items.IndexOf(CircuitosGrid.CurrentCell.Item);
            int target = rowIndex + ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            DataGridExcelNavigation.MoveToColumnOffset(
                CircuitosGrid, cell.Column, backward ? -1 : 1, wrapRows: true);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int rowIndex = CircuitosGrid.Items.IndexOf(CircuitosGrid.CurrentCell.Item);
            int target = rowIndex + (e.Key == Key.Down ? 1 : -1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            e.Handled = true;
            return;
        }

        // Left / Right en borde → confirmar y mover columna.
        CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        DataGridExcelNavigation.MoveToColumnOffset(
            CircuitosGrid, cell.Column, e.Key == Key.Right ? 1 : -1, wrapRows: false);
        e.Handled = true;
    }

    private static void ResolveFilterableComboText(ComboBox combo)
    {
        if (combo.SelectedItem is string selected)
        {
            combo.Text = selected;
            return;
        }

        var query = (combo.Text ?? "").Trim();
        if (query.Length == 0 || !ComboMasters.TryGetValue(combo, out var master))
            return;

        var matches = master
            .Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1)
        {
            combo.Text = matches[0];
            return;
        }

        var exact = matches.FirstOrDefault(x => x.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            combo.Text = exact;
    }

    private void ApplyFilterableComboToRow(ComboBox combo)
    {
        if (_cancelledFilterCombos.Remove(combo)) return;
        if (combo.DataContext is not CircuitoScRowVm row) return;
        if (combo.Tag is not string target || string.IsNullOrEmpty(target)) return;

        switch (target)
        {
            case "ConductorDistribucion": row.ConductorDistribucion = combo.Text; break;
            case "SeccionDistribucion": row.SeccionDistribucion = combo.Text; break;
            case "TipoTuberia": row.TipoTuberia = combo.Text; break;
            case "SeccionTuberia": row.SeccionTuberia = combo.Text; break;
            case "ConductorDerivacion": row.ConductorDerivacion = combo.Text; break;
            case "SeccionDerivacion": row.SeccionDerivacion = combo.Text; break;
        }
    }

    private static bool TryConsumeEditor(object sender, out TextBox textBox, out CircuitoScRowVm row)
    {
        textBox = (TextBox)sender;
        row = null!;
        if (Equals(textBox.Tag, CancelledEditTag))
        {
            textBox.Tag = null;
            return false;
        }
        if (textBox.DataContext is not CircuitoScRowVm rowVm)
            return false;
        row = rowVm;
        return true;
    }

    private bool TryConsumeFilterableCombo(object sender, out ComboBox combo, out CircuitoScRowVm row)
    {
        combo = (ComboBox)sender;
        row = null!;
        if (_cancelledFilterCombos.Remove(combo))
            return false;
        if (combo.DataContext is not CircuitoScRowVm rowVm)
            return false;
        row = rowVm;
        return true;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    // Busca el TextBox interno (PART_EditableTextBox) de un ComboBox editable ya realizado en
    // el árbol visual. Solo funciona después de que la plantilla se aplicó (ej. en el evento
    // Loaded o en un handler posterior, nunca en el constructor de la celda).
    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }
        return null;
    }

    // Junta todos los descendientes de tipo T en orden de árbol visual (usado por el flyout de
    // pisos para navegar ↑↓ entre los TextBox de L.Band/L.PVC de cada zona).
    private static void CollectDescendants<T>(DependencyObject root, List<T> results) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                results.Add(match);
            CollectDescendants(child, results);
        }
    }
}
