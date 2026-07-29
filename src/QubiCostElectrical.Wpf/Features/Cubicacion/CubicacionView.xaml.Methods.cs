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
    private void SetFieldEditingState(TextBox tb, bool isEditing)
    {
        _fieldEditingState[tb] = isEditing;
        if (tb == CantHeaderBox)
            _cantEditingStarted = isEditing;
    }
    private bool IsFieldEditing(TextBox tb)
    {
        return _fieldEditingState.TryGetValue(tb, out var isEditing) && isEditing;
    }

    public CubicacionView(CubicacionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        DataGridExcelNavigation.Attach(ResourceGrid);
        BindItemRowCellWidths();
        ApplyGridMode();
        Loaded += OnLoaded;
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
                or nameof(CubicacionViewModel.SectorColumnWidth))
            {
                ApplyGridMode();
            }

            if (e.PropertyName == nameof(CubicacionViewModel.SelectedRow))
            {
                var rowVm = vm.SelectedRow;
                if (rowVm != null)
                {
                    if (ResourceGrid.CurrentCell.Item != rowVm)
                    {
                        int index = ResourceGrid.Items.IndexOf(rowVm);
                        if (index >= 0)
                        {
                            DataGridColumn? col = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible ? CodigoColumn : null;
                            col ??= ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                                   ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                            if (col != null)
                            {
                                DataGridExcelNavigation.FocusCell(ResourceGrid, index, col);
                            }
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

        // Sin foco inicial dentro de la vista, ningún atajo de teclado llega
        // hasta que el usuario hace clic. Enfocar la grilla al entrar.
        if (!IsKeyboardFocusWithin)
            ResourceGrid.Focus();
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
                _autocompleteEditor?.Focus();
            }
            e.Handled = true;
            return;
        }

        // Con Alt presionado WPF entrega Key.System; la tecla real va en SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
        {
            if (key == Key.Up)
            {
                MoveSelectedPartida(-1);
                e.Handled = true;
                return;
            }
            if (key == Key.Down)
            {
                MoveSelectedPartida(1);
                e.Handled = true;
                return;
            }
            if (key == Key.P && Vm.AplicarConjuntoCommand.CanExecute(null))
            {
                Vm.AplicarConjuntoCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.L && Vm.OpenPrecioListaCommand.CanExecute(null))
            {
                Vm.OpenPrecioListaCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.C && Vm.OpenScTomarCommand.CanExecute(null))
            {
                Vm.OpenScTomarCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.I && Vm.OpenItemizadoCommand.CanExecute(null))
            {
                Vm.OpenItemizadoCommand.Execute(null);
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
                case Key.A:
                    if (Vm.ShowSectorMatrixCommand.CanExecute(null))
                        Vm.ShowSectorMatrixCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D:
                    if (Vm.ShowResourceDetailCommand.CanExecute(null))
                        Vm.ShowResourceDetailCommand.Execute(null);
                    e.Handled = true;
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
            if (tb == CantHeaderBox)
            {
                CancelCantPick();
            }
            SetFieldEditingState(tb, false);
            BindingOperations.GetBindingExpression(tb, TextBox.TextProperty)?.UpdateTarget();
            tb.SelectAll();
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (tb == CantHeaderBox)
            {
                CommitCantHeaderBox();
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
                var fields = new[] { UnidadHeaderBox, CantHeaderBox, ObservacionHeaderBox };
                int idx = Array.IndexOf(fields, tb);
                if (idx >= 0)
                {
                    int next = idx + (e.Key == Key.Right ? 1 : -1);
                    if (next >= 0 && next < fields.Length)
                    {
                        fields[next].Focus();
                    }
                }
                e.Handled = true;
                return true;
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
    private void OnPartidaFieldGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            SetFieldEditingState(tb, false);
            SelectAllDeferred(tb);
        }
    }

    private void OnPartidaFieldPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
            HandleExcelFieldPreviewKeyDown(tb, e);
    }

    // Un SelectAll() llamado directo dentro de GotFocus es poco confiable con foco por CLIC:
    // el mouse-up que sigue al GotFocus reposiciona el cursor en el punto del clic y cancela
    // la selección recién hecha (funciona bien con Tab, que no tiene ese mouse-up). Con
    // BeginInvoke se aplica después de que WPF termina de procesar ese clic.
    private static void SelectAllDeferred(TextBox tb)
    {
        tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnCantHeaderBoxGotFocus(object sender, RoutedEventArgs e)
    {
        SetFieldEditingState(CantHeaderBox, false);
        CancelCantPick();
        SelectAllDeferred(CantHeaderBox);
    }

    private void OnCantHeaderBoxLostFocus(object sender, RoutedEventArgs e)
    {
        CommitCantHeaderBox();
    }


    private void OnCantHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleExcelFieldPreviewKeyDown(CantHeaderBox, e)) return;

        // Sin recursos no hay nada que "pickear" con ↓/↑, pero eso no debe bloquear Enter ni
        // Escape — antes un "return" temprano acá dejaba la Cantidad oficial sin poder
        // confirmarse con Enter en una partida sin recursos todavía.
        bool canPickRows = Vm.Rows.Count > 0;

        // Recién seleccionada (sin editar todavía): ↓/↑ acá NO arma fórmula todavía.
        if (!_cantEditingStarted)
        {
            if (e.Key is Key.Down or Key.Up)
            {
                // Sin editar, las flechas no hacen nada especial (no navegan la grilla ni arman
                // fórmula) — la celda "solo funciona si se entra a editarla".
                e.Handled = true;
                return;
            }
            return;
        }

        // Ya en edición: ↓/↑ arma/mueve la referencia en curso (si hay recursos de dónde tomarla).
        if (e.Key == Key.Down && canPickRows)
        {
            int next = _cantPickIndex < 0 ? 0 : Math.Min(_cantPickIndex + 1, Vm.Rows.Count - 1);
            ApplyCantPick(next);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && canPickRows)
        {
            ApplyCantPick(_cantPickIndex <= 0 ? -1 : _cantPickIndex - 1);
            e.Handled = true;
            return;
        }

        // Cualquier otra tecla (operador, dígito, borrar, Tab...) confirma la referencia que
        // estaba en curso; a partir de acá vuelve a escribirse como texto normal.
        if (_cantPickIndex >= 0)
            FinalizeCantPick();
    }

    private void CommitCantHeaderBox()
    {
        FinalizeCantPick();
        _cantEditingStarted = false;
        var binding = BindingOperations.GetBindingExpression(CantHeaderBox, TextBox.TextProperty);
        binding?.UpdateSource();
        // UpdateSource() por sí solo no alcanza: como el valor viaja "de vuelta" a este mismo
        // TextBox (setter transforma la fórmula en el resultado numérico), WPF no refresca el
        // Text automáticamente para no pisar lo que se está tecleando. Forzar UpdateTarget()
        // para que la celda muestre el resultado (p.ej. "R1+R2" → "3,00") en vez de dejar la
        // fórmula escrita como si no hubiera pasado nada.
        binding?.UpdateTarget();
    }

    // Inserta o reemplaza, en el mismo lugar del texto, el token (R1, R2...) de la fila que se
    // está "pickeando" con ↓/↑, y resalta esa fila con una propiedad propia del row VM
    // (IsFormulaPickTarget) — nunca la selección real de la grilla: eso dispara
    // SelectionChanged → Vm.SelectedRow → foco/paneles de detalle, robándole el foco al cuadro
    // de fórmula (y en algunos casos, una excepción).
    private void ApplyCantPick(int newIndex)
    {
        if (newIndex < 0)
        {
            RemoveCantPickToken();
            ClearCantPickHighlight();
            _cantPickIndex = -1;
            return;
        }

        string token = $"R{newIndex + 1}";

        if (_cantPickIndex < 0)
        {
            // Si al entrar a la celda seguía todo (o parte) seleccionado — ej. recién enfocada,
            // o clic directo en un recurso como primer gesto sin haber tecleado nada — la
            // referencia debe REEMPLAZAR esa selección, no agregarse después. Sin esto, "0,00"
            // seleccionado + clic en un recurso daba "0,00R1" en vez de "R1".
            if (CantHeaderBox.SelectionLength > 0)
            {
                _cantPickTokenStart = CantHeaderBox.SelectionStart;
                _cantPickTokenLength = CantHeaderBox.SelectionLength;
            }
            else
            {
                _cantPickTokenStart = Math.Clamp(CantHeaderBox.CaretIndex, 0, CantHeaderBox.Text.Length);
                _cantPickTokenLength = 0;
            }
        }

        string text = CantHeaderBox.Text;
        int removeLength = Math.Min(_cantPickTokenLength, text.Length - _cantPickTokenStart);
        text = text.Remove(_cantPickTokenStart, Math.Max(removeLength, 0)).Insert(_cantPickTokenStart, token);
        CantHeaderBox.Text = text;
        _cantPickTokenLength = token.Length;
        CantHeaderBox.CaretIndex = _cantPickTokenStart + _cantPickTokenLength;

        ClearCantPickHighlight();
        _cantPickIndex = newIndex;
        Vm.Rows[_cantPickIndex].IsFormulaPickTarget = true;
        if (_cantPickIndex < ResourceGrid.Items.Count)
            ResourceGrid.ScrollIntoView(ResourceGrid.Items[_cantPickIndex]);
    }

    // Deja el token ya escrito tal cual (no lo borra) y cierra la sesión de "pickeo" — la
    // siguiente ↓/↑ empieza una referencia nueva en vez de seguir moviendo esta.
    private void FinalizeCantPick()
    {
        ClearCantPickHighlight();
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
    }

    // Cancela la referencia en curso y borra su texto — se usa cuando ↑ retrocede antes de la
    // primera fila, o al presionar Escape.
    private void CancelCantPick()
    {
        RemoveCantPickToken();
        ClearCantPickHighlight();
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
    }

    private void RemoveCantPickToken()
    {
        if (_cantPickTokenLength <= 0 || _cantPickTokenStart < 0) return;
        string text = CantHeaderBox.Text;
        int removeLength = Math.Min(_cantPickTokenLength, text.Length - _cantPickTokenStart);
        if (removeLength <= 0) return;
        CantHeaderBox.Text = text.Remove(_cantPickTokenStart, removeLength);
        CantHeaderBox.CaretIndex = _cantPickTokenStart;
        _cantPickTokenLength = 0;
    }

    private void ClearCantPickHighlight()
    {
        if (_cantPickIndex >= 0 && _cantPickIndex < Vm.Rows.Count)
            Vm.Rows[_cantPickIndex].IsFormulaPickTarget = false;
    }

    // Clic directo en una celda CANT de un recurso mientras CantHeaderBox tiene el foco →
    // inserta/reemplaza su referencia y la confirma en un solo paso (equivale a ↓/↑ hasta esa
    // fila y luego escribir un operador). Cualquier otro clic sigue el comportamiento normal.
    private void OnResourceGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Alt+clic en una casilla de la matriz de sectores (modo Casillas): abre la Hoja de
        // Cubicación en modo selección y trae el valor elegido directo a esa casilla. Se revisa
        // primero porque no depende de tener el foco en CantHeaderBox (eso es exclusivo de Detalle).
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

        if (!Vm.IsDetailMode || !CantHeaderBox.IsKeyboardFocused) return;
        if (e.OriginalSource is not DependencyObject dep) return;

        var cell = FindParent<DataGridCell>(dep);
        if (cell == null || !ReferenceEquals(cell.Column, CantidadColumn)) return;

        int rowIndex = ResourceGrid.Items.IndexOf(cell.DataContext);
        if (rowIndex < 0) return;

        ApplyCantPick(rowIndex);
        FinalizeCantPick();
        _cantEditingStarted = true;
        CantHeaderBox.Focus();
        e.Handled = true;
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
    private void OnGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (Vm.IsReadOnly)
        {
            e.Cancel = true;
            return;
        }

        if (e.Row.Item is not CubicacionRowVm row) return;

        // Filas esclavo de template: no editar celdas de sector (se calculan solas)
        if (row.IsTemplateSlave && _sectorColumns.Contains(e.Column))
        {
            e.Cancel = true;
            return;
        }
    }

    private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if ((ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn))
            && e.EditingElement is TextBox tb)
        {
            _autocompleteEditor = tb;
            tb.TextChanged += OnAutocompleteEditorTextChanged;
            tb.PreviewKeyDown += OnAutocompleteEditorPreviewKeyDown;
            
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

    private void OnAutocompleteEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        // Escribir en celda = modo teclado: el mouse quieto no debe robar el ítem marcado.
        FreezeAutocompleteHover();
        if (sender is TextBox tb && ResourceGrid.CurrentItem is CubicacionRowVm row)
            _ = Vm.NotifyCodigoChangedAsync(row, tb.Text);
    }

    private void OnAutocompleteEditorPreviewKeyDown(object sender, KeyEventArgs e)
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
                var targetEnter = AutocompleteList.SelectedItem as AutocompleteSuggestionVm
                    ?? Vm.AutocompleteFirstItem;
                if (targetEnter != null)
                {
                    Vm.ApplyMaterialCommand.Execute(targetEnter.Material);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    e.Handled = true;
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
        if (_sectorPickRow >= 0)
            FinalizeSectorPick();
        ClearAllSectorReferenceHighlights();
        _sectorLastPickedRow = -1;
        _sectorLastPickedCol = -1;

        if ((ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn))
            && e.EditingElement is TextBox tb)
        {
            tb.TextChanged -= OnAutocompleteEditorTextChanged;
            tb.PreviewKeyDown -= OnAutocompleteEditorPreviewKeyDown;
            if (ReferenceEquals(_autocompleteEditor, tb))
                _autocompleteEditor = null;
            AutocompletePopup.PlacementTarget = null;
            Vm.AutocompleteVisible = false;
        }

        if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is CubicacionRowVm row)
        {
            // Forzar explícito el valor tecleado en SectorValues[slot] en vez de confiar en que
            // el binding TwoWay (UpdateSourceTrigger=LostFocus) de la columna lo empuje solo —
            // reportado en uso real: escribir una fórmula en una casilla y confirmar (Enter/Tab/
            // flecha) podía dejar la celda en 0 porque el valor tecleado nunca llegaba a
            // SectorValues. Esto garantiza el commit sin depender del timing interno de WPF.
            int slot = _sectorColumns.IndexOf(e.Column);
            if (slot >= 0 && e.EditingElement is TextBox sectorBox)
                row.SectorValues[slot] = sectorBox.Text;

            Vm.OnRowEdited(row);
        }
    }

    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+A → modo Casillas, Ctrl+D → modo Detalle. Se intercepta ANTES que
        // DataGridExcelNavigation (que usa esas mismas teclas para seleccionar todo / rellenar
        // hacia abajo en Itemizado y en el resto de esta grilla) para que en Cubicación ganen
        // el cambio de vista, a pedido explícito — Itemizado conserva el comportamiento Excel.
        if (!IsGridEditing() && e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key is Key.A or Key.D)
        {
            if (e.Key == Key.A && Vm.ShowSectorMatrixCommand.CanExecute(null))
                Vm.ShowSectorMatrixCommand.Execute(null);
            else if (e.Key == Key.D && Vm.ShowResourceDetailCommand.CanExecute(null))
                Vm.ShowResourceDetailCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!IsGridEditing() && e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.X)
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
            if (!handledBySector)
            {
                DataGridExcelNavigation.HandlePreviewKeyDown(ResourceGrid, e);
            }
        }
        finally
        {
            _suppressSelectionSync = false;
        }

        if (handledBySector || e.Handled)
        {
            SyncGridSelectionFromGrid();
            return;
        }

        bool editing = IsGridEditing();
        var modifiers = e.KeyboardDevice.Modifiers;
        // Con Alt presionado WPF entrega Key.System; la tecla real va en SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

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

        // Ctrl+D → duplicar fila seleccionada
        if (e.Key == Key.D && modifiers == ModifierKeys.Control && !editing)
        {
            if (Vm.DuplicateRowCommand.CanExecute(null))
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

        // Alt+Arriba/Abajo → navegar entre partidas (incluso editando)
        // Alt+I → Resumen Itemizado
        // Alt+P → Aplicar conjunto
        // Alt+L → Precio lista
        // Alt+C → Tomar de SC
        if (modifiers == ModifierKeys.Alt)
        {
            if (key == Key.Up)
            {
                if (editing)
                {
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                NavigatePartida(-1);
                e.Handled = true;
                return;
            }
            if (key == Key.Down)
            {
                if (editing)
                {
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                NavigatePartida(1);
                e.Handled = true;
                return;
            }
            if (key == Key.I && Vm.OpenItemizadoCommand.CanExecute(null))
            {
                Vm.OpenItemizadoCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.P && Vm.AplicarConjuntoCommand.CanExecute(null))
            {
                Vm.AplicarConjuntoCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.L && Vm.OpenPrecioListaCommand.CanExecute(null))
            {
                Vm.OpenPrecioListaCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.C && Vm.OpenScTomarCommand.CanExecute(null))
            {
                Vm.OpenScTomarCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Alt+Shift+Arriba/Abajo → reordenar fila
        if (modifiers == (ModifierKeys.Alt | ModifierKeys.Shift) && !editing)
        {
            if (key == Key.Up && Vm.MoveRowUpCommand.CanExecute(null))
            {
                Vm.MoveRowUpCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (key == Key.Down && Vm.MoveRowDownCommand.CanExecute(null))
            {
                Vm.MoveRowDownCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }

    // Público: usado también por ShellWindow como respaldo cuando Alt+↑/↓ se presiona con el
    // foco fuera de esta vista (ej. en un botón del marco de la ventana).
    public void NavigatePartida(int delta)
    {
        MoveSelectedPartida(delta);
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync)
            return;
        SyncGridSelectionFromGrid();
    }

    private void OnGridCurrentCellChanged(object sender, EventArgs e)
    {
        if (_suppressSelectionSync)
            return;
        SyncGridSelectionFromGrid();
    }

    private void SyncGridSelectionFromGrid()
    {
        var selected = ResourceGrid.SelectedCells.Select(c => c.Item).OfType<CubicacionRowVm>().Distinct().ToList();
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
                    col => CubicacionViewModel.ResolveAnchoredPasteColumn(col.Header?.ToString() ?? "") >= 0);
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
                _autocompleteEditor?.Focus();
                e.Handled = true;
                break;
            default:
                _autocompleteEditor?.Focus();
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

        Vm.SelectedPartida = partida;
        // Scroll a la primera fila de la grilla al cambiar partida y enfocar directamente la columna Código
        if (ResourceGrid.Items.Count > 0)
        {
            var firstItem = ResourceGrid.Items[0];
            ResourceGrid.ScrollIntoView(firstItem);
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridColumn? col = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible ? CodigoColumn : null;
                col ??= ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                       ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                if (col != null)
                {
                    DataGridExcelNavigation.FocusCell(ResourceGrid, 0, col);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
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
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T found) return found;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
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
        SetColumnHeader(UnidadColumn, thin, "UD", thinStyle);
        SetColumnHeader(CantidadColumn, thin, "CANT", thinStyle);
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

    private FrameworkElement BuildSectorHeader(string headerText, double columnWidth)
    {
        var parts = SplitHeader(headerText);
        var fgStrong = (System.Windows.Media.Brush)FindResource("Brush.Ink");
        var fgLabel = (System.Windows.Media.Brush)FindResource("Brush.Ink");
        double maxWidth = Math.Max(20, columnWidth - 24);

        if (parts.line2 == null)
        {
            return new TextBlock
            {
                Text = parts.line1,
                ToolTip = headerText,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = fgStrong,
                MaxWidth = maxWidth,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var grid = new Grid
        {
            ToolTip = headerText,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        grid.Children.Add(new TextBlock
        {
            Text = parts.line1,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = fgStrong,
            MaxWidth = maxWidth,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        grid.Children.Add(new TextBlock
        {
            Text = parts.line2,
            FontSize = 8,
            FontWeight = FontWeights.Medium,
            Foreground = fgLabel,
            MaxWidth = maxWidth,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(grid.Children[1], 1);
        return grid;
    }

    private static (string line1, string? line2) SplitHeader(string headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return ("", null);

        int idx = headerText.IndexOfAny(['-', '/', ' ']);
        if (idx > 0 && idx < headerText.Length - 1)
            return (headerText[..idx], headerText[(idx + 1)..]);

        return (headerText, null);
    }

    private void OnSectorWidthKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var tb = (TextBox)sender;
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void ApplyGridMode()
    {
        foreach (var column in _sectorColumns)
            ResourceGrid.Columns.Remove(column);
        _sectorColumns.Clear();

        if (!Vm.IsSectorMatrixMode)
        {
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
            UfUnitColumn.Visibility = Visibility.Visible;
            SubtotalUfColumn.Visibility = Visibility.Visible;
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
                StringFormat = "N2"
            };
            // Encabezados nativos invisibles en Detalle (la fila de partida ya hace de encabezado)
            ResourceGrid.HeadersVisibility = DataGridHeadersVisibility.None;
            SetSharedColumnHeaders(thin: true);
            ResourceGrid.FrozenColumnCount = 5;
            ResourceGrid.ColumnHeaderHeight = double.NaN;
            // Totales CLP visibles también en Detalle: MANO DE OBRA = Σ (MO $ × cantidad).
            // Solo se oculta la barra de atajos de teclado.
            SetBottomChromeVisible(showTotals: true, showStatus: false);

            // Al entrar a Detalle, el foco siempre parte en la Cantidad oficial de la partida
            // — a pedido explícito. Con BeginInvoke porque la fila recién se está haciendo
            // visible (Visibility bindea a IsDetailMode) y todavía no está lista para recibir
            // foco en este mismo tick.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Vm.IsDetailMode)
                    CantHeaderBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
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
        CantidadColumn.Visibility = Visibility.Visible;
        // CANTIDAD vuelve a ser de solo lectura: en Casillas se arma sola desde las celdas de
        // sector (S1/S2/P1...), no se escribe directo acá.
        CantidadColumn.IsReadOnly = true;
        CantidadColumn.Binding = new Binding(nameof(CubicacionRowVm.CantidadDisplay));
        ResourceGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        SetSharedColumnHeaders(thin: false);
        CancelCantPick();
        _cantEditingStarted = false;
        ResourceGrid.FrozenColumnCount = 5;
        ResourceGrid.ColumnHeaderHeight = 44;
        SetBottomChromeVisible(showTotals: true, showStatus: true);

        for (int slot = 0; slot < CubicacionViewModel.SectorWindowSize; slot++)
        {
            // Ya no hay "ventana": las 300 columnas de sector existen todas a la vez y el
            // offset absoluto de un sector coincide siempre con su índice/slot.
            int absoluteOffset = slot;
            string headerText = slot < Vm.VisibleSectorHeaders.Count
                ? Vm.VisibleSectorHeaders[slot]
                : "";

            var header = BuildSectorHeader(headerText, Vm.SectorColumnWidth);

            var elementStyle = new Style(typeof(TextBlock));
            elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            elementStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3, 0, 7, 0)));

            var editingStyle = new Style(typeof(TextBox));
            editingStyle.Setters.Add(new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            editingStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(3, 0, 5, 0)));

            var cellStyle = new Style(typeof(DataGridCell), (Style)FindResource("DataGridCell.Excel"));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, (System.Windows.Media.Brush)FindResource("Brush.Surface2")));

            // Resalta la celda EXACTA (no toda la fila) apuntada al armar una referencia entre
            // casillas con las flechas (ver HandleSectorPick) — sin esto, con solo la fila
            // resaltada, no se distinguía cuál columna iba a quedar seleccionada.
            var pickTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(CubicacionRowVm.FormulaPickTargetOffsets))
                {
                    Converter = SectorPickOffsetEqualsConverter.Instance,
                    ConverterParameter = absoluteOffset
                },
                Value = true
            };
            pickTrigger.Setters.Add(new Setter(Control.BackgroundProperty, (System.Windows.Media.Brush)FindResource("Brush.BlueSoft")));
            pickTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, (System.Windows.Media.Brush)FindResource("Brush.Blue")));
            pickTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            cellStyle.Triggers.Add(pickTrigger);

            var column = new DisplayBindingTextColumn
            {
                Header = header,
                Width = Vm.SectorColumnWidth,
                MinWidth = Vm.SectorColumnWidth,
                CanUserResize = false,
                CanUserSort = false,
                Binding = new Binding($"SectorValues[{slot}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                DisplayBinding = new Binding($"SectorDisplayValues[{slot}]"),
                ElementStyle = elementStyle,
                EditingElementStyle = editingStyle,
                CellStyle = cellStyle
            };

            ResourceGrid.Columns.Add(column);
            _sectorColumns.Add(column);
        }

        // En modo Casillas, seleccionar todo / extender selección desde una casilla de sector
        // (Ctrl+Shift+flecha, Ctrl+E, etc.) no debe arrastrar Código/Descripción/Unidad/Cantidad
        // (datos del material, no cantidades a operar) — solo las columnas de sectores.
        DataGridExcelNavigation.SetColumnScope(ResourceGrid, col => _sectorColumns.Contains(col));

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

    // Estado del "pickeo" de referencias a CUALQUIER celda (fila + columna) desde una casilla
    // de sector — arma tokens "F{fila}C{columna}", resueltos como referencia viva por el motor
    // de fórmulas (ver EvaluateSafeWithRowReferences). -1 = no hay pickeo en curso.
    private int _sectorPickRow = -1;
    private int _sectorPickCol = -1;
    private int _sectorPickTokenStart = -1;
    private int _sectorPickTokenLength;
    // Última celda efectivamente elegida en ESTA edición (persiste entre referencias
    // encadenadas, se limpia solo al terminar de editar la celda) — el siguiente pickeo
    // arranca desde ACÁ, no desde la columna de la celda que se está editando, para que "una
    // flecha a la izquierda" mueva un paso desde donde apuntabas antes, no desde donde escribís.
    private int _sectorLastPickedRow = -1;
    private int _sectorLastPickedCol = -1;
    // Todas las filas actualmente resaltadas porque su celda aparece referenciada en el texto
    // de la fórmula que se está editando — no solo la última "pickeada", para que el usuario
    // vea de un vistazo TODO lo que está sumando, como Excel colorea cada rango referenciado.
    private readonly HashSet<int> _sectorHighlightedRows = new();

    private bool HandleSectorNavigation(KeyEventArgs e)
    {
        if (!Vm.IsSectorMatrixMode || _sectorColumns.Count == 0)
            return false;

        var editingNow = IsGridEditing();

        // Escribiendo en una casilla de sector: CUALQUIER flecha (sin modificador) arma o mueve
        // una referencia viva a la celda apuntada (fila + columna), igual que escribir "=" y
        // apuntar con el mouse a cualquier celda en Excel real — en vez de confirmar y saltar.
        // Se activa si: ya hay un pickeo en curso, el carácter justo antes del cursor es un
        // operador (+, -, *, /, =), O la fórmula ya tiene al menos una referencia armada antes
        // (F{fila}C{columna}) y el cursor está al final — así apretar flecha de nuevo encadena
        // otra suma sola, sin que el usuario tenga que tipear "+" a mano cada vez. Fuera de eso
        // (ej. una celda con un número plano tipeado a mano), la flecha se comporta como
        // siempre: confirma y mueve a la celda vecina.
        if (editingNow && e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down or Key.Left or Key.Right
            && _sectorColumns.Contains(ResourceGrid.CurrentCell.Column)
            && (_sectorPickRow >= 0 || (Keyboard.FocusedElement is TextBox activeEditor && CanStartSectorPick(activeEditor))))
        {
            HandleSectorPick(e.Key);
            e.Handled = true;
            return true;
        }

        // Cualquier otra tecla mientras había una referencia en curso la deja fija (no la sigue
        // moviendo) — la próxima flecha empieza una referencia nueva.
        if (_sectorPickRow >= 0)
            FinalizeSectorPick();

        var modifiers = e.KeyboardDevice.Modifiers;
        int currentSlot = _sectorColumns.IndexOf(ResourceGrid.CurrentCell.Column);
        // Capturada ANTES de cualquier CommitEdit — ver comentario en FocusSectorColumn/
        // FocusCodigoColumn sobre por qué no hay que releer ResourceGrid.CurrentItem después.
        object? activeRow = ResourceGrid.CurrentCell.Item;

        if (modifiers == ModifierKeys.Control && e.Key is Key.Left or Key.Right)
        {
            // Ctrl+Derecha desde la columna Código → ir al primer sector absoluto. Este chequeo
            // no depende de currentSlot (Código nunca es una columna de sector).
            if (e.Key == Key.Right && ReferenceEquals(ResourceGrid.CurrentCell.Column, CodigoColumn))
            {
                FocusSectorColumn(activeRow, 0);
                e.Handled = true;
                return true;
            }
        }

        // El resto de este bloque solo aplica con el foco realmente parado en una celda de
        // sector. Si currentSlot es -1 (foco en Código/Descripción/Unidad/Cantidad), dejar que
        // el manejo genérico de Ctrl+flecha (que nunca cambia de fila) se haga cargo.
        if (modifiers == ModifierKeys.Control && e.Key is Key.Left or Key.Right && currentSlot >= 0)
        {
            // Ctrl+Izquierda en el primer sector absoluto (0-299) → volver a Código.
            if (e.Key == Key.Left && currentSlot == 0)
            {
                FocusCodigoColumn(activeRow);
                e.Handled = true;
                return true;
            }

            // Estilo Excel real: salta al borde del bloque de datos entre los hasta 300
            // sectores de la fila (no una paginación fija que ignora dónde hay contenido). Mira
            // el arreglo completo (row.Source.QtyExpressions).
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

    // Misma regla que usa Excel para Ctrl+flecha: si la celda actual tiene contenido y la
    // siguiente también, avanza hasta el final del bloque contiguo; si la siguiente está vacía
    // (o la actual lo está), salta directo a la próxima celda con contenido; si no hay ninguna
    // más en esa dirección, va al extremo del rango (0 o max-1).
    internal static bool SectorHasContent(string[]? expressions, int index)
        => expressions != null && index >= 0 && index < expressions.Length && !string.IsNullOrWhiteSpace(expressions[index]);

    internal static int FindSectorDataEdge(string[]? expressions, int current, int direction, int max)
    {
        int next = current + direction;
        if (next < 0) return 0;
        if (next >= max) return max - 1;

        if (SectorHasContent(expressions, current) && SectorHasContent(expressions, next))
        {
            int i = next;
            while (i + direction >= 0 && i + direction < max && SectorHasContent(expressions, i + direction))
                i += direction;
            return i;
        }

        int j = next;
        while (j >= 0 && j < max && !SectorHasContent(expressions, j))
            j += direction;
        return j < 0 ? 0 : j >= max ? max - 1 : j;
    }

    // El carácter justo antes del cursor es un operador que espera un operando a continuación
    // — igual que Excel: recién ahí las flechas empiezan a "apuntar" a otra celda.
    private static bool EndsWithFormulaOperator(TextBox textBox)
    {
        int idx = textBox.CaretIndex;
        if (idx <= 0 || idx > textBox.Text.Length) return false;
        return textBox.Text[idx - 1] is '+' or '-' or '*' or '/' or '=' or '(';
    }

    private static readonly System.Text.RegularExpressions.Regex SectorCellReferenceRegex =
        new(@"[Ff](\d+)[Cc](\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Con el cursor al final y la fórmula ya conteniendo al menos una referencia armada antes
    // (F{fila}C{columna}), se puede encadenar otra suma con solo apretar flecha de nuevo — no
    // hace falta tipear "+" a mano cada vez (ver HandleSectorPick, que inserta el operador
    // solo). Evita el bug reportado en uso real: "quiero sumar varias y no puedo".
    private static bool CanStartSectorPick(TextBox textBox)
        => EndsWithFormulaOperator(textBox)
           || (textBox.CaretIndex == textBox.Text.Length && SectorCellReferenceRegex.IsMatch(textBox.Text));

    // Inserta o mueve, en el mismo lugar del texto, el token F{fila}C{columna} de la celda que
    // se está "pickeando" con las flechas dentro de una casilla de sector — el token queda
    // resuelto por el motor de fórmulas como una referencia VIVA a esa celda exacta (ver
    // EvaluateSafeWithRowReferences), no un texto fijo. La columna se mueve solo dentro de la
    // ventana de 15 sectores VISIBLE en pantalla — no se desplaza la ventana durante el pickeo
    // para no reconstruir las columnas de la grilla en medio de una edición en curso.
    private void HandleSectorPick(Key key)
    {
        if (Keyboard.FocusedElement is not TextBox textBox) return;
        if (Vm.Rows.Count == 0) return;

        if (_sectorPickRow < 0)
        {
            // Encadenando: ya hay una referencia armada antes y no terminaba en operador —
            // se entiende que el usuario quiere sumar OTRO término, así que se inserta el "+"
            // automáticamente antes de empezar el nuevo pickeo.
            if (!EndsWithFormulaOperator(textBox) && textBox.CaretIndex == textBox.Text.Length)
            {
                int insertAt = textBox.Text.Length;
                textBox.Text = textBox.Text.Insert(insertAt, "+");
                textBox.CaretIndex = insertAt + 1;
            }

            // Arranca desde la ÚLTIMA celda efectivamente elegida en esta edición, si hubo una
            // (encadenando una segunda referencia, el primer paso mueve desde ahí, no desde la
            // columna de la celda que se está editando — si no, hacía falta un paso "extra" de
            // más para llegar adonde el usuario apuntaba). Solo la PRIMERA referencia de toda la
            // edición arranca desde la celda actual, como Excel.
            if (_sectorLastPickedRow >= 0)
            {
                _sectorPickRow = _sectorLastPickedRow;
                _sectorPickCol = _sectorLastPickedCol;
            }
            else
            {
                var currentRow = ResourceGrid.CurrentCell.Item as CubicacionRowVm;
                _sectorPickRow = currentRow != null ? Vm.Rows.IndexOf(currentRow) : 0;
                if (_sectorPickRow < 0) _sectorPickRow = 0;
                int currentSlot = _sectorColumns.IndexOf(ResourceGrid.CurrentCell.Column);
                _sectorPickCol = currentSlot >= 0 ? currentSlot : 0;
            }
        }

        int rangeEnd = _sectorColumns.Count - 1;

        switch (key)
        {
            case Key.Up: _sectorPickRow = Math.Max(0, _sectorPickRow - 1); break;
            case Key.Down: _sectorPickRow = Math.Min(Vm.Rows.Count - 1, _sectorPickRow + 1); break;
            case Key.Left: _sectorPickCol = Math.Max(0, _sectorPickCol - 1); break;
            case Key.Right: _sectorPickCol = Math.Min(rangeEnd, _sectorPickCol + 1); break;
        }

        ApplySectorPick(textBox, _sectorPickRow, _sectorPickCol);
    }

    private void ApplySectorPick(TextBox textBox, int rowIndex, int colOffset)
    {
        _sectorLastPickedRow = rowIndex;
        _sectorLastPickedCol = colOffset;

        string token = $"F{rowIndex + 1}C{colOffset + 1}";
        bool firstPick = _sectorPickTokenStart < 0;

        if (firstPick)
        {
            if (textBox.SelectionLength > 0)
            {
                _sectorPickTokenStart = textBox.SelectionStart;
                _sectorPickTokenLength = textBox.SelectionLength;
            }
            else
            {
                _sectorPickTokenStart = Math.Clamp(textBox.CaretIndex, 0, textBox.Text.Length);
                _sectorPickTokenLength = 0;
            }
        }

        string text = textBox.Text;
        int removeLength = Math.Min(_sectorPickTokenLength, Math.Max(0, text.Length - _sectorPickTokenStart));
        text = text.Remove(_sectorPickTokenStart, removeLength).Insert(_sectorPickTokenStart, token);
        textBox.Text = text;
        _sectorPickTokenLength = token.Length;
        textBox.CaretIndex = _sectorPickTokenStart + _sectorPickTokenLength;

        // Resalta TODAS las celdas referenciadas en el texto actual (no solo esta), para que se
        // vea de un vistazo todo lo que se está sumando — a pedido explícito, como Excel colorea
        // cada rango de una fórmula mientras se está armando.
        RefreshSectorReferenceHighlights(textBox);

        // Deliberadamente SIN ScrollIntoView acá: en una grilla virtualizada, desplazar la vista
        // hacia la fila apuntada puede reciclar el contenedor de la fila que se está EDITANDO
        // (si queda fuera del viewport), cortando la edición en curso a mitad de armar la
        // fórmula — justo el bug reportado de "no puedo sumar varias". Si la fila apuntada no es
        // visible, el usuario igual ve el resultado al confirmar; no vale el riesgo de romper la
        // edición por mostrarla de entrada. Se refuerza el foco en el editor por si algo lo movió.
        textBox.Focus();
    }

    // Reescanea el texto completo de la fórmula y resalta CADA celda referenciada — reemplaza
    // por completo el conjunto anterior de resaltados (algunas pueden haberse borrado o
    // cambiado de posición si el usuario editó el texto a mano). Agrupa por fila para poder
    // marcar VARIAS columnas de la MISMA fila a la vez (ej. suma horizontal: F4C1+F4C2+F4C3),
    // no solo una — antes solo quedaba resaltada la última columna de esa fila.
    private void RefreshSectorReferenceHighlights(TextBox textBox)
    {
        var referencedByRow = new Dictionary<int, HashSet<int>>();
        foreach (System.Text.RegularExpressions.Match m in SectorCellReferenceRegex.Matches(textBox.Text))
        {
            if (!int.TryParse(m.Groups[1].Value, out int rowNumber)) continue;
            if (!int.TryParse(m.Groups[2].Value, out int colNumber)) continue;
            int rowIndex = rowNumber - 1;
            if (rowIndex < 0 || rowIndex >= Vm.Rows.Count) continue;
            if (!referencedByRow.TryGetValue(rowIndex, out var offsets))
                referencedByRow[rowIndex] = offsets = new HashSet<int>();
            offsets.Add(colNumber - 1);
        }

        foreach (var rowIndex in _sectorHighlightedRows)
        {
            if (referencedByRow.ContainsKey(rowIndex)) continue;
            if (rowIndex >= 0 && rowIndex < Vm.Rows.Count)
            {
                Vm.Rows[rowIndex].IsFormulaPickTarget = false;
                Vm.Rows[rowIndex].ClearFormulaPickTargetOffsets();
            }
        }

        foreach (var (rowIndex, offsets) in referencedByRow)
        {
            var row = Vm.Rows[rowIndex];
            row.IsFormulaPickTarget = true;
            row.ClearFormulaPickTargetOffsets();
            foreach (var offset in offsets)
                row.AddFormulaPickTargetOffset(offset);
        }

        _sectorHighlightedRows.Clear();
        foreach (var rowIndex in referencedByRow.Keys)
            _sectorHighlightedRows.Add(rowIndex);
    }

    private void ClearAllSectorReferenceHighlights()
    {
        foreach (var rowIndex in _sectorHighlightedRows)
        {
            if (rowIndex >= 0 && rowIndex < Vm.Rows.Count)
            {
                Vm.Rows[rowIndex].IsFormulaPickTarget = false;
                Vm.Rows[rowIndex].ClearFormulaPickTargetOffsets();
            }
        }
        _sectorHighlightedRows.Clear();
    }

    // Cierra la sesión de "pickeo" en curso (deja el token ya escrito tal cual) — la próxima
    // flecha empieza una referencia nueva en vez de seguir moviendo esta. NO borra los
    // resaltados: mientras la celda siga en edición, todas las referencias de la fórmula
    // quedan marcadas (ver ClearAllSectorReferenceHighlights, llamado al terminar la edición).
    private void FinalizeSectorPick()
    {
        _sectorPickRow = -1;
        _sectorPickCol = -1;
        _sectorPickTokenStart = -1;
        _sectorPickTokenLength = 0;
    }

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

    private void MoveSelectedPartida(int offset)
    {
        if (Vm == null || Vm.FilteredPartidas == null || Vm.FilteredPartidas.Count <= 1)
            return;

        // Confirmar edición activa en el DataGrid antes de cambiar de partida
        if (IsGridEditing())
        {
            ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ResourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        int currentIndex = Vm.SelectedPartida != null ? Vm.FilteredPartidas.IndexOf(Vm.SelectedPartida) : -1;
        int targetIndex = currentIndex + offset;

        while (targetIndex >= 0 && targetIndex < Vm.FilteredPartidas.Count)
        {
            var candidate = Vm.FilteredPartidas[targetIndex];
            if (candidate.EsPartida)
            {
                Vm.SelectedPartida = candidate;
                PartidasList.ScrollIntoView(candidate);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ResourceGrid.Items.Count > 0)
                    {
                        var firstItem = ResourceGrid.Items[0];
                        var firstCol = ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                                       ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
                        if (firstCol != null)
                        {
                            DataGridExcelNavigation.FocusCell(ResourceGrid, 0, firstCol);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                break;
            }
            targetIndex += offset;
        }
    }
}



// Columna de sector: se edita como una DataGridTextColumn normal (conserva "escribir para
// empezar a editar", commit con Enter/Tab/flechas, etc. — todo el comportamiento de
// DataGridExcelNavigation), pero cuando NO se está editando muestra el RESULTADO calculado de
// la fórmula (SectorDisplayValues) en vez del texto crudo (SectorValues) — igual que una celda
// de Excel: escribís "2+3" y ves "5", pero al volver a entrar a editar ves la fórmula original.