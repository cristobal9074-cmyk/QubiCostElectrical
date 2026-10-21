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
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Converters;
using QubiCostElectrical.Wpf.Infrastructure;

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
        vm.CommitOpenCellHandler = CommitOpenCell;
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
                if (Vm.SuppressPartidaFocus)
                    return;
                ApplyGridMode();
                if (Vm.SelectedPartida != null)
                    PartidasList.ScrollIntoView(Vm.SelectedPartida);
                FocusFirstGridCell();
            }

            if (e.PropertyName == nameof(CubicacionViewModel.SelectedRow))
            {
                if (_isSyncingFromGrid)
                    return;

                if (DataGridExcelNavigation.IsExtendingSelection(ResourceGrid))
                    return;

                var rowVm = vm.SelectedRow;
                if (rowVm != null)
                {
                    if (ResourceGrid.CurrentCell.Item == rowVm && ResourceGrid.SelectedCells.Count > 0)
                        return;

                    DataGridColumn? col = CodigoColumn != null && CodigoColumn.Visibility == Visibility.Visible ? CodigoColumn : null;
                    col ??= ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                           ?? ResourceGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);

                    int index = ResourceGrid.Items.IndexOf(rowVm);
                    if (index >= 0 && col != null)
                    {
                        DataGridExcelNavigation.FocusCell(ResourceGrid, index, col);
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

            var baseTransparentEditStyle = (Style)FindResource("DataGrid.CellEditTransparent");
            var editingStyle = new Style(typeof(TextBox), baseTransparentEditStyle);
            editingStyle.Setters.Add(new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            editingStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(2, 0, 4, 0)));
            editingStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            editingStyle.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent));

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
