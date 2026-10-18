using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Converters;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView : UserControl
{
    private void FocusMotivoNoCubicarBox()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (MotivoNoCubicarBox == null)
                return;

            // Sale de la celda activa de la grilla para que el teclado escriba en la observación.
            try
            {
                ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch
            {
                // Ignorar: a veces no hay edición activa.
            }

            MotivoNoCubicarBox.Focus();
            Keyboard.Focus(MotivoNoCubicarBox);
            MotivoNoCubicarBox.CaretIndex = MotivoNoCubicarBox.Text?.Length ?? 0;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnRowAdded(string rowId)
    {
        Dispatcher.BeginInvoke(new Action(() => BeginEditNewRow(rowId)), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void BeginEditNewRow(string rowId)
    {
        var row = ResourceGrid.Items.OfType<CubicacionRowVm>().FirstOrDefault(r => r.Id == rowId);
        if (row == null)
            return;

        var column = CodigoColumn ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                     ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (column == null)
            return;

        ResourceGrid.ScrollIntoView(row, column);
        ResourceGrid.UpdateLayout();
        ResourceGrid.SelectedItem = row;
        ResourceGrid.CurrentCell = new DataGridCellInfo(row, column);
        ResourceGrid.Focus();

        if (!ResourceGrid.BeginEdit())
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DataGridExcelNavigation.FindEditingTextBox(ResourceGrid) is { } textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnPartidasPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm == null)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        if (key == Key.Escape && !string.IsNullOrEmpty(Vm.PartidaFilter))
        {
            if (Vm.ClearPartidaFilterCommand.CanExecute(null))
                Vm.ClearPartidaFilterCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl++ (y Ctrl+Shift++ en teclados donde + va con Shift) → agregar partida
        bool ctrl = mods.HasFlag(ModifierKeys.Control);
        bool alt = mods.HasFlag(ModifierKeys.Alt);
        bool shift = mods.HasFlag(ModifierKeys.Shift);
        if (ctrl && !alt && (key == Key.OemPlus || key == Key.Add))
        {
            if (Vm.AddPartidaCommand.CanExecute(null))
                Vm.AddPartidaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl+- → eliminar partida activa
        if (ctrl && !alt && !shift && (key == Key.OemMinus || key == Key.Subtract))
        {
            if (Vm.DeletePartidaCommand.CanExecute(null))
                Vm.DeletePartidaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Espacio → Alternar marcado para aplicación de plantilla en lote
        if (!ctrl && !alt && key == Key.Space && Keyboard.FocusedElement is not TextBox)
        {
            if (Vm.SelectedPartida != null && Vm.SelectedPartida.EsPartida)
            {
                Vm.SelectedPartida.IsTagged = !Vm.SelectedPartida.IsTagged;
                e.Handled = true;
            }
        }
    }

    private void OnMoreActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }

    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } && Vm != null)
        {
            Vm.ApplyObservationChip(text);
        }
    }

    private void OnClearObservationClick(object sender, RoutedEventArgs e)
    {
        Vm?.ClearObservation();
    }

    private void OnRecentObservationsClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && Vm != null)
        {
            var menu = new ContextMenu();
            var prefs = WpfAppHost.Current?.UserPreferencesStore?.Load() ?? new UserPreferences();
            var suggestions = ObservationMemoryStore.GetSuggestions(prefs);

            if (suggestions.Count == 0)
            {
                var emptyItem = new MenuItem { Header = "Sin observaciones recientes", IsEnabled = false };
                menu.Items.Add(emptyItem);
            }
            else
            {
                foreach (var obs in suggestions)
                {
                    var item = new MenuItem { Header = obs };
                    string captured = obs;
                    item.Click += (_, _) => Vm.ApplyObservationChip(captured);
                    menu.Items.Add(item);
                }
            }

            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void SetFieldEditingState(TextBox tb, bool isEditing)
    {
        _fieldEditingState[tb] = isEditing;
        if (tb == CantHeaderBox || tb.Tag as string == "PartidaCant") _cantEditingStarted = isEditing;
    }
    private bool IsFieldEditing(TextBox tb) => _fieldEditingState.TryGetValue(tb, out var isEditing) && isEditing;

    public CubicacionView(CubicacionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        BindItemRowCellWidths();
        ApplyGridMode();
        Loaded += OnLoaded;
        vm.RequestFocusMotivoNoCubicar += FocusMotivoNoCubicarBox;
        vm.RequestFillDown += HandleFillDown;
        vm.RequestFillRight += HandleFillRight;
        vm.RequestRepeatMeasurement += HandleRepeatMeasurement;
        vm.RequestFocusCubicacionZone += HandleFocusCubicacionZone;
        vm.RowAdded += OnRowAdded;
        // Seleccionar primer ítem del popup al abrirse
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CubicacionViewModel.AutocompleteVisible)
                && vm.AutocompleteVisible
                && AutocompleteList.Items.Count > 0)
            {
                FreezeAutocompleteHover();
                AutocompleteList.SelectedIndex = 0;
            }

            if (e.PropertyName is nameof(CubicacionViewModel.IsSectorMatrixMode)
                or nameof(CubicacionViewModel.VisibleSectorHeaders)
                or nameof(CubicacionViewModel.MuestraUf))
            {
                ApplyGridMode();
            }

            if (e.PropertyName == nameof(CubicacionViewModel.SelectedPartida))
            {
                ApplyGridMode();
                if (Vm.SelectedPartida != null)
                    PartidasList.ScrollIntoView(Vm.SelectedPartida);
                FocusFirstGridCell();
            }

            if (e.PropertyName == nameof(CubicacionViewModel.SelectedRow))
            {
                var rowVm = vm.SelectedRow;
                if (rowVm != null)
                {
                    DataGridColumn? col = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible ? CodigoColumn : null;
                    col ??= ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                           ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);

                    if (ResourceGrid.CurrentCell.Item != rowVm || ResourceGrid.CurrentCell.Column != col || ResourceGrid.SelectedCells.Count == 0)
                    {
                        int index = ResourceGrid.Items.IndexOf(rowVm);
                        if (index >= 0 && col != null)
                        {
                            DataGridExcelNavigation.FocusCell(ResourceGrid, index, col);
                        }
                    }
                }
                else
                {
                    ResourceGrid.SelectedCells.Clear();
                }
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedPartida != null)
            PartidasList.ScrollIntoView(Vm.SelectedPartida);

        // Sin foco inicial dentro de la vista, enfocar la grilla y la primera celda (Código)
        if (!IsKeyboardFocusWithin)
            FocusFirstGridCell();

        // Sincronizar desplazamiento horizontal entre la fila de cabecera (ItemRowHost) y las columnas de ResourceGrid
        ResourceGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, args) =>
        {
            if (args.HorizontalChange != 0 && ItemRowHost.RenderTransform is TranslateTransform tt)
            {
                tt.X = -args.HorizontalOffset;
            }
        }));
    }

    private void OnPartidaHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            BindingOperations.GetBindingExpression(tb, TextBox.TextProperty)?.UpdateSource();
            if (tb == PartidaCodigoBox) { PartidaDescripcionBox?.Focus(); PartidaDescripcionBox?.SelectAll(); }
            else { ResourceGrid?.Focus(); }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && sender is TextBox tbEsc)
        {
            BindingOperations.GetBindingExpression(tbEsc, TextBox.TextProperty)?.UpdateTarget();
            ResourceGrid?.Focus();
            e.Handled = true;
        }
    }

    // ── Teclado global ────────────────────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Escape cierra autocompletado o devuelve foco a la grilla
        if (e.Key == Key.Escape)
        {
            if (Vm.AutocompleteVisible)
            {
                Vm.AutocompleteVisible = false;
                _codigoEditor?.Focus();
            }
            e.Handled = true;
            return;
        }

        // Con Alt presionado WPF entrega Key.System; la tecla real va en SystemKey.
        if (CubicacionShortcutRouting.TryHandle(e, Vm))
        {
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        bool ctrl = mods.HasFlag(ModifierKeys.Control);
        bool alt = mods.HasFlag(ModifierKeys.Alt);
        bool shift = mods.HasFlag(ModifierKeys.Shift);

        // Ctrl++ → agregar partida
        if (ctrl && !alt && (key == Key.OemPlus || key == Key.Add || (shift && (key == Key.OemPlus || key == Key.Oem1 || key == Key.OemQuestion))))
        {
            if (Vm.AddPartidaCommand.CanExecute(null))
            {
                Vm.AddPartidaCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+- → eliminar partida activa
        if (ctrl && !alt && !shift && (key == Key.OemMinus || key == Key.Subtract))
        {
            if (Vm.DeletePartidaCommand.CanExecute(null))
            {
                Vm.DeletePartidaCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    if (Vm.SaveCommand.CanExecute(null))
                        ((ICommand)Vm.SaveCommand).Execute(null);
                    e.Handled = true;
                    return;
                case Key.V:
                    if (Keyboard.FocusedElement is not TextBoxBase || ReferenceEquals(Keyboard.FocusedElement, ResourceGrid))
                    {
                        OnPasteKeyDown(sender, e);
                        e.Handled = true;
                    }
                    return;
            }
        }
    }

    // Alinea cada celda de la fila de partida (modo Detalle) con el ancho real de su columna del
    // DataGrid, para que se lea como una sola tabla y siga alineada aunque el usuario redimensione
    // una columna. La última celda (Observaciones) llena el resto vía DockPanel.LastChildFill.
    private void BindItemRowCellWidths()
    {
        BindCellWidth(ItemRowCodigoCell, IndexColumn);
        BindCellWidth(ItemRowDescCell, DescripcionColumn);
        BindCellWidth(ItemRowUiCell, UnidadColumn);
        BindCellWidth(ItemRowCantCell, CantidadColumn);
        BindCellWidth(ItemRowMatCell, MaterialPriceColumn);
        BindCellWidth(ItemRowMoCell, LaborPriceColumn);
        BindCellWidth(ItemRowTotalCell, TotalPriceColumn);
        BindCellWidth(ItemRowSubtotalCell, SubtotalColumn);
        BindCellWidth(ItemRowGgUtCell, GgUtColumn);
        BindCellWidth(ItemRowUfUnitCell, UfUnitColumn);
        BindCellWidth(ItemRowSubtotalUfCell, SubtotalUfColumn);
    }

    private static void BindCellWidth(FrameworkElement cell, DataGridColumn column)
        => cell.SetBinding(FrameworkElement.WidthProperty,
            new Binding(nameof(DataGridColumn.ActualWidth)) { Source = column });

    // ── Cantidad oficial con fórmula tipo Excel (CantHeaderBox, modo Detalle) ──────────────
    // Esta celda es la Cantidad oficial de la partida y admite sumar/restar/multiplicar/dividir
    // los recursos de abajo (solo la columna CANT es "pickeable"), con el mismo modelo que una
    // planilla: clic/Tab SELECCIONA la celda (todo el texto resaltado, ↓/↑ todavía no hace
    // nada); recién al escribir el primer carácter empieza la EDICIÓN, y ahí sí:
    //   · ↓/↑ inserta o mueve una referencia (R1, R2...) en el punto donde está el cursor, y
    //     resalta esa fila en la grilla — sin robarle nunca el foco al cuadro de fórmula.
    //   · Escribir cualquier otra tecla (+, -, *, /, un número, borrar...) "confirma" esa
    //     referencia en el texto; el siguiente ↓/↑ empieza una referencia nueva. Así se arma
    //     "R1+R2-R3" tecleando sin soltar el cuadro de fórmula.
    //   · Clic directo en una celda CANT de un recurso hace lo mismo que ↓/↑ + tecla, en un
    //     solo paso (ver OnResourceGridPreviewMouseLeftButtonDown).
    // Enter evalúa, confirma y CIERRA la edición (↓/↑ ya no vuelve a agregar nada hasta que se
    // escriba de nuevo). Escape descarta los cambios. Supr/Backspace con todo seleccionado (recién
    // entrado a la celda) borra todo de un tiro, en vez de tener que borrar dígito por dígito.
    // Ctrl+←/→ recorre las 3 casillas editables de la fila de partida (Unidad, Cantidad,
    // Observaciones) — las únicas editables de esa fila; todo lo demás es solo texto. Al llegar
    // a una casilla queda todo el texto seleccionado, igual que al hacer clic en ella.
    private bool HandleExcelFieldPreviewKeyDown(TextBox tb, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            SetFieldEditingState(tb, true);
            tb.SelectionLength = 0;
            tb.CaretIndex = tb.Text.Length;
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Escape)
        {
            if (tb == CantHeaderBox || tb.Tag as string == "PartidaCant")
                CancelCantPick();
            else if (tb == UnidadHeaderBox || tb.Tag as string == "PartidaUnidad")
                CubicacionFormulaPicker.CancelUnidadPick(tb, Vm.Rows, Vm);
            SetFieldEditingState(tb, false);
            tb.SelectAll();
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (tb == CantHeaderBox || tb.Tag as string == "PartidaCant")
            {
                CommitCantHeaderBox(tb, force: true);
                CubicacionFormulaPicker.FocusGridCell(ResourceGrid, 0, CodigoColumn ?? CantidadColumn);
            }
            else if (tb == UnidadHeaderBox || tb.Tag as string == "PartidaUnidad")
            {
                CubicacionFormulaPicker.CommitUnidadPick(tb, Vm, null, force: true);
                ActiveCantBox.Focus();
                ActiveCantBox.SelectAll();
            }
            else
            {
                BindingOperations.GetBindingExpression(tb, TextBox.TextProperty)?.UpdateSource();
            }
            SetFieldEditingState(tb, false);
            tb.SelectAll();
            e.Handled = true;
            return true;
        }

        if (!IsFieldEditing(tb) && e.Key is Key.Delete or Key.Back)
        {
            tb.Text = "";
            SetFieldEditingState(tb, true);
            e.Handled = true;
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control || !IsFieldEditing(tb))
        {
            if (e.Key is Key.Left or Key.Right)
            {
                if (tb == UnidadHeaderBox || tb.Tag as string == "PartidaUnidad")
                {
                    if (e.Key == Key.Right) { ActiveCantBox.Focus(); ActiveCantBox.SelectAll(); e.Handled = true; return true; }
                }
                else if (tb == CantHeaderBox || tb.Tag as string == "PartidaCant")
                {
                    if (e.Key == Key.Left) { ActiveUnidadBox.Focus(); ActiveUnidadBox.SelectAll(); e.Handled = true; return true; }
                    else if (e.Key == Key.Right)
                    {
                        CommitCantHeaderBox(tb, force: true);
                        CubicacionFormulaPicker.FocusFirstCubicacionCell(ResourceGrid, _sectorColumns, CantidadColumn);
                        e.Handled = true; return true;
                    }
                }
                else if (tb == ObservacionHeaderBox && e.Key == Key.Left)
                {
                    ActiveCantBox.Focus(); ActiveCantBox.SelectAll(); e.Handled = true; return true;
                }
            }
        }

        bool isModifierOrNavigation = e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or
                                              Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.Tab or
                                              Key.Left or Key.Right or Key.Up or Key.Down or Key.F2 or Key.Enter or Key.Escape;

        if (!isModifierOrNavigation && !IsFieldEditing(tb))
        {
            SetFieldEditingState(tb, true);
        }

        return false;
    }

    // Unidad y Observaciones de la partida: al entrar queda todo seleccionado (como Excel) y
    // se navega con flechas directas (o Ctrl+←/→). No arman fórmula (eso es exclusivo de
    // Cantidad, ver OnCantHeaderBoxPreviewKeyDown).
    private void ClearGridSelection()
    {
        ResourceGrid.SelectedCells.Clear();
        ResourceGrid.SelectedItem = null;
        ResourceGrid.CurrentCell = default;
    }

    private void OnPartidaFieldGotFocus(object sender, RoutedEventArgs e) { ClearGridSelection(); if (sender is TextBox tb) { SetFieldEditingState(tb, false); SelectAllDeferred(tb); } }
    private void OnPartidaFieldPreviewKeyDown(object sender, KeyEventArgs e) { if (sender is TextBox tb) HandleExcelFieldPreviewKeyDown(tb, e); }
    private static void SelectAllDeferred(TextBox tb) => tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);

    private TextBox? _templateUnidadBox;
    private TextBox? _templateCantBox;
    private void OnUnidadHeaderBoxLoaded(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.Tag as string == "PartidaUnidad") _templateUnidadBox = tb; }
    private void OnCantHeaderBoxLoaded(object sender, RoutedEventArgs e) { if (sender is TextBox tb && tb.Tag as string == "PartidaCant") _templateCantBox = tb; }
    private TextBox ActiveUnidadBox => (Keyboard.FocusedElement as TextBox) is { } tb && (tb == UnidadHeaderBox || tb.Tag as string == "PartidaUnidad") ? tb : (Vm.IsSectorMatrixMode ? (_templateUnidadBox ?? UnidadHeaderBox) : UnidadHeaderBox);
    private TextBox ActiveCantBox => (Keyboard.FocusedElement as TextBox) is { } tb && (tb == CantHeaderBox || tb.Tag as string == "PartidaCant") ? tb : (Vm.IsSectorMatrixMode ? (_templateCantBox ?? CantHeaderBox) : CantHeaderBox);

    private void OnHeaderTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (sender is TextBox tb && !tb.IsKeyboardFocused) { ClearGridSelection(); tb.Focus(); SelectAllDeferred(tb); e.Handled = true; } }
    private void OnUnidadHeaderBoxGotFocus(object sender, RoutedEventArgs e) { var tb = sender as TextBox ?? ActiveUnidadBox; ClearGridSelection(); CubicacionFormulaPicker.SetActiveUnidadBox(tb, Vm); SetFieldEditingState(tb, false); SelectAllDeferred(tb); }
    private void OnUnidadHeaderBoxLostFocus(object sender, RoutedEventArgs e)
    {
        UnidadSuggestionsPopup.IsOpen = false;
        CubicacionFormulaPicker.CommitUnidadPick(sender as TextBox ?? ActiveUnidadBox, Vm);
    }

    private void OnUnidadHeaderBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsKeyboardFocused)
            CubicacionFormulaPicker.ShowUnidadSuggestions(tb, UnidadSuggestionsPopup, UnidadSuggestionsList);
    }

    private void OnUnidadSuggestionClick(object sender, MouseButtonEventArgs e)
    {
        if (UnidadSuggestionsList.SelectedItem is CubicacionFormulaPicker.UnidadSuggestion sug) ApplyUnidadSuggestion(sug);
    }

    private void ApplyUnidadSuggestion(CubicacionFormulaPicker.UnidadSuggestion sug)
    {
        var tb = ActiveUnidadBox;
        if (tb != null) { tb.Text = sug.Code; if (Vm != null) Vm.PartidaUnidadText = sug.Code; }
        UnidadSuggestionsPopup.IsOpen = false;
        ActiveCantBox.Focus();
        ActiveCantBox.SelectAll();
    }

    private void OnUnidadHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox ?? ActiveUnidadBox;
        if (CubicacionFormulaPicker.HandleUnidadPopupKey(e, UnidadSuggestionsPopup, UnidadSuggestionsList, ApplyUnidadSuggestion)) return;
        if (e.Key == Key.Escape) { CubicacionFormulaPicker.CancelUnidadPick(tb, Vm.Rows, Vm, ResourceGrid); SetFieldEditingState(tb, false); tb.SelectAll(); e.Handled = true; return; }
        if (CubicacionFormulaPicker.HandleUnidadNavigation(tb, e, Vm, ResourceGrid, UnidadColumn, () => { ActiveCantBox.Focus(); ActiveCantBox.SelectAll(); })) return;
        HandleExcelFieldPreviewKeyDown(tb, e);
    }

    private void OnCantHeaderBoxGotFocus(object sender, RoutedEventArgs e) { var tb = sender as TextBox ?? ActiveCantBox; ClearGridSelection(); CubicacionFormulaPicker.SetActiveCantBox(tb, Vm); SetFieldEditingState(tb, false); SelectAllDeferred(tb); }
    private void OnCantHeaderBoxLostFocus(object sender, RoutedEventArgs e) => CommitCantHeaderBox(sender as TextBox ?? ActiveCantBox);
    private void OnCantHeaderBoxPreviewTextInput(object sender, TextCompositionEventArgs e) => CubicacionFormulaPicker.NotifyCantTextInput(e.Text);

    private void OnCantHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox ?? ActiveCantBox;
        CubicacionFormulaPicker.NotifyCantKeyDown(e.Key);
        if (CubicacionFormulaPicker.HandleCantNavigation(tb, e, Vm, ResourceGrid, CantidadColumn, () => { ActiveUnidadBox.Focus(); ActiveUnidadBox.SelectAll(); }, () => CommitCantHeaderBox(tb, force: true), () => { CommitCantHeaderBox(tb, force: true); CubicacionFormulaPicker.FocusFirstCubicacionCell(ResourceGrid, _sectorColumns, CantidadColumn); }, _cantEditingStarted)) return;
        HandleExcelFieldPreviewKeyDown(tb, e);
    }

    private bool _isCommittingCant;
    private bool _isCancellingCant;
    private void CommitCantHeaderBox(TextBox? target = null, bool force = false)
    {
        if (_isCommittingCant || _isCancellingCant || (!force && CubicacionFormulaPicker.SuppressLostFocusCommit)) return;
        _isCommittingCant = true;
        try
        {
            var box = target ?? ActiveCantBox;
            FinalizeCantPick();
            _cantEditingStarted = false;
            if (box != null && Vm != null) { Vm.PartidaCantidadText = box.Text ?? ""; box.Text = Vm.PartidaCantidadText; }
            if (box != null) BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateTarget();
            if (box != CantHeaderBox && CantHeaderBox != null) { CantHeaderBox.Text = Vm?.PartidaCantidadText ?? ""; BindingOperations.GetBindingExpression(CantHeaderBox, TextBox.TextProperty)?.UpdateTarget(); }
            if (_templateCantBox != null && _templateCantBox != box) { _templateCantBox.Text = Vm?.PartidaCantidadText ?? ""; BindingOperations.GetBindingExpression(_templateCantBox, TextBox.TextProperty)?.UpdateTarget(); }
        }
        finally { _isCommittingCant = false; }
    }

    private void ApplyCantPick(int newIndex) => CubicacionFormulaPicker.ApplyCantPick(ActiveCantBox, newIndex, Vm.Rows, ResourceGrid, CantidadColumn);
    private void FinalizeCantPick() => CubicacionFormulaPicker.FinalizeCantPick(Vm.Rows, ResourceGrid);
    private void CancelCantPick()
    {
        _isCancellingCant = true;
        try { CubicacionFormulaPicker.CancelCantPick(ActiveCantBox, Vm.Rows, Vm, ResourceGrid); }
        finally { Dispatcher.BeginInvoke(new Action(() => _isCancellingCant = false), System.Windows.Threading.DispatcherPriority.Background); }
    }
    private void ClearCantPickHighlight() => CubicacionFormulaPicker.ClearCantPickHighlight(Vm.Rows, ResourceGrid);

    // Clic directo en una celda CANT de un recurso mientras CantHeaderBox tiene el foco →
    // inserta/reemplaza su referencia y la confirma en un solo paso (equivale a ↓/↑ hasta esa
    // fila y luego escribir un operador). Cualquier otro clic sigue el comportamiento normal.
    private void OnResourceGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Alt+clic en una casilla de la matriz de sectores (modo Casillas): abre la Hoja de
        // Cubicación en modo selección y trae el valor elegido directo a esa casilla.
        if (Vm.IsSectorMatrixMode && Keyboard.Modifiers == ModifierKeys.Alt &&
            e.OriginalSource is DependencyObject altDep)
        {
            var altCell = FindParent<DataGridCell>(altDep);
            int slot = altCell != null ? _sectorColumns.IndexOf(altCell.Column) : -1;
            if (altCell != null && slot >= 0 && altCell.DataContext is CubicacionRowVm sectorRow)
            {
                Vm.PickSectorValueFromHojaCubicacion(sectorRow, slot);
                e.Handled = true;
                return;
            }
        }

        if (e.OriginalSource is not DependencyObject dep) return;
        var cell = FindParent<DataGridCell>(dep);
        if (cell == null || cell.DataContext is not CubicacionRowVm targetRow) return;

        int rowIndex = ResourceGrid.Items.IndexOf(targetRow);
        var focusedTb = Keyboard.FocusedElement as TextBox;

        // 1. Clic para tomar unidad en UD de partida (cabecera Casillas o Detalle)
        bool isUnidadActive = CubicacionFormulaPicker.IsUnidadActive ||
                              (focusedTb != null && (focusedTb == UnidadHeaderBox || focusedTb.Tag as string == "PartidaUnidad"));
        if (isUnidadActive && rowIndex >= 0)
        {
            var box = CubicacionFormulaPicker.ActiveUnidadBox ?? focusedTb ?? UnidadHeaderBox;
            CubicacionFormulaPicker.SetActiveUnidadBox(box, Vm);
            CubicacionFormulaPicker.ApplyUnidadPick(box, rowIndex, Vm.Rows, ResourceGrid, UnidadColumn);
            if (e.ClickCount >= 2)
            {
                CubicacionFormulaPicker.CommitUnidadPick(box, Vm, ResourceGrid, force: true);
                ActiveCantBox.Focus();
                ActiveCantBox.SelectAll();
                e.Handled = true;
                return;
            }
            box.Focus();
            e.Handled = true;
            return;
        }

        // 1b. Clic mientras se edita celda de recurso en columna UD
        if (focusedTb != null && FindParent<DataGridCell>(focusedTb)?.Column == UnidadColumn && CubicacionFormulaPicker.TryPickUnidad(focusedTb, targetRow, UnidadHeaderBox, Vm))
        {
            e.Handled = true;
            return;
        }

        // 2. Clic para tomar casillas en CANT (modo Detalle o modo Casillas)
        bool isCantEditing = (focusedTb != null && (focusedTb == CantHeaderBox || focusedTb.Tag as string == "PartidaCant"))
                             || _cantEditingStarted || CubicacionFormulaPicker.IsCantActive;
        if (isCantEditing && rowIndex >= 0 && cell.Column == CantidadColumn)
        {
            var box = CubicacionFormulaPicker.ActiveCantBox ?? focusedTb ?? ActiveCantBox;
            CubicacionFormulaPicker.SetActiveCantBox(box, Vm);
            CubicacionFormulaPicker.PickCantCell(box, rowIndex, Vm.Rows, ResourceGrid, CantidadColumn);
            _cantEditingStarted = true;
            box.Focus();
            e.Handled = true;
            return;
        }

        // 3. Clic para tomar casillas mientras se edita una casilla de sector
        if (focusedTb != null && FindParent<DataGridCell>(focusedTb) is { } editingCell && _sectorColumns.Contains(editingCell.Column) && cell != editingCell)
        {
            if (focusedTb.Text.StartsWith("=") || focusedTb.Text.StartsWith("+") || CubicacionSectorFormulaController.EndsWithFormulaOperator(focusedTb))
            {
                if (rowIndex >= 0 && CubicacionFormulaPicker.TryPickSector(focusedTb, rowIndex, _sectorColumns.IndexOf(cell.Column)))
                {
                    _sectorFormulaController.RefreshSectorReferenceHighlights(focusedTb, Vm);
                    e.Handled = true;
                    return;
                }
            }
        }

        // 4. Clic simple en celda editable (ej: CÓDIGO o casillas): entra de inmediato en modo edición
        // tipo Excel sin requerir doble clic ni F2.
        if (!Vm.IsReadOnly && !cell.IsEditing && !cell.Column.IsReadOnly)
        {
            if (!cell.IsFocused)
            {
                cell.Focus();
            }
            ResourceGrid.CurrentCell = new DataGridCellInfo(cell);
            ResourceGrid.BeginEdit();
        }
    }

    private void OnResourceGridPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Vm.IsReadOnly) return;
        if (DataGridExcelNavigation.TryBeginEditFromChar(ResourceGrid, e.Text))
        {
            e.Handled = true;
        }
    }

    // Arma al vuelo el menú contextual de una casilla de la matriz de sectores, con la opción
    // "Ir a Hoja de Cubicación" — solo cuando el clic derecho realmente cae en una celda de datos
    // de sector (no en "#", "UD", "CANT" ni fuera de una fila). Se reconstruye cada vez porque la
    // celda destino (fila + sector) cambia según dónde se hizo clic.
    private void OnResourceGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!Vm.IsSectorMatrixMode) return;
        if (e.OriginalSource is not DependencyObject dep) return;

        var cell = FindParent<DataGridCell>(dep);
        int slot = cell != null ? _sectorColumns.IndexOf(cell.Column) : -1;
        if (cell == null || slot < 0 || cell.DataContext is not CubicacionRowVm row)
            return;

        var menuItem = new MenuItem { Header = "Ir a Hoja de Cubicación", Tag = (row, slot) };
        menuItem.Click += OnGoToHojaCubicacionClick;
        cell.ContextMenu = new ContextMenu { ItemsSource = new object[] { menuItem } };
    }

    // "Ir a Hoja de Cubicación" del menú contextual de una casilla de sector: misma acción que
    // Alt+clic, pero disparada desde el clic derecho. El MenuItem lleva la celda (fila + slot)
    // en su Tag, puesto ahí al abrir el menú (ver OnResourceGridContextMenuOpening).
    private void OnGoToHojaCubicacionClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not (CubicacionRowVm row, int absoluteOffset)) return;
        Vm.PickSectorValueFromHojaCubicacion(row, absoluteOffset);
    }

    // ── DataGrid ─────────────────────────────────────────────────────────
    private ProjectFileData? _cellPreEditSnapshot;

    private void OnGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (Vm.IsReadOnly)
        {
            e.Cancel = true;
            return;
        }

        _cellPreEditSnapshot = Vm.CaptureSnapshot();

        if (e.Row.Item is not CubicacionRowVm row) return;
    }

    private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if ((ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn)) && e.EditingElement is TextBox tb)
        {
            _codigoEditor = tb;
            tb.TextChanged += OnCodigoEditorTextChanged;
            tb.PreviewKeyDown += OnCodigoEditorPreviewKeyDown;

            // Vincular el popup con la celda activa que se está editando
            AutocompletePopup.PlacementTarget = tb;

            // Si la edición empezó tecleando un carácter, ese carácter ya quedó en el
            // TextBox con el cursor detrás; seleccionar todo aquí lo dejaría marcado y la
            // siguiente tecla lo borraría en vez de continuar escribiendo el código.
            if (e.EditingEventArgs is KeyEventArgs or System.Windows.Input.TextCompositionEventArgs)
                tb.CaretIndex = tb.Text.Length;
            else
                tb.SelectAll();
        }
    }

    private void OnCodigoEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        // Escribir código o descripción = modo teclado: el mouse quieto no debe robar el ítem marcado.
        FreezeAutocompleteHover();
        if (sender is TextBox tb && ResourceGrid.CurrentItem is CubicacionRowVm row)
        {
            if (ReferenceEquals(ResourceGrid.CurrentCell.Column, CodigoColumn))
                _ = Vm.NotifyCodigoChangedAsync(row, tb.Text);
            else
                _ = Vm.SearchMaterialsAsync(tb.Text);
        }
    }

    private void OnCodigoEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Vm.AutocompleteVisible) return;

        switch (e.Key)
        {
            case Key.Down:
                // Mover selección en el popup hacia abajo sin perder foco del editor
                MoveAutocompleteSelection(+1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveAutocompleteSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                var targetEnter = AutocompleteList.SelectedItem as AutocompleteSuggestionVm ?? Vm.AutocompleteFirstItem;
                if (targetEnter != null)
                {
                    Vm.ApplyMaterialCommand.Execute(targetEnter.Material);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    e.Handled = true;
                    int nextIdx = ResourceGrid.SelectedIndex + 1;
                    var col = CodigoColumn ?? ResourceGrid.CurrentCell.Column;
                    if (nextIdx > 0 && nextIdx < ResourceGrid.Items.Count && col != null)
                    {
                        ResourceGrid.Dispatcher.BeginInvoke(new Action(() => DataGridExcelNavigation.FocusCell(ResourceGrid, nextIdx, col)), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
                else if (sender is TextBox directBox && ResourceGrid.CurrentItem is CubicacionRowVm directRow)
                {
                    // Si el usuario escribió un código a mano que no tiene sugerencia en el popup,
                    // guardar directamente el texto escrito en la fila y confirmar.
                    directRow.Codigo = directBox.Text;
                    Vm.AutocompleteVisible = false;
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    e.Handled = true;
                    int nextIdx = ResourceGrid.SelectedIndex + 1;
                    var col = CodigoColumn ?? ResourceGrid.CurrentCell.Column;
                    if (nextIdx > 0 && nextIdx < ResourceGrid.Items.Count && col != null)
                    {
                        ResourceGrid.Dispatcher.BeginInvoke(new Action(() => DataGridExcelNavigation.FocusCell(ResourceGrid, nextIdx, col)), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
                break;

            case Key.Tab:
                // Tab con autocompletado abierto → aplica el seleccionado explícitamente,
                // o el primero solo si es coincidencia exacta (ver Key.Enter arriba).
                if (AutocompleteList.SelectedItem is AutocompleteSuggestionVm first)
                    Vm.ApplyMaterialCommand.Execute(first.Material);
                else if (Vm.AutocompleteFirstItemIsExactMatch && Vm.AutocompleteFirstItem is { } fallback)
                    Vm.ApplyMaterialCommand.Execute(fallback.Material);
                // No marcar Handled para que Tab avance a la siguiente celda
                break;

            case Key.Escape:
                Vm.AutocompleteVisible = false;
                e.Handled = true;
                break;
        }
    }

    private void MoveAutocompleteSelection(int delta)
    {
        int count = AutocompleteList.Items.Count;
        if (count == 0) return;

        FreezeAutocompleteHover();
        int current = AutocompleteList.SelectedIndex;
        int next = Math.Clamp(current + delta, 0, count - 1);
        AutocompleteList.SelectedIndex = next;
        AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
    }

    /// <summary>
    /// Congela el hover: la selección solo la mueve ↑↓ hasta que el mouse se mueva de verdad.
    /// </summary>
    private void FreezeAutocompleteHover()
    {
        _autocompleteHoverArmed = false;
        _autocompleteHoverFreezePos = Mouse.GetPosition(AutocompleteList);
    }

    private void OnGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        _sectorFormulaController.OnCellEditEnding(Vm);

        if ((ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn)) && e.EditingElement is TextBox tb)
        {
            tb.TextChanged -= OnCodigoEditorTextChanged;
            tb.PreviewKeyDown -= OnCodigoEditorPreviewKeyDown;
            if (ReferenceEquals(_codigoEditor, tb))
                _codigoEditor = null;
            AutocompletePopup.PlacementTarget = null;
            Vm.AutocompleteVisible = false;
        }

        var preEditSnapshot = _cellPreEditSnapshot;
        _cellPreEditSnapshot = null;

        if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is CubicacionRowVm row)
        {
            // Forzar explícito el valor tecleado en SectorValues[slot] en vez de confiar en que
            // el binding TwoWay (UpdateSourceTrigger=LostFocus) de la columna lo empuje solo —
            // reportado en uso real: escribir una fórmula en una casilla y confirmar (Enter/Tab/
            // flecha) podía dejar la celda en 0 porque el valor tecleado nunca llegaba a
            // SectorValues. Esto garantiza el commit sin depender del timing interno de WPF.
            int slot = _sectorColumns.IndexOf(e.Column);
            if (slot >= 0 && e.EditingElement is TextBox sectorBox)
            {
                row.SectorValues[slot] = sectorBox.Text;
                RecordMeasurement(sectorBox.Text);
            }
            else if (ReferenceEquals(e.Column, CodigoColumn) && e.EditingElement is TextBox codigoBox)
            {
                row.Codigo = codigoBox.Text;
                RecordMeasurement(codigoBox.Text);
            }
            else if (ReferenceEquals(e.Column, UnidadColumn) && e.EditingElement is TextBox unidadBox)
            {
                string resolved = Vm.ResolveUnidadReference(unidadBox.Text);
                if (resolved != unidadBox.Text)
                {
                    unidadBox.Text = resolved;
                    row.Unidad = resolved;
                }
                RecordMeasurement(unidadBox.Text);
            }
            else if (ReferenceEquals(e.Column, CantidadColumn) && e.EditingElement is TextBox cantBox)
            {
                if (QuantityFormulaService.IsFormulaLike(cantBox.Text))
                {
                    var vars = Vm.BuildPartidaCantidadVariables();
                    var res = QuantityFormulaService.Evaluate(cantBox.Text, vars);
                    if (res.Success)
                    {
                        cantBox.Text = res.Value % 1 == 0 ? res.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : res.Value.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture);
                        row.Cantidad = res.Value;
                    }
                }
                RecordMeasurement(cantBox.Text);
            }
            else if (e.EditingElement is TextBox generalBox)
            {
                RecordMeasurement(generalBox.Text);
            }

            Vm.OnRowEdited(row, preEditSnapshot);
        }
    }

    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Si el evento de teclado proviene de un control secundario fuera de las celdas/filas del DataGrid
        // (por ejemplo el TextBox de cabecera de partida UD o CANT en modo Casillas), no debe ser
        // interceptado por los comandos de fila/celda de la grilla (evitando borrar filas o alterar selecciones).
        if (e.OriginalSource is DependencyObject dep && dep != ResourceGrid && FindParent<DataGridCell>(dep) == null && FindParent<DataGridRow>(dep) == null)
        {
            return;
        }

        if (CubicacionFormulaPicker.TryNavigateUpFromFirstRow(ResourceGrid, e, ActiveUnidadBox, ActiveCantBox, CantidadColumn, _sectorColumns)) return;

        var editing = IsGridEditing();
        if (editing && MatchesConfiguredShortcut(e, CubicacionShortcutCatalog.PartidaPrevious, CubicacionShortcutCatalog.PartidaNext, CubicacionShortcutCatalog.QuickJump))
        { ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true); ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true); }

        if (CubicacionShortcutRouting.TryHandle(e, Vm))
        {
            e.Handled = true;
            return;
        }

        // Ctrl+V se atiende aquí: el pegado Excel marca PreviewKeyDown como atendido y eso suprime el KeyDown que burbujea hasta el UserControl.
        if (!editing && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            OnPasteKeyDown(ResourceGrid, e);
            e.Handled = true;
            return;
        }

        if (!editing && e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.X)
        {
            DataGridExcelNavigation.CopySelectionToClipboard(ResourceGrid);
            if (e.Key == Key.X)
            {
                var cells = ResourceGrid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
                if (cells.Count > 0)
                {
                    var rows = cells.Select(c => c.Item as CubicacionRowVm).Where(r => r != null).Distinct().ToList();
                    Vm.RunBatchedEdit("Cortar celdas", () =>
                    {
                        DataGridExcelNavigation.ClearSelection(ResourceGrid);
                        foreach (var row in rows)
                        {
                            if (row != null)
                            {
                                Vm.OnRowEdited(row);
                            }
                        }
                    });
                    CommandManager.InvalidateRequerySuggested();
                }
            }
            e.Handled = true;
            return;
        }

        if (!IsGridEditing() && e.Key is Key.Delete or Key.Back)
        {
            var cells = ResourceGrid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
            if (cells.Count > 0)
            {
                var rows = cells.Select(c => c.Item as CubicacionRowVm).Where(r => r != null).Distinct().ToList();
                Vm.RunBatchedEdit("Borrar celdas", () =>
                {
                    DataGridExcelNavigation.ClearSelection(ResourceGrid);
                    foreach (var row in rows)
                    {
                        if (row != null)
                        {
                            Vm.OnRowEdited(row);
                        }
                    }
                });
                CommandManager.InvalidateRequerySuggested();
                e.Handled = true;
                return;
            }
        }

        // Mientras el helper de navegación reconstruye la selección (Shift+Flecha, Ctrl+A,
        // etc.) suprimimos SelectionChanged/CurrentCellChanged: sin esto, cada celda agregada
        // a SelectedCells disparaba un recorrido LINQ completo de la selección (O(n²) para un
        // rectángulo de n celdas), sintiéndose "pegado" al seleccionar sectores. Se sincroniza
        // una sola vez al terminar.
        _suppressSelectionSync = true;
        bool handledBySector;
        try
        {
            handledBySector = HandleSectorNavigation(e);
        }
        finally
        {
            _suppressSelectionSync = false;
        }

        if (handledBySector)
        {
            SyncGridSelectionFromGrid();
            return;
        }

        if (e.Handled)
        {
            return;
        }

        var modifiers = e.KeyboardDevice.Modifiers;
        // Con Alt presionado WPF entrega Key.System; la tecla real va en SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Flecha Arriba en fila 1 (sin editar): sube el foco a las casillas de cabecera (UD / CANT)
        if (!editing && modifiers == ModifierKeys.None && e.Key == Key.Up)
        {
            int currentRowIdx = ResourceGrid.Items.IndexOf(ResourceGrid.CurrentCell.Item ?? ResourceGrid.SelectedItem);
            if (currentRowIdx == 0)
            {
                var targetBox = (ResourceGrid.CurrentCell.Column == CantidadColumn || _sectorColumns.Contains(ResourceGrid.CurrentCell.Column))
                    ? ActiveCantBox : ActiveUnidadBox;
                targetBox.Focus();
                targetBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        // Insert o Ctrl+N → agregar nueva fila
        if ((e.Key == Key.Insert && modifiers == ModifierKeys.None && !editing) || (e.Key == Key.N && modifiers == ModifierKeys.Control && !editing))
        {
            if (Vm.AddRowCommand.CanExecute(null))
                Vm.AddRowCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Delete → eliminar filas seleccionadas (sin editar)
        if (e.Key == Key.Delete && modifiers == ModifierKeys.None && !editing)
        {
            if (Vm.DeleteRowCommand.CanExecute(null))
                Vm.DeleteRowCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl+D → duplicar fila seleccionada (solo si hay a lo sumo 1 fila activa, no si se seleccionó todo)
        if (e.Key == Key.D && modifiers == ModifierKeys.Control && !editing)
        {
            if (Vm.DuplicateRowCommand.CanExecute(null) && ResourceGrid.SelectedItems.Count <= 1 && ResourceGrid.SelectedCells.Count <= 1)
                Vm.DuplicateRowCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl + (OEM Plus o Add del numpad) → insertar fila en la posición actual
        if (modifiers == ModifierKeys.Control && (key == Key.OemPlus || key == Key.Add))
        {
            if (Vm.InsertRowCommand.CanExecute(null))
            {
                if (editing)
                {
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                Vm.InsertRowCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Ctrl - (OEM Minus o Subtract del numpad) → eliminar fila(s) activa(s)
        if (modifiers == ModifierKeys.Control && (key == Key.OemMinus || key == Key.Subtract))
        {
            if (Vm.DeleteRowCommand.CanExecute(null))
            {
                if (editing)
                {
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                Vm.DeleteRowCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }
    }

    private static bool MatchesConfiguredShortcut(KeyEventArgs e, params string[] actionIds) =>
        CubicacionShortcutRouting.Matches(e, actionIds);

    public bool TryHandleConfiguredShortcut(KeyEventArgs e) => CubicacionShortcutRouting.TryHandle(e, Vm);

    // Público: usado también por ShellWindow como respaldo cuando Alt+↑/↓ se presiona con el
    // foco fuera de esta vista (ej. en un botón del marco de la ventana).
    public void NavigatePartida(int delta)
    {
        MoveSelectedPartida(delta);
    }

    private bool IsPartidaHeaderFocused()
    {
        var focused = Keyboard.FocusedElement;
        return focused == UnidadHeaderBox || focused == CantHeaderBox || focused == ObservacionHeaderBox
            || (focused is TextBox tb && (tb.Tag as string == "PartidaUnidad" || tb.Tag as string == "PartidaCant"));
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync)
            return;
        SyncGridSelectionFromGrid();

        if (ResourceGrid.SelectedCells.Count == 0 && ResourceGrid.Items.Count > 0 && !IsPartidaHeaderFocused())
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ResourceGrid.SelectedCells.Count == 0 && ResourceGrid.Items.Count > 0 && !IsPartidaHeaderFocused())
                {
                    FocusFirstGridCell();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnGridCurrentCellChanged(object sender, EventArgs e)
    {
        if (_suppressSelectionSync)
            return;

        if (ResourceGrid.CurrentCell.Column == IndexColumn && ResourceGrid.CurrentCell.Item != null)
        {
            var targetCol = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible
                ? CodigoColumn
                : ResourceGrid.Columns.FirstOrDefault(c => c != IndexColumn && c.Visibility == Visibility.Visible);
            if (targetCol != null)
            {
                var cell = new DataGridCellInfo(ResourceGrid.CurrentCell.Item, targetCol);
                _suppressSelectionSync = true;
                try
                {
                    ResourceGrid.CurrentCell = cell;
                    ResourceGrid.SelectedCells.Clear();
                    ResourceGrid.SelectedCells.Add(cell);
                }
                finally
                {
                    _suppressSelectionSync = false;
                }
                DataGridExcelNavigation.FocusCell(ResourceGrid, ResourceGrid.Items.IndexOf(ResourceGrid.CurrentCell.Item), targetCol);
                SyncGridSelectionFromGrid();
                return;
            }
        }

        if (ResourceGrid.CurrentCell.IsValid && ResourceGrid.CurrentCell.Column != null && ResourceGrid.CurrentCell.Item != null)
        {
            if (ResourceGrid.SelectedCells.Count == 0 ||
                (ResourceGrid.SelectedCells.Count == 1 && !ResourceGrid.SelectedCells.Contains(ResourceGrid.CurrentCell)))
            {
                _suppressSelectionSync = true;
                try
                {
                    ResourceGrid.SelectedCells.Clear();
                    ResourceGrid.SelectedCells.Add(ResourceGrid.CurrentCell);
                }
                finally
                {
                    _suppressSelectionSync = false;
                }
            }
        }

        SyncGridSelectionFromGrid();
    }

    private void SyncGridSelectionFromGrid()
    {
        List<CubicacionRowVm> selected;
        if (ResourceGrid.SelectedCells.Count > 100)
            selected = ResourceGrid.SelectedItems.OfType<CubicacionRowVm>().Distinct().ToList();
        else
            selected = ResourceGrid.SelectedCells.Select(c => c.Item).OfType<CubicacionRowVm>().Distinct().ToList();

        if (selected.Count == 0 && ResourceGrid.CurrentCell.Item is CubicacionRowVm currentFromCell)
            selected.Add(currentFromCell);

        Vm.SyncGridRowSelection(selected);

        CubicacionRowVm? activeRow = ResourceGrid.CurrentCell.Item as CubicacionRowVm
            ?? ResourceGrid.SelectedItem as CubicacionRowVm;
        Vm.SelectedRow = activeRow;

        // Al cambiar de celda activa, cerrar el autocompletado para evitar molestias visuales
        Vm.AutocompleteVisible = false;
    }

    // ── Pegado desde Excel ────────────────────────────────────────────────
    private void OnPasteClick(object sender, RoutedEventArgs e, bool showPreview = false)
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();

        if (!showPreview && Vm.TryPasteAnchored(ResourceGrid, text))
        {
            DataGridExcelNavigation.ClearCopyMarker(ResourceGrid);
            return;
        }

        // El ancla no cae en Código/Descripción/UD/Cantidad (esas ya las maneja
        // TryPasteAnchored arriba, con su propia lógica de negocio): pegado genérico
        // estilo Excel, usado sobre todo por la matriz de sectores (S1, S2, TA-P2…).
        if (!showPreview)
        {
            bool pasted = false;
            var selectedCells = ResourceGrid.SelectedCells.Where(c => c.IsValid && c.Column != null).ToList();
            var rows = selectedCells.Select(c => c.Item as CubicacionRowVm).Where(r => r != null).Distinct().ToList();

            Vm.RunBatchedEdit("Pegar celdas", () =>
            {
                pasted = DataGridExcelNavigation.TryPasteIntoSelection(
                    ResourceGrid, text,
                    col => CubicacionViewModel.IsFixedPasteColumn(col));
                if (pasted)
                {
                    foreach (var row in rows)
                    {
                        if (row != null)
                            Vm.OnRowEdited(row);
                    }
                }
            });
            if (pasted)
            {
                DataGridExcelNavigation.ClearCopyMarker(ResourceGrid);
                return;
            }
        }

        Vm.PasteClipboardDirect(text, showPreview);
        DataGridExcelNavigation.ClearCopyMarker(ResourceGrid);
    }

    private void OnPasteToolbarClick(object sender, RoutedEventArgs e)
        => OnPasteClick(sender, e, showPreview: true);

    private void OnPasteKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase tb && !ReferenceEquals(tb, ResourceGrid))
            return;
        OnPasteClick(sender, e, showPreview: false);
    }

    private void OnCreateMaterialClick(object sender, RoutedEventArgs e)
        => Vm.BeginQuickAddMaterial();

    private void OnTipoSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged también se dispara cuando el BINDING actualiza el combo al cambiar
        // de fila seleccionada (no solo cuando el usuario elige un tipo). Sin este guard, el
        // solo hecho de recorrer filas marcaba el proyecto como "Cambios sin guardar" y
        // empujaba puntos de undo espurios. El teclado/mouse del usuario siempre deja el
        // foco dentro del combo; las actualizaciones programáticas del binding, no.
        if (sender is not ComboBox combo || !combo.IsKeyboardFocusWithin)
            return;

        // El combo de Tipo bindea directo a SelectedRow.Tipo (no pasa por RightPanelVm), así
        // que hay que avisar al ViewModel para que recalcule totales y marque cambios pendientes.
        if (Vm.SelectedRow != null)
            Vm.OnRowEdited(Vm.SelectedRow);
    }

    // ── Autocompletado (clic / hover con ratón) ───────────────────────────
    // Un solo clic aplica el material. PreviewMouseLeftButtonDown (no Up/DoubleClick):
    // con el popup abierto, el clic sale del TextBox y el DataGrid cierra la edición
    // antes del MouseUp; hay que aplicar en el preview. Teclado intacto.
    private void OnAutocompleteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject dep) return;

        var listItem = FindParent<ListBoxItem>(dep);
        if (listItem?.DataContext is not AutocompleteSuggestionVm suggestion)
            return;

        AutocompleteList.SelectedItem = suggestion;
        if (_codigoEditor != null)
            _codigoEditor.Text = suggestion.Material.Codigo;
        Vm.ApplyMaterialCommand.Execute(suggestion.Material);
        ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        e.Handled = true;
    }

    // Al pasar el mouse, la selección sigue al ítem — pero solo tras un movimiento real.
    // Si el puntero está quieto sobre la lista (p. ej. encima del código), el teclado manda.
    private void OnAutocompleteMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(AutocompleteList);
        if (!_autocompleteHoverArmed)
        {
            var dx = pos.X - _autocompleteHoverFreezePos.X;
            var dy = pos.Y - _autocompleteHoverFreezePos.Y;
            if (dx * dx + dy * dy < 9) // < ~3 px: ruido / mouse quieto
                return;
            _autocompleteHoverArmed = true;
        }

        if (e.OriginalSource is not DependencyObject dep) return;
        var listItem = FindParent<ListBoxItem>(dep);
        if (listItem?.DataContext is not AutocompleteSuggestionVm suggestion) return;
        if (ReferenceEquals(AutocompleteList.SelectedItem, suggestion)) return;

        AutocompleteList.SelectedItem = suggestion;
        AutocompleteList.ScrollIntoView(suggestion);
    }

    private void OnAutocompleteKeyDown(object sender, KeyEventArgs e)
    {
        if (AutocompleteList.Items.Count == 0)
            return;

        switch (e.Key)
        {
            case Key.Down:
                MoveAutocompleteSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveAutocompleteSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                var targetListEnter = AutocompleteList.SelectedItem as AutocompleteSuggestionVm
                    ?? Vm.AutocompleteFirstItem;
                if (targetListEnter != null)
                {
                    Vm.ApplyMaterialCommand.Execute(targetListEnter.Material);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    e.Handled = true;
                }
                break;
            case Key.Tab:
                if (AutocompleteList.SelectedItem is AutocompleteSuggestionVm material)
                {
                    Vm.ApplyMaterialCommand.Execute(material.Material);
                    e.Handled = true;
                }
                else if (Vm.AutocompleteFirstItemIsExactMatch && Vm.AutocompleteFirstItem is { } first)
                {
                    Vm.ApplyMaterialCommand.Execute(first.Material);
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                Vm.AutocompleteVisible = false;
                _codigoEditor?.Focus();
                e.Handled = true;
                break;
            default:
                _codigoEditor?.Focus();
                break;
        }
    }

    // ── Selección de partida desde la lista ───────────────────────────────
    private void OnPartidaSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PartidasList.SelectedItem is not PartidaListItemVm partida)
            return;

        // Confirmar edición activa en el DataGrid antes de cambiar de partida
        if (IsGridEditing())
        {
            ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        if (!partida.EsPartida)
        {
            // Los títulos de sección no son seleccionables: el ViewModel ya ignoró la
            // asignación, pero el ListBox (binding TwoWay) igual marca el título como
            // "seleccionado" visualmente. Reponer la selección en la partida activa real.
            PartidasList.SelectedItem = Vm.SelectedPartida;
            return;
        }

        if (ReferenceEquals(Vm.SelectedPartida, partida))
            return;

        Vm.SelectedPartida = partida;
        PartidasList.ScrollIntoView(partida);
        FocusFirstGridCell();
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private bool IsGridEditing()
    {
        // Solo puede haber una fila en edición a la vez; verificar el elemento con foco
        // es O(1) en vez de iterar todos los contenedores del DataGrid.
        return Keyboard.FocusedElement is TextBox tb && FindParent<DataGridCell>(tb) != null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        return QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.FindAncestor<T>(child);
    }

    // Alterna entre los títulos normales (Casillas) y encabezados delgados sin texto (Detalle,
    // donde la fila de partida ya cumple ese rol) para las columnas compartidas entre ambos
    // modos. Mantener el encabezado nativo VISIBLE — aunque en blanco — conserva el tirador de
    // redimensionar columnas con el mouse; con HeadersVisibility="None" se perdía por completo.
    private void SetSharedColumnHeaders(bool thin)
    {
        var thinStyle = thin ? (Style)FindResource("DataGridColumnHeader.Thin") : null;
        SetColumnHeader(CodigoColumn, thin, "CÓDIGO", thinStyle);
        SetColumnHeader(DescripcionColumn, thin, "DESCRIPCIÓN", thinStyle);
        if (thin)
        {
            UnidadColumn.HeaderTemplate = null; SetColumnHeader(UnidadColumn, true, "", thinStyle);
            CantidadColumn.HeaderTemplate = null; SetColumnHeader(CantidadColumn, true, "", thinStyle);
        }
        else
        {
            var ph = (Style)FindResource("DataGridColumnHeader.PartidaHeader");
            UnidadColumn.HeaderTemplate = (DataTemplate)FindResource("HeaderTemplate.PartidaUnidad");
            UnidadColumn.Header = null; UnidadColumn.HeaderStyle = ph;
            CantidadColumn.HeaderTemplate = (DataTemplate)FindResource("HeaderTemplate.PartidaCantidad");
            CantidadColumn.Header = null; CantidadColumn.HeaderStyle = ph;
        }
        SetColumnHeader(MaterialPriceColumn, thin, "MAT $", thinStyle);
        SetColumnHeader(LaborPriceColumn, thin, "MO $", thinStyle);
        SetColumnHeader(TotalPriceColumn, thin, "$ UNIT", thinStyle);
        SetColumnHeader(SubtotalColumn, thin, "SUBTOTAL", thinStyle);
        SetColumnHeader(GgUtColumn, thin, "GG+UT", thinStyle);
        SetColumnHeader(UfUnitColumn, thin, "UF UNIT", thinStyle);
        SetColumnHeader(SubtotalUfColumn, thin, "SUBTOTAL UF", thinStyle);
        SetColumnHeader(ObservacionesColumn, thin, "OBSERVACIONES", thinStyle);
    }

    private static void SetColumnHeader(DataGridColumn column, bool thin, string normalText, Style? thinStyle)
    {
        column.Header = thin ? "" : normalText;
        column.HeaderStyle = thinStyle;
    }

    private void ApplyGridMode()
    {
        if (!Vm.IsSectorMatrixMode)
        {
            // Ocultar columnas de sector instantáneamente sin destruirlas
            foreach (var column in _sectorColumns)
                column.Visibility = Visibility.Collapsed;

            // Modo Detalle: la tabla del boceto — orden ITEM/DESCRIPCIÓN/UNIDAD/CANTIDAD/MAT/MO/
            // $ UNIT/SUBTOTAL/GG+UT/UF UNIT. Se ocultan los encabezados nativos del DataGrid (la
            // fila de partida de arriba hace de encabezado + datos editables) y el panel/totales
            // de abajo.
            if (ResourceGrid.Columns.Contains(CodigoColumn)) ResourceGrid.Columns.Remove(CodigoColumn);
            if (!ResourceGrid.Columns.Contains(MaterialPriceColumn)) ResourceGrid.Columns.Insert(4, MaterialPriceColumn);
            if (!ResourceGrid.Columns.Contains(LaborPriceColumn)) ResourceGrid.Columns.Insert(5, LaborPriceColumn);
            if (!ResourceGrid.Columns.Contains(TotalPriceColumn)) ResourceGrid.Columns.Insert(6, TotalPriceColumn);
            if (!ResourceGrid.Columns.Contains(SubtotalColumn)) ResourceGrid.Columns.Insert(7, SubtotalColumn);
            if (!ResourceGrid.Columns.Contains(GgUtColumn)) ResourceGrid.Columns.Insert(8, GgUtColumn);
            if (!ResourceGrid.Columns.Contains(UfUnitColumn)) ResourceGrid.Columns.Insert(9, UfUnitColumn);
            if (!ResourceGrid.Columns.Contains(SubtotalUfColumn)) ResourceGrid.Columns.Insert(10, SubtotalUfColumn);
            if (!ResourceGrid.Columns.Contains(ObservacionesColumn)) ResourceGrid.Columns.Add(ObservacionesColumn);
            MaterialPriceColumn.Visibility = Visibility.Visible;
            LaborPriceColumn.Visibility = Visibility.Visible;
            TotalPriceColumn.Visibility = Visibility.Visible;
            SubtotalColumn.Visibility = Visibility.Visible;
            GgUtColumn.Visibility = Visibility.Visible;
            // Política de moneda: en un proyecto en CLP no hay columnas UF (UNIT/SUBTOTAL).
            var ufVisibility = Vm.MuestraUf ? Visibility.Visible : Visibility.Collapsed;
            UfUnitColumn.Visibility = ufVisibility;
            SubtotalUfColumn.Visibility = ufVisibility;
            ItemRowUfUnitCell.Visibility = ufVisibility;
            ItemRowSubtotalUfCell.Visibility = ufVisibility;
            ObservacionesColumn.Visibility = Visibility.Visible;
            IndexColumn.Visibility = Visibility.Visible;
            UnidadColumn.Visibility = Visibility.Visible;      // unidad del recurso — editable
            CantidadColumn.Visibility = Visibility.Visible;    // cantidad del recurso — editable
            // CANTIDAD editable en Detalle: bindea directo a Cantidad (decimal, TwoWay) en vez de
            // CantidadDisplay (string de solo lectura, usado en Casillas donde la cantidad se
            // arma sola desde las casillas de sector).
            CantidadColumn.IsReadOnly = false;
            CantidadColumn.Binding = new Binding(nameof(CubicacionRowVm.Cantidad))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                StringFormat = "#,##0.##"
            };
            // Encabezados nativos invisibles en Detalle (la fila de partida ya hace de encabezado)
            ResourceGrid.HeadersVisibility = DataGridHeadersVisibility.None;
            SetSharedColumnHeaders(thin: true);
            ResourceGrid.FrozenColumnCount = 5;
            // Totales CLP visibles también en Detalle: MANO DE OBRA = Σ (MO $ × cantidad).
            // Solo se oculta la barra de atajos de teclado.
            SetBottomChromeVisible(showTotals: true, showStatus: false);
            DataGridExcelNavigation.SetColumnScope(ResourceGrid, null);
            return;
        }

        // Modo Casillas: queda EXACTAMENTE como estaba. Encabezados nativos visibles, "#", "UD",
        // "CANT", panel DETALLE, totales y estado; se ocultan las columnas exclusivas de Detalle.
        ResourceGrid.Columns.Remove(MaterialPriceColumn);
        ResourceGrid.Columns.Remove(LaborPriceColumn);
        ResourceGrid.Columns.Remove(TotalPriceColumn);
        ResourceGrid.Columns.Remove(SubtotalColumn);
        ResourceGrid.Columns.Remove(GgUtColumn);
        ResourceGrid.Columns.Remove(UfUnitColumn);
        ResourceGrid.Columns.Remove(SubtotalUfColumn);
        ResourceGrid.Columns.Remove(ObservacionesColumn);
        if (!ResourceGrid.Columns.Contains(CodigoColumn)) ResourceGrid.Columns.Insert(1, CodigoColumn);
        CodigoColumn.Visibility = Visibility.Visible;
        IndexColumn.Visibility = Visibility.Visible;
        IndexColumn.Header = "#";
        UnidadColumn.Visibility = Visibility.Visible;
        UnidadColumn.IsReadOnly = true;
        CantidadColumn.Visibility = Visibility.Visible;
        CantidadColumn.IsReadOnly = true;
        CantidadColumn.Binding = new Binding(nameof(CubicacionRowVm.CantidadDisplay));
        ResourceGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        SetSharedColumnHeaders(thin: false);
        CancelCantPick();
        _cantEditingStarted = false;
        ResourceGrid.FrozenColumnCount = 5;
        SetBottomChromeVisible(showTotals: true, showStatus: true);

        // Calculamos cuántas columnas de sectores deben estar visibles:
        // Solo las zonas activas del proyecto (con nombre), más cualquier slot que tenga datos ingresados.
        // Si no hay zonas creadas y no hay datos, visibleCount = 0 (no se muestran casillas vacías a la derecha).
        int activeZoneCount = 0;
        for (int i = 0; i < Vm.VisibleSectorHeaders.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(Vm.VisibleSectorHeaders[i]))
                activeZoneCount = i + 1;
        }

        int maxDataSlot = -1;
        if (Vm.Rows != null)
        {
            foreach (var r in Vm.Rows)
            {
                var exprs = r.Source?.QtyExpressions;
                if (exprs != null)
                {
                    for (int s = exprs.Length - 1; s >= 0; s--)
                    {
                        if (!string.IsNullOrWhiteSpace(exprs[s]))
                        {
                            if (s > maxDataSlot) maxDataSlot = s;
                            break;
                        }
                    }
                }
            }
        }

        int visibleCount = Math.Max(activeZoneCount, maxDataSlot + 1);
        var sectorHeaderStyle = (Style)FindResource("DataGridColumnHeader.CasillasGeneral");

        // Si las columnas de sector ya fueron instanciadas, las mostramos instantáneamente
        // actualizando sus etiquetas sin reconstruir 300 columnas en el árbol visual.
        if (_sectorColumns.Count > 0)
        {
            for (int slot = 0; slot < _sectorColumns.Count; slot++)
            {
                var column = _sectorColumns[slot];
                bool shouldBeVisible = slot < visibleCount;
                column.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                if (column.HeaderStyle != sectorHeaderStyle)
                    column.HeaderStyle = sectorHeaderStyle;

                if (shouldBeVisible)
                {
                    string headerText = slot < Vm.VisibleSectorHeaders.Count
                        ? Vm.VisibleSectorHeaders[slot]
                        : "";
                    if (column.Header is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb)
                    {
                        if (tb.Text != headerText)
                        {
                            tb.Text = headerText;
                            // ToolTip vacío = popup navy sin texto (píldora negra flotante).
                            sp.ToolTip = string.IsNullOrEmpty(headerText) ? null : headerText;
                        }
                    }
                }
            }
        }
        else
        {
            var elementStyle = new Style(typeof(TextBlock));
            elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            elementStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3, 0, 7, 0)));

            var editingStyle = new Style(typeof(TextBox));
            editingStyle.Setters.Add(new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            editingStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(3, 0, 5, 0)));

            var baseCellStyle = (Style)FindResource("DataGridCell.Excel");
            var surf2Brush = (System.Windows.Media.Brush)FindResource("Brush.Surface2");
            var blueSoftBrush = (System.Windows.Media.Brush)FindResource("Brush.BlueSoft");
            var blueBrush = (System.Windows.Media.Brush)FindResource("Brush.Blue");

            for (int slot = 0; slot < CubicacionViewModel.SectorWindowSize; slot++)
            {
                int absoluteOffset = slot;
                string headerText = slot < Vm.VisibleSectorHeaders.Count ? Vm.VisibleSectorHeaders[slot] : "";
                bool shouldBeVisible = slot < visibleCount;

                var header = new StackPanel
                {
                    // ToolTip vacío = popup navy sin texto (píldora negra flotante en la grilla).
                    ToolTip = string.IsNullOrEmpty(headerText) ? null : headerText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                header.Children.Add(new TextBlock
                {
                    Text = headerText,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    MaxWidth = 50,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var cellStyle = new Style(typeof(DataGridCell), baseCellStyle);
                cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, surf2Brush));

                var pickTrigger = new DataTrigger
                {
                    Binding = new Binding(nameof(CubicacionRowVm.FormulaPickTargetOffsets))
                    {
                        Converter = SectorPickOffsetEqualsConverter.Instance,
                        ConverterParameter = absoluteOffset
                    },
                    Value = true
                };
                pickTrigger.Setters.Add(new Setter(Control.BackgroundProperty, blueSoftBrush));
                pickTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, blueBrush));
                pickTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
                cellStyle.Triggers.Add(pickTrigger);

                var column = new DisplayBindingTextColumn
                {
                    Header = header,
                    HeaderStyle = sectorHeaderStyle,
                    SectorSlot = absoluteOffset,
                    Width = 52,
                    MinWidth = 52,
                    CanUserResize = false,
                    CanUserSort = false,
                    Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed,
                    Binding = new Binding($"SectorValues[{slot}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    DisplayBinding = new Binding($"SectorDisplayValues[{slot}]"),
                    ElementStyle = elementStyle,
                    EditingElementStyle = editingStyle,
                    CellStyle = cellStyle
                };

                ResourceGrid.Columns.Add(column);
                _sectorColumns.Add(column);
            }
        }

        // En modo Casillas, seleccionar todo / extender selección desde una casilla de sector
        // (Ctrl+Shift+flecha, Ctrl+E, etc.) no debe arrastrar Código/Descripción/Unidad/Cantidad
        // (datos del material, no cantidades a operar) — solo las columnas de sectores visibles.
        DataGridExcelNavigation.SetColumnScope(ResourceGrid, col => !col.IsReadOnly && col != IndexColumn && col.Visibility == Visibility.Visible);

        // Al entrar a Casillas, el foco siempre parte en la primera casilla de la tabla de
        // cubicación — ahorra tener que hacer clic manualmente, igual que en Detalle.
        // BeginInvoke porque las columnas recién se están terminando de armar en este mismo tick.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (Vm.IsSectorMatrixMode && ResourceGrid.Items.Count > 0)
            {
                var firstCol = ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                               ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                if (firstCol != null)
                    DataGridExcelNavigation.FocusCell(ResourceGrid, 0, firstCol);
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    // TotalsBar siempre en CLP (MATERIALES / MANO DE OBRA / …). En Detalle se mantiene visible
    // para cruzar unitarios de la grilla con la suma de la partida; StatusBar es solo Casillas.
    private void SetBottomChromeVisible(bool showTotals, bool showStatus)
    {
        var conv = (System.Windows.Data.IValueConverter)FindResource("BoolToVisibility");
        if (showTotals)
        {
            TotalsBar.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(CubicacionViewModel.HasPartida)) { Converter = conv });
        }
        else
        {
            BindingOperations.ClearBinding(TotalsBar, UIElement.VisibilityProperty);
            TotalsBar.Visibility = Visibility.Collapsed;
        }

        if (showStatus)
        {
            StatusBar.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(CubicacionViewModel.HasPartida)) { Converter = conv });
        }
        else
        {
            BindingOperations.ClearBinding(StatusBar, UIElement.VisibilityProperty);
            StatusBar.Visibility = Visibility.Collapsed;
        }
    }

    private readonly CubicacionSectorFormulaController _sectorFormulaController = new();

    private bool HandleSectorNavigation(KeyEventArgs e)
    {
        if (!Vm.IsSectorMatrixMode || _sectorColumns.Count == 0)
            return false;

        var editingNow = IsGridEditing();

        if (editingNow && e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down or Key.Left or Key.Right
            && _sectorColumns.Contains(ResourceGrid.CurrentCell.Column)
            && (_sectorFormulaController.IsPickingInProgress || (Keyboard.FocusedElement is TextBox activeEditor && CubicacionSectorFormulaController.CanStartSectorPick(activeEditor))))
        {
            if (Keyboard.FocusedElement is TextBox activeTb)
            {
                _sectorFormulaController.HandleSectorPick(e.Key, activeTb, Vm, ResourceGrid, _sectorColumns);
                e.Handled = true;
                return true;
            }
        }

        if (_sectorFormulaController.IsPickingInProgress)
            _sectorFormulaController.FinalizeSectorPick();

        var modifiers = e.KeyboardDevice.Modifiers;
        int currentSlot = _sectorColumns.IndexOf(ResourceGrid.CurrentCell.Column);
        object? activeRow = ResourceGrid.CurrentCell.Item;

        if (modifiers == ModifierKeys.Control && e.Key is Key.Left or Key.Right)
        {
            if (e.Key == Key.Right && ReferenceEquals(ResourceGrid.CurrentCell.Column, CodigoColumn))
            {
                FocusSectorColumn(activeRow, 0);
                e.Handled = true;
                return true;
            }
        }

        if (modifiers == ModifierKeys.Control && e.Key is Key.Left or Key.Right && currentSlot >= 0)
        {
            if (e.Key == Key.Left && currentSlot == 0)
            {
                FocusCodigoColumn(activeRow);
                e.Handled = true;
                return true;
            }

            var currentRow = activeRow as CubicacionRowVm;
            int direction = e.Key == Key.Right ? 1 : -1;
            int targetAbsolute = FindSectorDataEdge(
                currentRow?.Source.QtyExpressions, currentSlot, direction, CubicacionViewModel.MaximumSectorCount);

            ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            FocusSectorColumn(activeRow, targetAbsolute);
            e.Handled = true;
            return true;
        }

        return false;
    }

    internal static bool SectorHasContent(string[]? expressions, int index)
        => CubicacionSectorNavigation.HasContent(expressions, index);

    internal static int FindSectorDataEdge(string[]? expressions, int current, int direction, int max)
        => CubicacionSectorNavigation.FindDataEdge(expressions, current, direction, max);

    private void ClearAllSectorReferenceHighlights() => _sectorFormulaController.ClearAllSectorReferenceHighlights(Vm);

    private void FinalizeSectorPick() => _sectorFormulaController.FinalizeSectorPick();

    // Recibe la fila explícita (capturada en el momento del keypress, ANTES de cualquier
    // CommitEdit) en vez de releer ResourceGrid.CurrentItem dentro del callback diferido — con
    // BeginInvoke ya pasó un tick completo (y un CommitEdit de por medio en varios casos), y
    // CurrentItem (el cursor de la vista de colección) puede desincronizarse de CurrentCell.Item
    // justo al confirmar la primera edición de una fila recién creada, lo que hacía que el foco
    // terminara aplicándose sobre otra fila de la grilla.
    private void FocusCodigoColumn(object? row)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (row == null)
                return;

            int rowIndex = ResourceGrid.Items.IndexOf(row);
            if (rowIndex >= 0)
            {
                DataGridExcelNavigation.FocusCell(ResourceGrid, rowIndex, CodigoColumn);
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FocusSectorColumn(object? row, int slot)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (row == null || slot < 0 || slot >= _sectorColumns.Count)
                return;

            var column = _sectorColumns[slot];
            int rowIndex = ResourceGrid.Items.IndexOf(row);
            if (rowIndex >= 0)
            {
                DataGridExcelNavigation.FocusCell(ResourceGrid, rowIndex, column);
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void HandleFocusCubicacionZone(CubicacionRowVm row)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (row == null) return;
            ResourceGrid.Focus();
            int rowIndex = ResourceGrid.Items.IndexOf(row);
            if (rowIndex < 0) return;

            DataGridColumn? targetCol = null;
            if (Vm.IsSectorMatrixMode && _sectorColumns.Count > 0)
            {
                targetCol = _sectorColumns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                            ?? _sectorColumns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
            }

            if (targetCol == null && CantidadColumn != null && CantidadColumn.Visibility == Visibility.Visible)
            {
                targetCol = CantidadColumn;
            }

            if (targetCol == null)
            {
                targetCol = ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                            ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
            }

            if (targetCol != null)
            {
                DataGridExcelNavigation.FocusCell(ResourceGrid, rowIndex, targetCol);
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void FocusFirstGridCell()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ResourceGrid.Items.Count == 0)
            {
                ResourceGrid.Focus();
                Keyboard.Focus(ResourceGrid);
                return;
            }

            var item = ResourceGrid.Items[0];
            if (item is CubicacionRowVm rowVm && Vm.SelectedRow != rowVm)
            {
                Vm.SelectedRow = rowVm;
            }

            DataGridColumn? col = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible
                ? CodigoColumn
                : null;

            col ??= ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                   ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);

            if (col == null)
            {
                ResourceGrid.Focus();
                Keyboard.Focus(ResourceGrid);
                return;
            }

            _suppressSelectionSync = true;
            try
            {
                ResourceGrid.SelectedCells.Clear();
                var cellInfo = new DataGridCellInfo(item, col);
                ResourceGrid.SelectedCells.Add(cellInfo);
                ResourceGrid.CurrentCell = cellInfo;
            }
            finally
            {
                _suppressSelectionSync = false;
            }

            DataGridExcelNavigation.FocusCell(ResourceGrid, 0, col);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void MoveSelectedPartida(int offset)
    {
        if (IsGridEditing())
        {
            ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        Vm?.NavigatePartidaRelative(offset);
    }

    private readonly CubicacionGridFillController _fillController = new();

    private void RecordMeasurement(string? value) => _fillController.RecordMeasurement(value);

    private int GetSectorSlotFromColumn(DataGridColumn? column)
    {
        if (column == null) return -1;
        return _sectorColumns.IndexOf(column);
    }

    private void HandleFillDown() => _fillController.HandleFillDown(Vm, ResourceGrid, CantidadColumn, GetSectorSlotFromColumn);

    private void HandleFillRight() => _fillController.HandleFillRight(Vm, ResourceGrid, GetSectorSlotFromColumn);

    private void HandleRepeatMeasurement() => _fillController.HandleRepeatMeasurement(Vm, ResourceGrid, CantidadColumn, GetSectorSlotFromColumn);

    public void SelectAndScrollToRow(CubicacionRowVm row)
    {
        if (row == null) return;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ResourceGrid.ScrollIntoView(row);
                var col = CodigoColumn ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                if (col != null && ResourceGrid.SelectionUnit != DataGridSelectionUnit.FullRow)
                {
                    ResourceGrid.SelectedCells.Clear();
                    var cell = new DataGridCellInfo(row, col);
                    ResourceGrid.SelectedCells.Add(cell);
                    ResourceGrid.CurrentCell = cell;
                }
                else { ResourceGrid.SelectedItem = row; }
                ResourceGrid.Focus();
            }
            catch { }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
