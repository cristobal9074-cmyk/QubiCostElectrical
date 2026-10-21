using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView : UserControl
{
    private void OnPartidasPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm == null)
            return;

        DispatchCell(new CubicacionCellInput
        {
            Source = CubicacionInputSource.PartidasPanel,
            Key = CubicacionCellKeyMap.From(e),
            Modifiers = CubicacionCellKeyMap.ModifiersOf(e),
            PartidaFilterActive = !string.IsNullOrEmpty(Vm.PartidaFilter),
            FocusedIsTextBox = Keyboard.FocusedElement is TextBox,
            SelectedIsPartida = Vm.SelectedPartida is { EsPartida: true }
        }, e);
    }

    private void OnPartidaHeaderBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb)
            return;
        var source = ReferenceEquals(tb, PartidaCodigoBox)
            ? CubicacionInputSource.PartidaCodigo
            : CubicacionInputSource.PartidaDescripcion;
        DispatchCell(ReadField(source, e, tb), e, tb);
    }

    // ── Teclado global ────────────────────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e) => DispatchCell(ReadShellKey(e), e);

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
        if (sender is not TextBox tb)
            return;
        DispatchCell(new CubicacionCellInput
        {
            Source = CubicacionInputSource.CodigoEditor,
            Key = CubicacionCellKeyMap.From(e),
            Modifiers = CubicacionCellKeyMap.ModifiersOf(e),
            AutocompleteOpen = Vm.AutocompleteVisible,
            AutocompleteHasItem = (AutocompleteList.SelectedItem ?? Vm.AutocompleteFirstItem) != null,
            AutocompleteHasExplicitSelection = AutocompleteList.SelectedItem != null,
            FirstIsExactMatch = Vm.AutocompleteFirstItemIsExactMatch
        }, e, tb);
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
                if (!TryAcceptGridFormula(row, slot, sectorBox.Text, isSector: true))
                {
                    e.Cancel = true;
                    return;
                }

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
                if (!TryAcceptGridFormula(row, slot: -1, cantBox.Text, isSector: false))
                {
                    e.Cancel = true;
                    return;
                }

                if (CubicacionPartidaHelper.TryParsePartidaCantidad((cantBox.Text ?? "").Trim(), Vm.BuildPartidaCantidadVariables(), out decimal parsed, out _))
                {
                    cantBox.Text = parsed % 1 == 0
                        ? parsed.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)
                        : parsed.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture);
                    row.Cantidad = parsed;
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

    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e) => DispatchCell(ReadGridKey(e), e);

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
            bool extending = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                || DataGridExcelNavigation.IsExtendingSelection(ResourceGrid);
            if (!extending)
            {
                DataGridExcelNavigation.ResetAnchor(ResourceGrid);
                if (ResourceGrid.SelectedCells.Count != 1 || !ResourceGrid.SelectedCells.Contains(ResourceGrid.CurrentCell))
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
        }

        SyncGridSelectionFromGrid();
    }

    private void SyncGridSelectionFromGrid()
    {
        _isSyncingFromGrid = true;
        try
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
        finally
        {
            _isSyncingFromGrid = false;
        }
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
}
