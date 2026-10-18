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
        viewModel.CircuitoAdded += OnCircuitoAdded;
        viewModel.CircuitoRemoving += OnCircuitoRemoving;
        viewModel.RequestClearGridSelection += () =>
        {
            CircuitosGrid?.SelectedCells.Clear();
            if (CircuitosGrid != null)
                CircuitosGrid.CurrentCell = default;
        };
        // Confirma la celda activa del DataGrid ANTES de que LoadCircuitos() limpie Circuitos.
        // Sin esto, si el usuario tenía un TextBox en edición (ej. LargoTuberia) y hace clic en
        // otro tablero, el binding UpdateSourceTrigger=LostFocus no alcanza a propagar el valor
        // al row model porque la fila es destruida antes de que LostFocus dispare — el dato
        // ingresado se pierde silenciosamente y el circuito queda en 0.
        viewModel.RequestCommitGridEdit += () =>
        {
            if (CircuitosGrid == null || !IsCircuitosGridEditing()) return;
            if (Keyboard.FocusedElement is FrameworkElement focusedEditor)
            {
                var tb = focusedEditor as TextBox ?? FindDescendant<TextBox>(focusedEditor);
                var cb = FindAncestor<ComboBox>(focusedEditor) ?? focusedEditor as ComboBox;
                var editingCell = FindAncestor<DataGridCell>(focusedEditor);
                if (tb != null && editingCell?.Column != null && editingCell.DataContext is CircuitoScRowVm editingRow)
                    CommitCircuitoCellEditor(tb, cb, editingCell.Column, editingRow);
            }
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };
        // Al empezar a editar, dejar solo esa celda seleccionada — evita el “fila entera azul”
        // si antes había un rango o un circuito completo marcado vía N°.
        CircuitosGrid.BeginningEdit += OnCircuitosGridBeginningEdit;
        CircuitosGrid.PreparingCellForEdit += (_, e) =>
        {
            CircuitosGrid.SelectedCells.Clear();
            if (e.Row.Item != null && e.Column != null)
                CircuitosGrid.SelectedCells.Add(new DataGridCellInfo(e.Row.Item, e.Column));
        };
        CircuitosGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnCircuitosGridScrollChanged));
        CircuitosGrid.LayoutUpdated += OnCircuitosGridLayoutUpdated;
        CircuitosGrid.SizeChanged += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
        };
        CircuitosGrid.Loaded += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            EnsureActiveCellSelected();
        };

        DataContextChanged += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CubicacionScViewModel.SelectedCuadro) or nameof(CubicacionScViewModel.CircuitosFiltrados)
                or nameof(CubicacionScViewModel.IsPerspectiveGenerales) or nameof(CubicacionScViewModel.IsPerspectiveDeptos)
                or nameof(CubicacionScViewModel.ShowColUbicacion) or nameof(CubicacionScViewModel.ShowColBandeja))
            {
                UpdateColumnsVisibility();
                Dispatcher.BeginInvoke(new Action(EnsureActiveCellSelected), System.Windows.Threading.DispatcherPriority.Loaded);
                Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            if (e.PropertyName is nameof(CubicacionScViewModel.IsAdvancedMode))
            {
                UpdateColumnsVisibility();
                Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        CircuitosGrid.SelectedCellsChanged += (_, _) =>
        {
            if (CircuitosGrid.SelectedCells.Count == 0 && CircuitosGrid.Items.Count > 0)
            {
                Dispatcher.BeginInvoke(new Action(EnsureActiveCellSelected), System.Windows.Threading.DispatcherPriority.Input);
            }
        };

        PreviewKeyDown += UserControl_PreviewKeyDown;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (e.Key == Key.Escape && DataContext is CubicacionScViewModel scVmEsc && scVmEsc.HasSearchText)
        {
            if (scVmEsc.ClearSearchCommand.CanExecute(null)) scVmEsc.ClearSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && !alt && DataContext is CubicacionScViewModel scVm)
        {
            bool isPlus = e.Key is Key.Add or Key.OemPlus || (shift && e.Key is Key.OemPlus or Key.Oem1 or Key.OemQuestion);
            bool isMinus = !shift && e.Key is Key.Subtract or Key.OemMinus;

            if (isPlus && scVm.AddCircuitoCommand.CanExecute(null))
            {
                if (IsCircuitosGridEditing())
                {
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                scVm.AddCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (isMinus && scVm.RemoveCircuitoCommand.CanExecute(null))
            {
                if (IsCircuitosGridEditing())
                {
                    CircuitosGrid.CancelEdit();
                }
                scVm.RemoveCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }

    private void OnCircuitoAdded(string circuitoId)
    {
        Dispatcher.BeginInvoke(new Action(() => BeginEditNewCircuito(circuitoId)), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnCircuitoRemoving(CircuitoScRowVm row)
    {
        CircuitosGrid.CancelEdit();

        if (CircuitosGrid.SelectedCells.Any(c => ReferenceEquals(c.Item, row)))
        {
            CircuitosGrid.SelectedCells.Clear();
        }

        if (ReferenceEquals(CircuitosGrid.CurrentCell.Item, row))
        {
            CircuitosGrid.CurrentCell = default;
        }
    }

    private void BeginEditNewCircuito(string circuitoId)
    {
        var row = CircuitosGrid.Items.OfType<CircuitoScRowVm>().FirstOrDefault(r => r.Id == circuitoId);
        if (row == null)
            return;

        var column = CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                     ?? CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (column == null)
            return;

        CircuitosGrid.ScrollIntoView(row, column);
        CircuitosGrid.UpdateLayout();
        if (DataContext is CubicacionScViewModel scVm)
            scVm.SelectedCircuito = row;
        CircuitosGrid.CurrentCell = new DataGridCellInfo(row, column);
        CircuitosGrid.SelectedCells.Clear();
        CircuitosGrid.SelectedCells.Add(CircuitosGrid.CurrentCell);

        CircuitosGrid.Focus();
        Keyboard.Focus(CircuitosGrid);
        CircuitosGrid.BeginEdit();
    }

    private void OnCircuitosGridBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (Vm.IsGridReadOnly || !Vm.CanEdit)
            e.Cancel = true;
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
    // ── Colapsar / Expandir panel de tableros (Mueca) ─────────────────────────
    private bool _isSidebarCollapsed;
    private GridLength _savedSidebarWidth = new(170);

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        ApplySidebarCollapseState();
    }

    private void TablerosListCollapsed_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _isSidebarCollapsed = false;
        ApplySidebarCollapseState();
    }

    private void ApplySidebarCollapseState()
    {
        if (_isSidebarCollapsed)
        {
            if (TablerosColumn.Width.Value > 60)
                _savedSidebarWidth = TablerosColumn.Width;

            TablerosColumn.MinWidth = 48;
            TablerosColumn.MaxWidth = 48;
            TablerosColumn.Width = new GridLength(48);

            SidebarSplitter.Visibility = Visibility.Collapsed;
            SidebarMuecaIcon.Text = "▸";
            SidebarMuecaButton.ToolTip = "Expandir panel de tableros";

            TablerosHeaderExpanded.Visibility = Visibility.Collapsed;
            TablerosHeaderCollapsed.Visibility = Visibility.Visible;
            TablerosListExpanded.Visibility = Visibility.Collapsed;
            TablerosListCollapsed.Visibility = Visibility.Visible;
        }
        else
        {
            TablerosColumn.MinWidth = 130;
            TablerosColumn.MaxWidth = 300;
            TablerosColumn.Width = _savedSidebarWidth.Value >= 130 ? _savedSidebarWidth : new GridLength(170);

            SidebarSplitter.Visibility = Visibility.Visible;
            SidebarMuecaIcon.Text = "◂";
            SidebarMuecaButton.ToolTip = "Colapsar panel de tableros";

            TablerosHeaderExpanded.Visibility = Visibility.Visible;
            TablerosHeaderCollapsed.Visibility = Visibility.Collapsed;
            TablerosListExpanded.Visibility = Visibility.Visible;
            TablerosListCollapsed.Visibility = Visibility.Collapsed;
        }
    }

    // ── Renombrar tablero (sidebar) ──────────────────────────────────────────
    private void TableroItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.CanEdit) return;
        if (sender is ListBoxItem { DataContext: CuadroScRowVm vm })
            vm.IsEditing = true;
    }

    private void TableroEdit_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanEdit) return;
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

    internal const string CancelledEditTag = "cancelled";

    // Sist./Alim.: TextBox de texto libre con Binding Mode=OneWay (ver comentario en el
    // XAML) — el commit hacia el ViewModel se hace acá, a mano, en vez de confiar en
    // UpdateSourceTrigger=LostFocus de un binding TwoWay dentro de un DataGridTemplateColumn,
    // que no confirma de forma confiable al cerrar la celda con Tab/Enter. internal (no
    // private) para que CubicacionScViewCodeBehindTests pueda invocarlos directamente sin
    // reflection ni montar el DataGrid completo en modo edición.
    internal void TipoEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Tipo = textBox.Text;
    }

    internal void FaseEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Fase = textBox.Text;
    }

    // Ducto Ø, Conductor Band./PVC, Sección Band./PVC: ComboBox editable con lista filtrada
    internal void SeccionTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionTuberia = combo.Text;
    }

    internal void TipoTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.TipoTuberia = combo.Text;
    }

    internal void ConductorBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.ConductorDistribucion = combo.Text;
    }

    internal void SeccionBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionDistribucion = combo.Text;
    }

    internal void ConductorPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.ConductorDerivacion = combo.Text;
    }

    internal void SeccionPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionDerivacion = combo.Text;
    }

    // Tag del ComboBox = nombre de propiedad a confirmar (ConductorDistribucion, etc.).
    internal static List<string> GetMasterOptionsStatic(ComboBox combo)
    {
        if (ComboMasters.TryGetValue(combo, out var list) && list.Count > 0)
            return list;

        if (combo.ItemsSource is IEnumerable<string> strItems)
        {
            var masterList = strItems.ToList();
            ComboMasters.Remove(combo);
            ComboMasters.Add(combo, masterList);
            return masterList;
        }

        return new List<string>();
    }

    private List<string> GetMasterOptions(ComboBox combo)
    {
        if (ComboMasters.TryGetValue(combo, out var list) && list.Count > 0)
            return list;

        var vm = DataContext as CubicacionScViewModel ?? Vm;
        if (vm != null && combo.Tag is string tag)
        {
            IEnumerable<string>? source = tag switch
            {
                "Tipo" => vm.Tipos,
                "Fase" => vm.Fases,
                "TipoRecorrido" => vm.TiposRecorrido,
                "TipoBandeja" => vm.TiposBandeja,
                "TipoTuberia" => vm.TiposTuberia,
                "SeccionTuberia" => vm.DiametrosTuberia,
                "ConductorDistribucion" or "ConductorDerivacion" => vm.TiposConductor,
                "SeccionDistribucion" or "SeccionDerivacion" => vm.SeccionesConductor,
                _ => null
            };

            if (source != null)
            {
                var masterList = source.ToList();
                ComboMasters.Remove(combo);
                ComboMasters.Add(combo, masterList);
                return masterList;
            }
        }

        return GetMasterOptionsStatic(combo);
    }

    private void FilterableCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var combo = (ComboBox)sender;
        GetMasterOptions(combo);

        combo.DropDownOpened -= FilterableCombo_DropDownOpened;
        combo.DropDownOpened += FilterableCombo_DropDownOpened;

        // Enter/Tab/flechas con la misma semántica Excel que las celdas TextBox.
        var editor = FindDescendant<TextBox>(combo);
        if (editor == null) return;
        editor.PreviewKeyDown -= FilterableComboEditor_PreviewKeyDown;
        editor.PreviewKeyDown += FilterableComboEditor_PreviewKeyDown;
        editor.PreviewTextInput -= FilterableComboEditor_PreviewTextInput;
        editor.PreviewTextInput += FilterableComboEditor_PreviewTextInput;

        editor.Focus();
        editor.CaretIndex = editor.Text.Length;
        editor.SelectionLength = 0;
    }

    private void FilterableCombo_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var editor = FindDescendant<TextBox>(combo);
        if (editor == null) return;

        editor.CaretIndex = editor.Text.Length;
        editor.SelectionLength = 0;

        // Deshacer el SelectAll() que WPF ejecuta internamente en ComboBox.OnDropDownOpened
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            editor.CaretIndex = editor.Text.Length;
            editor.SelectionLength = 0;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FilterableComboEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox tb && tb.SelectionLength > 0 && tb.Text.Length > 0)
        {
            // Evitar que el siguiente carácter tecleado (ej. '5' después de '2') reemplace lo escrito
            tb.CaretIndex = tb.Text.Length;
            tb.SelectionLength = 0;
        }
    }

    // Filtra el desplegable a las opciones que CONTIENEN lo tecleado o cuyo prefijo numérico coincide.
    // NO altera combo.SelectedItem ni combo.Text durante el tecleo para permitir escribir libremente (ej. "25").
    private void FilterableCombo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isNavigatingFilterCombo) return;
        if (sender is not ComboBox combo) return;

        var master = GetMasterOptions(combo);
        if (master.Count == 0) return;

        var query = (combo.Text ?? "").Trim();
        var editorBox = FindDescendant<TextBox>(combo);
        int caret = editorBox?.CaretIndex ?? query.Length;

        string queryNum = query.Replace(',', '.');

        var filtered = string.IsNullOrEmpty(query)
            ? master
            : master.Where(x =>
                x.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (System.Text.RegularExpressions.Regex.IsMatch(x.Replace(',', '.'), @"^[0-9]+") &&
                 System.Text.RegularExpressions.Regex.Match(x.Replace(',', '.'), @"^([0-9]+(\.[0-9]+)?)").Value.StartsWith(queryNum, StringComparison.OrdinalIgnoreCase))
            )
            .OrderByDescending(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Length)
            .ToList();

        if (combo.ItemsSource is not List<string> current || current.Count != filtered.Count || !current.SequenceEqual(filtered, StringComparer.Ordinal))
        {
            combo.ItemsSource = filtered;
            if (editorBox != null)
            {
                editorBox.CaretIndex = caret;
                editorBox.SelectionLength = 0;
            }
        }

        if (combo.Text != query)
        {
            combo.Text = query;
            if (editorBox != null) editorBox.CaretIndex = Math.Min(caret, query.Length);
        }

        combo.IsDropDownOpen = filtered.Count > 0 && combo.IsKeyboardFocusWithin;
        if (editorBox != null && editorBox.SelectionLength > 0)
        {
            editorBox.CaretIndex = editorBox.Text.Length;
            editorBox.SelectionLength = 0;
        }

        editorBox?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (editorBox.IsKeyboardFocusWithin && editorBox.SelectionLength > 0)
            {
                editorBox.CaretIndex = editorBox.Text.Length;
                editorBox.SelectionLength = 0;
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    // Un Enter confirma y baja de fila; Tab avanza de celda. ↑↓ navegan opciones cuando está abierto.
    private void FilterableComboEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var combo = FindAncestor<ComboBox>(tb);
        if (combo == null) return;

        if (e.Key is Key.Up or Key.Down && combo.IsDropDownOpen && combo.Items.Count > 0)
        {
            int count = combo.Items.Count;
            int currentIdx = combo.SelectedIndex;
            int nextIdx = e.Key == Key.Down
                ? (currentIdx + 1 >= count ? 0 : currentIdx + 1)
                : (currentIdx - 1 < 0 ? count - 1 : currentIdx - 1);

            _isNavigatingFilterCombo = true;
            try
            {
                combo.SelectedIndex = nextIdx;
                if (combo.SelectedItem is string selText)
                {
                    tb.Text = selText;
                    tb.SelectAll();
                }
            }
            finally { _isNavigatingFilterCombo = false; }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }
            _cancelledFilterCombos.Add(combo);
            CircuitosGrid.CancelEdit(DataGridEditingUnit.Cell);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key == Key.Left && !(tb.CaretIndex == 0 || tb.SelectionLength == tb.Text.Length)) return;
            if (e.Key == Key.Right && !(tb.CaretIndex == tb.Text.Length || tb.SelectionLength == tb.Text.Length)) return;
        }
        else if (e.Key is not (Key.Enter or Key.Return or Key.Tab or Key.Up or Key.Down))
        {
            return;
        }

        var cell = FindAncestor<DataGridCell>(combo);
        if (cell?.Column == null) return;

        combo.IsDropDownOpen = false;
        ResolveFilterableComboText(combo);
        ApplyFilterableComboToRow(combo);

        var rowItem = combo.DataContext as CircuitoScRowVm ?? cell.DataContext as CircuitoScRowVm;
        int rowIndex = rowItem != null ? CircuitosGrid.Items.IndexOf(rowItem) : CircuitosGrid.Items.IndexOf(CircuitosGrid.CurrentCell.Item);

        if (e.Key is Key.Enter or Key.Return)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int target = rowIndex + ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            DataGridExcelNavigation.MoveToColumnOffset(CircuitosGrid, cell.Column, backward ? -1 : 1, wrapRows: true, rowItem);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int target = rowIndex + (e.Key == Key.Down ? 1 : -1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
        int offset = e.Key == Key.Right ? 1 : -1;
        DataGridExcelNavigation.MoveToColumnOffset(CircuitosGrid, cell.Column, offset, wrapRows: false, rowItem);
        e.Handled = true;
    }

    internal static string? FindBestCatalogMatch(IEnumerable<string> catalog, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        query = query.Trim();
        var list = catalog.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (list.Count == 0) return null;

        // 1. Igualdad exacta (case-insensitive)
        var exact = list.FirstOrDefault(x => string.Equals(x.Trim(), query, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 2. Coincidencia numérica exacta inicial (ej. "25" -> "25mm" o "25mm2", "2.5" -> "2.5mm2", "50" -> "50mm")
        string queryNum = query.Replace(',', '.');
        var numericExact = list.FirstOrDefault(x =>
        {
            string itemNum = x.Trim().Replace(',', '.');
            var m = System.Text.RegularExpressions.Regex.Match(itemNum, @"^([0-9]+(\.[0-9]+)?)");
            return m.Success && string.Equals(m.Groups[1].Value, queryNum, StringComparison.OrdinalIgnoreCase);
        });
        if (numericExact != null) return numericExact;

        // 3. Prefijo exacto (ej. "emt" -> "EMT", "ca" -> "CAÑERIA", "eva" -> "EVA")
        // Ordenado por longitud para preferir la opción más directa ("EVA" antes de "EVANOTOX")
        var startsWith = list.Where(x => x.Trim().StartsWith(query, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(x => x.Trim().Length)
                             .FirstOrDefault();
        if (startsWith != null) return startsWith;

        // 4. Subcadena contenida
        var contains = list.Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
                           .OrderBy(x => x.Trim().Length)
                           .FirstOrDefault();
        if (contains != null) return contains;

        return null;
    }

    private void ResolveFilterableComboText(ComboBox combo)
    {
        var master = GetMasterOptions(combo);
        var query = (combo.Text ?? "").Trim();

        if (master.Count > 0 && query.Length > 0)
        {
            var match = FindBestCatalogMatch(master, query);
            if (match != null)
            {
                combo.Text = match;
                combo.SelectedItem = match;
                return;
            }
        }

        // Si el usuario escribió un texto y no hay match en catálogo, RESPETAR el texto que escribió.
        // NUNCA sobreescribir con combo.SelectedItem si query no está vacío.
        if (string.IsNullOrWhiteSpace(query))
        {
            if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            {
                combo.Text = selected;
            }
        }
        else
        {
            combo.Text = query;
        }
    }

    internal void ApplyFilterableComboToRow(ComboBox combo)
    {
        if (_cancelledFilterCombos.Remove(combo)) return;
        if (combo.DataContext is not CircuitoScRowVm row || !row.CanEdit) return;
        ResolveFilterableComboText(combo);
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

    /// <summary>
    /// Confirma síncronamente los datos del editor de celda antes de que DataGrid.CommitEdit
    /// desmonte la plantilla visual, evitando cualquier pérdida de texto al moverse con el teclado.
    /// </summary>
    internal static void CommitCircuitoCellEditor(TextBox textBox, ComboBox? parentCombo, DataGridColumn column, CircuitoScRowVm row)
    {
        if (!row.CanEdit) return;

        if (parentCombo != null)
        {
            parentCombo.IsDropDownOpen = false;
            var master = GetMasterOptionsStatic(parentCombo);
            var query = (textBox.Text ?? "").Trim();
            string finalText = query;
            if (master.Count > 0 && query.Length > 0)
            {
                var match = FindBestCatalogMatch(master, query);
                if (match != null)
                    finalText = match;
            }

            parentCombo.Text = finalText;
            if (parentCombo.Tag is string target && !string.IsNullOrEmpty(target))
            {
                switch (target)
                {
                    case "ConductorDistribucion": row.ConductorDistribucion = finalText; break;
                    case "SeccionDistribucion": row.SeccionDistribucion = finalText; break;
                    case "TipoTuberia": row.TipoTuberia = finalText; break;
                    case "SeccionTuberia": row.SeccionTuberia = finalText; break;
                    case "ConductorDerivacion": row.ConductorDerivacion = finalText; break;
                    case "SeccionDerivacion": row.SeccionDerivacion = finalText; break;
                }
            }
            return;
        }

        var header = column.Header?.ToString() ?? "";
        string text = textBox.Text;
        if (header.Equals("SIST", StringComparison.OrdinalIgnoreCase))
        {
            row.Tipo = text;
        }
        else if (header.Equals("ALIM", StringComparison.OrdinalIgnoreCase))
        {
            row.Fase = text;
        }
        else if (header.Equals("UBICACIÓN", StringComparison.OrdinalIgnoreCase) || header.Equals("UBICACION", StringComparison.OrdinalIgnoreCase))
        {
            if (!row.TienePisos)
                row.Ubicacion = text;
        }
        else if (header.StartsWith("L. BAND", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.LargoBandeja = d;
        }
        else if (header.StartsWith("L. PVC", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.LargoTuberia = d;
        }
        else if (header.StartsWith("MONTAJ", StringComparison.OrdinalIgnoreCase) || header.Equals("LUMINARIA", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.MontajesAlumbrado = d;
        }
        else if (header.Equals("5/8\"", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas58 = d;
        }
        else if (header.Equals("A11", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas100x100 = d;
        }
        else if (header.Equals("A01", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas100x65 = d;
        }
    }

    private static bool TryConsumeEditor(object sender, out TextBox textBox, out CircuitoScRowVm row)
    {
        textBox = (TextBox)sender;
        row = null!;
        if (Equals(textBox.Tag, CancelledEditTag)) { textBox.Tag = null; return false; }
        if (textBox.DataContext is not CircuitoScRowVm rowVm) return false;
        row = rowVm;
        return true;
    }

    private bool TryConsumeFilterableCombo(object sender, out ComboBox combo, out CircuitoScRowVm row)
    {
        combo = (ComboBox)sender;
        row = null!;
        if (_cancelledFilterCombos.Remove(combo)) return false;
        if (combo.DataContext is not CircuitoScRowVm rowVm) return false;
        ResolveFilterableComboText(combo);
        row = rowVm;
        return true;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        return QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.FindAncestor<T>(start);
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

    private void OnCircuitosGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0 && BandScrollViewer != null)
        {
            BandScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        if (e.ViewportWidthChange != 0 || e.ExtentWidthChange != 0)
        {
            SyncBandColumnWidths();
        }
    }

    private void SyncBandColumnWidths()
    {
        if (CircuitosGrid == null || BandGrid == null || BandColNumero == null)
            return;

        double W(DataGridColumn? col) => (col != null && col.Visibility == Visibility.Visible) ? col.ActualWidth : 0;

        BandColNumero.Width = new GridLength(W(NumeroColumn), GridUnitType.Pixel);
        BandColSist.Width = new GridLength(W(ColSist), GridUnitType.Pixel);
        BandColAlim.Width = new GridLength(W(ColAlim), GridUnitType.Pixel);

        BandColUbicacion.Width = new GridLength(W(ColUbicacion), GridUnitType.Pixel);

        double wMedidas = W(ColLBand) + W(ColLPvc);
        BandColMedidasEntrada.Width = new GridLength(wMedidas, GridUnitType.Pixel);

        double wBandeja = W(ColCondBand) + W(ColSeccBand) + W(ColTotBand);
        BandColTramoBandeja.Width = new GridLength(wBandeja, GridUnitType.Pixel);

        double wPvc = W(ColTipoPvc) + W(ColDuctoPvc) + W(ColCondPvc) + W(ColSeccPvc) + W(ColTotPvc);
        BandColTramoPvc.Width = new GridLength(wPvc, GridUnitType.Pixel);

        double wMontaje = W(ColLuminaria);
        BandColMontaje.Width = new GridLength(wMontaje, GridUnitType.Pixel);

        double wCajas = W(ColCaja58) + W(ColCaja100) + W(ColCaja65);
        BandColCajas.Width = new GridLength(wCajas, GridUnitType.Pixel);

        double wTmf = W(ColTmfCant) + W(ColTmfMed);
        BandColTmf.Width = new GridLength(wTmf, GridUnitType.Pixel);

        double wArtefactos = W(ColArtefactos);
        BandColArtefactos.Width = new GridLength(wArtefactos, GridUnitType.Pixel);
    }

    private double _lastTotalGridWidth = -1;
    private void OnCircuitosGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (CircuitosGrid == null || BandGrid == null) return;
        double currentTotal = 0;
        int i = 1;
        foreach (var col in CircuitosGrid.Columns)
        {
            if (col.Visibility == Visibility.Visible)
            {
                currentTotal += col.ActualWidth * i;
                i++;
            }
        }

        if (Math.Abs(currentTotal - _lastTotalGridWidth) > 0.5)
        {
            _lastTotalGridWidth = currentTotal;
            SyncBandColumnWidths();
        }
    }

    private void UpdateColumnsVisibility()
    {
        if (CircuitosGrid == null) return;

        var vm = Vm;
        bool isGenerales = vm == null || vm.IsPerspectiveGenerales;
        bool isAdv = vm != null && vm.IsAdvancedMode;

        var deptoColVis = isGenerales ? Visibility.Visible : Visibility.Collapsed;
        var bandejaAdvVis = (isGenerales && isAdv) ? Visibility.Visible : Visibility.Collapsed;

        if (ColUbicacion != null) ColUbicacion.Visibility = deptoColVis;
        if (ColLBand != null) ColLBand.Visibility = deptoColVis;
        if (ColCondBand != null) ColCondBand.Visibility = bandejaAdvVis;
        if (ColSeccBand != null) ColSeccBand.Visibility = bandejaAdvVis;
        if (ColTotBand != null) ColTotBand.Visibility = deptoColVis;
        if (ColTmfCant != null) ColTmfCant.Visibility = deptoColVis;
        if (ColTmfMed != null) ColTmfMed.Visibility = deptoColVis;

        if (BandBorderUbicacion != null) BandBorderUbicacion.Visibility = deptoColVis;
        if (BandBorderTramoBandeja != null) BandBorderTramoBandeja.Visibility = deptoColVis;
        if (BandBorderTmf != null) BandBorderTmf.Visibility = deptoColVis;

        if (BandLabelMedidasEntrada != null)
        {
            BandLabelMedidasEntrada.Text = isGenerales ? "MEDIDAS ENTRADA" : "L. PVC";
        }

        SyncBandColumnWidths();
    }
}
