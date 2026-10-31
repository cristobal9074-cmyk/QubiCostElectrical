using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public partial class ChangeOrderEditorDialog : Window
{
    private readonly ChangeOrderRow _draft;
    private readonly ChangeOrderEditorViewModel _vm;
    private readonly List<CubicacionRow> _baseProjectCubicacion;
    private readonly IPriceBankStore? _priceBankStore;
    private readonly List<DataGridColumn> _sectorColumns = new();
    private TextBox? _currentCodigoEditor;

    public ChangeOrderEditorDialog(
        ChangeOrderRow draft,
        bool muestraUf,
        decimal ufRate,
        IEnumerable<ChangeOrderPartidaPickerItem>? availableContractPartidas = null,
        IEnumerable<CubicacionRow>? baseProjectCubicacion = null,
        IPriceBankStore? priceBankStore = null,
        BudgetProjectSession? session = null)
    {
        _draft = draft;
        _baseProjectCubicacion = baseProjectCubicacion != null ? baseProjectCubicacion.ToList() : new List<CubicacionRow>();
        _priceBankStore = priceBankStore;

        _vm = new ChangeOrderEditorViewModel();
        _vm.ConfigureCurrency(muestraUf, ufRate);
        if (session != null)
        {
            _vm.ConfigureMargins(session.GastosGeneralesPct, session.UtilidadPct);
        }
        if (availableContractPartidas != null)
        {
            _vm.AvailableContractPartidas = availableContractPartidas.ToList();
        }
        _vm.LoadFrom(draft);

        InitializeComponent();
        DataContext = _vm;
        Owner = Application.Current?.MainWindow;
        _formulaController.OnPickHighlightChanged = UpdateFormulaPickHighlight;

        SourceInitialized += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            if (Height > workArea.Height - 30)
                Height = Math.Max(MinHeight, workArea.Height - 30);
            if (Width > workArea.Width - 30)
                Width = Math.Max(MinWidth, workArea.Width - 30);

            Left = Math.Max(workArea.Left, workArea.Left + (workArea.Width - Width) / 2);
            Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - Height) / 2);
        };

        BuildZoneColumns(session);
    }

    private void BuildZoneColumns(BudgetProjectSession? session)
    {
        var activeZones = session != null ? CubicacionSectorService.ZonasActivasOrdenadas(session) : null;
        var definedZoneNames = activeZones != null
            ? activeZones.Where(z => !string.IsNullOrWhiteSpace(z?.Nombre)).Select(z => z.Nombre.Trim()).ToList()
            : new List<string>();

        const int TotalCasillas = 16;
        var zoneNames = new List<string>(TotalCasillas);
        for (int i = 0; i < TotalCasillas; i++)
        {
            // Solo si el presupuesto TIENE zonas creadas en ZonasProyecto, usamos ese nombre.
            // Si no se ha creado ninguna zona en el presupuesto, el encabezado queda completamente limpio ("")
            // idéntico a la vista de Cubicación de QubiCost.
            string name = (i < definedZoneNames.Count) ? definedZoneNames[i] : "";
            zoneNames.Add(name);
        }

        var baseCellStyle = (Style)FindResource("DataGridCell.Excel");
        var zoneHeaderStyle = new Style(typeof(DataGridColumnHeader), (Style)FindResource("DataGridColumnHeader.Prototype"));
        zoneHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(2, 0, 2, 0)));
        zoneHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        zoneHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));

        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        elementStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, FindResource("Font.Mono")));
        elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        elementStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 5, 0)));

        var editingStyle = new Style(typeof(TextBox));
        editingStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Right));
        editingStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, FindResource("Font.Mono")));
        editingStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        editingStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(2, 0, 4, 0)));
        editingStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.White));
        editingStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))));
        editingStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(2)));

        // Insertar las columnas de zonas justo antes de las columnas de precio (MAT $, MO $, etc.)
        int insertIndex = ActiveMaterialesGrid.Columns.IndexOf(MatPriceColumn);
        if (insertIndex < 0) insertIndex = 5;

        for (int i = 0; i < zoneNames.Count; i++)
        {
            int slot = i;
            string zName = zoneNames[i];

            var headerPanel = new StackPanel
            {
                ToolTip = string.IsNullOrEmpty(zName) ? null : zName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (!string.IsNullOrEmpty(zName))
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = zName,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var cellStyle = new Style(typeof(DataGridCell), baseCellStyle);
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"))));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

            var pickTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(ChangeOrderMaterialVm.FormulaPickSlot)),
                Value = slot
            };
            pickTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"))));
            pickTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(2)));
            pickTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"))));
            cellStyle.Triggers.Add(pickTrigger);

            var col = new DisplayBindingTextColumn
            {
                Header = headerPanel,
                Width = 60,
                MinWidth = 52,
                CanUserSort = false,
                SectorSlot = slot,
                Binding = new Binding($"SectorValues[{slot}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                DisplayBinding = new Binding($"SectorDisplayValues[{slot}]"),
                CellStyle = cellStyle,
                HeaderStyle = zoneHeaderStyle,
                ElementStyle = elementStyle,
                EditingElementStyle = editingStyle
            };

            ActiveMaterialesGrid.Columns.Insert(insertIndex + i, col);
            _sectorColumns.Add(col);
        }

        SetViewMode(true);
    }

    private bool _isCasillasMode = true;

    private void OnCasillasViewClick(object sender, RoutedEventArgs e)
    {
        SetViewMode(true);
    }

    private void OnDetalleViewClick(object sender, RoutedEventArgs e)
    {
        SetViewMode(false);
    }

    private void SetViewMode(bool casillas)
    {
        _isCasillasMode = casillas;
        if (CasillasBtn != null)
        {
            CasillasBtn.Background = casillas ? Brushes.White : Brushes.Transparent;
            CasillasBtn.Foreground = casillas ? (Brush)FindResource("Brush.Ink") : (Brush)FindResource("Brush.Muted");
        }
        if (DetalleBtn != null)
        {
            DetalleBtn.Background = !casillas ? Brushes.White : Brushes.Transparent;
            DetalleBtn.Foreground = !casillas ? (Brush)FindResource("Brush.Ink") : (Brush)FindResource("Brush.Muted");
        }

        foreach (var col in _sectorColumns)
        {
            col.Visibility = casillas ? Visibility.Visible : Visibility.Collapsed;
        }

        if (MatPriceColumn != null) MatPriceColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
        if (LaborPriceColumn != null) LaborPriceColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
        if (TotalPriceColumn != null) TotalPriceColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
        if (SubtotalMatColumn != null) SubtotalMatColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
        if (GgUtColumn != null) GgUtColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
        if (ObservacionesColumn != null) ObservacionesColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Configure(bool isNew)
    {
        Title = isNew ? "Nuevo Adicional de Obra & Cubicación" : "Espacio de Trabajo — Adicional de Obra & Cubicación";
        TitleBlock.Text = isNew ? "NOMBRE DEL ADICIONAL (NUEVO):" : "NOMBRE / TÍTULO DEL ADICIONAL:";
        Loaded += (_, _) =>
        {
            if (_vm.SelectedLine == null && _vm.Lineas.Count > 0)
            {
                _vm.SelectedLine = _vm.Lineas.FirstOrDefault(l => !l.EsTitulo) ?? _vm.Lineas[0];
            }
            TituloBox.Focus();
        };
    }

    private void OnAddContractPartidaClick(object sender, RoutedEventArgs e)
    {
        if (_vm.AvailableContractPartidas.Count == 0)
        {
            WpfDialogService.Warning("Sin partidas de contrato", "No se encontraron partidas contractuales en el presupuesto actual o todas son títulos.");
            return;
        }

        var selected = ChangeOrderPartidaPickerDialog.SelectPartida(this, _vm.AvailableContractPartidas);
        if (selected != null)
        {
            _vm.AddContractualPartida(selected, _baseProjectCubicacion);
        }
    }

    private void OnAddTituloClick(object sender, RoutedEventArgs e)
    {
        _vm.AddTitulo();
    }

    private void OnAddExtraPartidaClick(object sender, RoutedEventArgs e)
    {
        _vm.AddExtraordinariaPartida();
    }

    private void OnRemoveLineClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChangeOrderLineVm line)
        {
            _vm.RemoveLine(line);
        }
    }

    private void OnAddMaterialClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelectedLine || _vm.SelectedLine?.EsTitulo == true)
        {
            WpfDialogService.Warning("Seleccione una partida", "Primero seleccione una partida contractual o extraordinaria en la tabla superior para agregarle materiales a cubicar.");
            return;
        }
        _vm.AddMaterialToActiveLine();
    }

    private void OnRemoveMaterialClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChangeOrderMaterialVm mat)
        {
            _vm.RemoveMaterialFromActiveLine(mat);
        }
    }

    private void OnPrecargarBaseClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelectedLine || _vm.SelectedLine?.EsTitulo == true)
        {
            WpfDialogService.Warning("Seleccione una partida", "Primero seleccione una partida contractual en la tabla superior.");
            return;
        }

        bool success = _vm.PreloadBaseMaterialsForActiveLine(_baseProjectCubicacion);
        if (!success)
        {
            WpfDialogService.Warning("Sin materiales base", "No se encontraron materiales para esta partida en el presupuesto original del contrato.");
        }
        else
        {
            WpfDialogService.Info("Materiales precargados", "Se importaron y escalaron los materiales originales a la medición de esta partida.");
        }
    }

    private void OnBuscarEnBancoClick(object sender, RoutedEventArgs e)
    {
        if (_priceBankStore == null)
        {
            WpfDialogService.Warning("Banco de precios", "El banco de precios no está disponible.");
            return;
        }
        WpfDialogService.Info("Banco de precios", "Escriba el código del material en la columna 'CÓDIGO'. Aparecerán sugerencias automáticas y al seleccionar se cargarán precios y descripción.");
    }

    private record SectorEditContext(ChangeOrderMaterialVm Material, int Slot);

    private void OnNumeroBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab)
        {
            TituloBox.Focus();
            TituloBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Right && sender is TextBox tb && (tb.SelectionStart == tb.Text.Length || tb.SelectionLength == tb.Text.Length))
        {
            TituloBox.Focus();
            TituloBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            FocusLineasGridCell(0, "Col_Item");
            e.Handled = true;
        }
    }

    private void OnTituloBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab or Key.Down)
        {
            FocusLineasGridCell(0, "Col_Item");
            e.Handled = true;
        }
        else if (e.Key == Key.Left && sender is TextBox tb && (tb.SelectionStart == 0 || tb.SelectionLength == tb.Text.Length))
        {
            NumeroBox.Focus();
            NumeroBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnLineasGridCellGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChangeOrderLineVm line)
        {
            if (_vm.SelectedLine != line)
            {
                _vm.SelectedLine = line;
            }
        }
    }

    private void OnLineasGridCantLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            ApplyPartidaCantEdit(tb);
        }
    }

    private void OnLineasGridCellPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string tag = tb.Tag as string ?? "Col_Item";
        var currentLine = tb.DataContext as ChangeOrderLineVm ?? _vm.SelectedLine;
        if (currentLine == null) return;

        int rowIndex = _vm.Lineas.IndexOf(currentLine);
        if (rowIndex < 0) return;

        if (tag == "Col_Cant" && (e.Key is Key.Enter or Key.Down or Key.Up or Key.Tab))
        {
            ApplyPartidaCantEdit(tb, currentLine);
        }

        // Flecha Abajo o Enter -> Mover a la fila siguiente
        if (e.Key is Key.Down or Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (rowIndex < _vm.Lineas.Count - 1)
            {
                int nextRow = rowIndex + 1;
                if (tag == "Col_Cant" && _vm.Lineas[nextRow].EsTitulo)
                {
                    int nextNonTitle = -1;
                    for (int r = nextRow; r < _vm.Lineas.Count; r++)
                    {
                        if (!_vm.Lineas[r].EsTitulo) { nextNonTitle = r; break; }
                    }
                    if (nextNonTitle >= 0)
                        FocusLineasGridCell(nextNonTitle, "Col_Cant");
                    else
                        FocusLineasGridCell(nextRow, "Col_Desc");
                }
                else
                {
                    FocusLineasGridCell(nextRow, tag);
                }
            }
            else
            {
                if (PartidaCantBox != null && PartidaCantBox.IsVisible && _vm.SelectedLine?.EsTitulo == false)
                {
                    PartidaCantBox.Focus();
                    PartidaCantBox.SelectAll();
                }
                else
                {
                    FocusActiveMaterialesGridFirstCell();
                }
            }
            e.Handled = true;
            return;
        }

        // Flecha Arriba -> Mover a la fila anterior
        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (rowIndex > 0)
            {
                int prevRow = rowIndex - 1;
                if (tag == "Col_Cant" && _vm.Lineas[prevRow].EsTitulo)
                {
                    int prevNonTitle = -1;
                    for (int r = prevRow; r >= 0; r--)
                    {
                        if (!_vm.Lineas[r].EsTitulo) { prevNonTitle = r; break; }
                    }
                    if (prevNonTitle >= 0)
                        FocusLineasGridCell(prevNonTitle, "Col_Cant");
                    else
                        FocusLineasGridCell(prevRow, "Col_Desc");
                }
                else
                {
                    FocusLineasGridCell(prevRow, tag);
                }
            }
            else
            {
                if (tag == "Col_Item")
                {
                    NumeroBox.Focus();
                    NumeroBox.SelectAll();
                }
                else
                {
                    TituloBox.Focus();
                    TituloBox.SelectAll();
                }
            }
            e.Handled = true;
            return;
        }

        // Flecha Derecha o Tab
        bool isAtEnd = tb.SelectionLength == tb.Text.Length || tb.SelectionStart == tb.Text.Length;
        if ((e.Key == Key.Right && isAtEnd && Keyboard.Modifiers == ModifierKeys.None) ||
            (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == 0))
        {
            if (tag == "Col_Item")
            {
                FocusLineasGridCell(rowIndex, "Col_Desc");
                e.Handled = true;
            }
            else if (tag == "Col_Desc")
            {
                if (!currentLine.EsTitulo)
                {
                    FocusLineasGridCell(rowIndex, "Col_Cant");
                    e.Handled = true;
                }
                else if (rowIndex < _vm.Lineas.Count - 1)
                {
                    FocusLineasGridCell(rowIndex + 1, "Col_Item");
                    e.Handled = true;
                }
            }
            else if (tag == "Col_Cant")
            {
                if (rowIndex < _vm.Lineas.Count - 1)
                {
                    FocusLineasGridCell(rowIndex + 1, "Col_Item");
                    e.Handled = true;
                }
                else if (PartidaCantBox != null && PartidaCantBox.IsVisible && _vm.SelectedLine?.EsTitulo == false)
                {
                    PartidaCantBox.Focus();
                    PartidaCantBox.SelectAll();
                    e.Handled = true;
                }
                else
                {
                    FocusActiveMaterialesGridFirstCell();
                    e.Handled = true;
                }
            }
            return;
        }

        // Flecha Izquierda o Shift+Tab
        bool isAtStart = tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == 0 && tb.SelectionLength == 0);
        if ((e.Key == Key.Left && isAtStart && Keyboard.Modifiers == ModifierKeys.None) ||
            (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
        {
            if (tag == "Col_Cant")
            {
                FocusLineasGridCell(rowIndex, "Col_Desc");
                e.Handled = true;
            }
            else if (tag == "Col_Desc")
            {
                FocusLineasGridCell(rowIndex, "Col_Item");
                e.Handled = true;
            }
            else if (tag == "Col_Item")
            {
                if (rowIndex > 0)
                {
                    int prevRow = rowIndex - 1;
                    if (!_vm.Lineas[prevRow].EsTitulo)
                        FocusLineasGridCell(prevRow, "Col_Cant");
                    else
                        FocusLineasGridCell(prevRow, "Col_Desc");
                    e.Handled = true;
                }
                else
                {
                    NumeroBox.Focus();
                    NumeroBox.SelectAll();
                    e.Handled = true;
                }
            }
            return;
        }
    }

    private void FocusLineasGridCell(int rowIndex, string columnTag)
    {
        if (rowIndex < 0 || rowIndex >= _vm.Lineas.Count) return;

        var line = _vm.Lineas[rowIndex];
        _vm.SelectedLine = line;
        LineasGrid.SelectedItem = line;
        LineasGrid.ScrollIntoView(line);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var rowContainer = LineasGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            if (rowContainer == null)
            {
                LineasGrid.UpdateLayout();
                rowContainer = LineasGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            }
            if (rowContainer != null)
            {
                var targetTb = FindVisualChildByTag<TextBox>(rowContainer, columnTag);
                if (targetTb != null && targetTb.IsVisible)
                {
                    targetTb.Focus();
                    targetTb.SelectAll();
                }
                else if (columnTag == "Col_Cant")
                {
                    var descTb = FindVisualChildByTag<TextBox>(rowContainer, "Col_Desc");
                    if (descTb != null && descTb.IsVisible)
                    {
                        descTb.Focus();
                        descTb.SelectAll();
                    }
                }
            }
        }), DispatcherPriority.Input);
    }

    private static T? FindVisualChildByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && string.Equals(typed.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                return typed;

            var result = FindVisualChildByTag<T>(child, tag);
            if (result != null) return result;
        }
        return null;
    }

    private void OnPartidaCantBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (e.Key is Key.Enter or Key.Down)
        {
            ApplyPartidaCantEdit(tb);
            FocusActiveMaterialesGridFirstCell();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            ApplyPartidaCantEdit(tb);
            FocusActiveMaterialesGridFirstCell();
            e.Handled = true;
        }
        else if (e.Key == Key.Up || (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
        {
            ApplyPartidaCantEdit(tb);
            int targetRow = LineasGrid.SelectedIndex >= 0 ? LineasGrid.SelectedIndex : _vm.Lineas.Count - 1;
            FocusLineasGridCell(targetRow, "Col_Cant");
            e.Handled = true;
        }
    }

    private void FocusActiveMaterialesGridFirstCell()
    {
        if (ActiveMaterialesGrid.Items.Count == 0)
        {
            if (_vm.SelectedLine != null && !_vm.SelectedLine.EsTitulo)
            {
                _vm.AddMaterialToActiveLine();
            }
        }

        if (ActiveMaterialesGrid.Items.Count > 0)
        {
            var firstItem = ActiveMaterialesGrid.Items[0];
            var targetCol = CodigoColumn ?? ActiveMaterialesGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly);
            if (targetCol != null)
            {
                DataGridSelection.SeleccionarFila(ActiveMaterialesGrid, firstItem, targetCol);
                ActiveMaterialesGrid.CurrentCell = new DataGridCellInfo(firstItem, targetCol);
                ActiveMaterialesGrid.ScrollIntoView(firstItem, targetCol);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DataGridExcelNavigation.FocusCell(ActiveMaterialesGrid, 0, targetCol);
                }), DispatcherPriority.Input);
            }
        }
    }

    private readonly ChangeOrderSectorFormulaController _formulaController = new();
    private int _lastHighlightedRow = -1;

    private void UpdateFormulaPickHighlight(int targetRow, int targetSlot)
    {
        if (_lastHighlightedRow >= 0 && _lastHighlightedRow < _vm.ActiveMateriales.Count)
        {
            _vm.ActiveMateriales[_lastHighlightedRow].FormulaPickSlot = -1;
        }

        if (targetRow >= 0 && targetRow < _vm.ActiveMateriales.Count && targetSlot >= 0)
        {
            _vm.ActiveMateriales[targetRow].FormulaPickSlot = targetSlot;
            _lastHighlightedRow = targetRow;
            ActiveMaterialesGrid.ScrollIntoView(_vm.ActiveMateriales[targetRow]);
        }
        else
        {
            _lastHighlightedRow = -1;
        }
    }

    private void OnSectorEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        _formulaController.HandlePreviewKeyDown(e, _vm.ActiveMateriales.Count, _sectorColumns.Count);
    }

    private void OnActiveMaterialesGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && _formulaController.HandleCellClick(dep, _sectorColumns, _vm))
        {
            e.Handled = true;
            return;
        }
    }

    private void OnActiveMaterialesGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_formulaController.HandlePreviewKeyDown(e, _vm.ActiveMateriales.Count, _sectorColumns.Count))
            return;

        int currentRowIndex = ActiveMaterialesGrid.Items.IndexOf(ActiveMaterialesGrid.CurrentCell.Item);

        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (currentRowIndex <= 0)
            {
                if (PartidaCantBox != null && PartidaCantBox.IsVisible && _vm.SelectedLine != null && !_vm.SelectedLine.EsTitulo)
                {
                    PartidaCantBox.Focus();
                    PartidaCantBox.SelectAll();
                    e.Handled = true;
                    return;
                }
                else
                {
                    FocusLineasGridCell(_vm.Lineas.Count - 1, "Col_Cant");
                    e.Handled = true;
                    return;
                }
            }
        }
        else if ((e.Key == Key.Delete || e.Key == Key.Back) && Keyboard.Modifiers == ModifierKeys.None)
        {
            var cell = ActiveMaterialesGrid.CurrentCell;
            if (cell.IsValid && cell.Item is ChangeOrderMaterialVm mat && _sectorColumns.Contains(cell.Column))
            {
                int slot = _sectorColumns.IndexOf(cell.Column);
                if (slot >= 0)
                {
                    mat.SetSector(slot, "");
                    _vm.RecalculateActiveMaterialFormulas();
                    _vm.RecalculateTotals();
                    e.Handled = true;
                }
            }
        }
        else if (e.Key is Key.Enter or Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (currentRowIndex == ActiveMaterialesGrid.Items.Count - 1 &&
                _vm.SelectedLine != null && !_vm.SelectedLine.EsContractual && !_vm.SelectedLine.EsTitulo)
            {
                var lastMat = _vm.ActiveMateriales.LastOrDefault();
                if (lastMat != null && (!string.IsNullOrWhiteSpace(lastMat.Codigo) || lastMat.Cantidad > 0m))
                {
                    _vm.AddMaterialToActiveLine();
                }
            }
        }
    }

    private void OnActiveMaterialesGridPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DataGridExcelNavigation.TryBeginEditFromChar(ActiveMaterialesGrid, e.Text))
        {
            e.Handled = true;
        }
    }

    private void OnPartidaCantBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            ApplyPartidaCantEdit(tb);
        }
    }

    private void ApplyPartidaCantEdit(TextBox tb, ChangeOrderLineVm? line = null)
    {
        var targetLine = line ?? tb.DataContext as ChangeOrderLineVm ?? _vm.SelectedLine;
        if (targetLine == null || targetLine.EsTitulo) return;
        string clean = tb.Text?.Trim() ?? "";
        if (clean.StartsWith("=", StringComparison.Ordinal))
            clean = clean[1..].TrimStart();

        decimal val = 0m;
        bool ok = false;
        try
        {
            var res = QuantityFormulaService.Evaluate(clean);
            if (res.Success)
            {
                val = res.Value;
                ok = true;
            }
        }
        catch { }

        if (!ok && ChangeOrderEditorViewModel.ParseDecimal(clean, out var directVal))
        {
            val = directVal;
            ok = true;
        }

        if (ok)
        {
            targetLine.CantidadAdicional = val;
            targetLine.CantidadAdicionalText = val % 1 == 0 ? Math.Round(val, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture) : val.ToString("#,##0.##", CultureInfo.InvariantCulture);
            if (!targetLine.EsContractual && !targetLine.EsTitulo && targetLine.Materiales.Count > 0)
            {
                targetLine.UpdateExtraordinariaPriceFromMateriales();
            }
            targetLine.Recalculate();
            _vm.RecalculateActiveMaterialFormulas();
            _vm.RecalculateTotals();
            _vm.RefreshSelectedLineHeader();
        }
        else
        {
            tb.Text = targetLine.CantidadAdicionalText;
        }
    }

    private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (ReferenceEquals(e.Column, CodigoColumn) && e.EditingElement is TextBox tb)
        {
            _currentCodigoEditor = tb;
            tb.TextChanged -= OnCodigoEditorTextChanged;
            tb.TextChanged += OnCodigoEditorTextChanged;
            tb.PreviewKeyDown -= OnCodigoEditorPreviewKeyDown;
            tb.PreviewKeyDown += OnCodigoEditorPreviewKeyDown;
            AutocompletePopup.PlacementTarget = tb;
        }
        else if (_sectorColumns.Contains(e.Column) && e.EditingElement is TextBox sectorBox && e.Row.Item is ChangeOrderMaterialVm mat)
        {
            int slot = _sectorColumns.IndexOf(e.Column);
            int rowIdx = _vm.ActiveMateriales.IndexOf(mat);
            if (slot >= 0)
            {
                _formulaController.OnPreparingCellForEdit(sectorBox, slot, rowIdx);

                sectorBox.Tag = new SectorEditContext(mat, slot);
                sectorBox.TextChanged -= OnSectorBoxTextChanged;
                sectorBox.TextChanged += OnSectorBoxTextChanged;
                sectorBox.PreviewKeyDown -= OnSectorEditorPreviewKeyDown;
                sectorBox.PreviewKeyDown += OnSectorEditorPreviewKeyDown;

                sectorBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (sectorBox.Text.StartsWith("=") || sectorBox.Text.StartsWith("+"))
                    {
                        sectorBox.CaretIndex = sectorBox.Text.Length;
                        sectorBox.SelectionLength = 0;
                    }
                    else
                    {
                        sectorBox.SelectAll();
                    }
                }), DispatcherPriority.Input);
            }
        }
        else if (e.EditingElement is TextBox editBox)
        {
            editBox.SelectAll();
        }
    }

    private void OnSectorBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is SectorEditContext ctx)
        {
            _formulaController.OnTextChanged(tb);
            ctx.Material.SetSector(ctx.Slot, tb.Text);
            _vm.RecalculateActiveMaterialFormulas();
        }
    }

    private void OnGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (ReferenceEquals(e.Column, CodigoColumn) && e.EditingElement is TextBox tb)
        {
            tb.TextChanged -= OnCodigoEditorTextChanged;
            tb.PreviewKeyDown -= OnCodigoEditorPreviewKeyDown;
            if (ReferenceEquals(_currentCodigoEditor, tb))
                _currentCodigoEditor = null;

            AutocompletePopup.IsOpen = false;

            if (e.Row.Item is ChangeOrderMaterialVm mat)
            {
                string code = tb.Text?.Trim() ?? "";
                mat.Codigo = code;
                mat.ResolveFromPriceBank(_priceBankStore);
                _vm.RecalculateActiveMaterialFormulas();
            }
        }
        else if (e.Row.Item is ChangeOrderMaterialVm mat)
        {
            int slot = _sectorColumns.IndexOf(e.Column);
            if (slot >= 0 && e.EditingElement is TextBox sectorBox)
            {
                sectorBox.TextChanged -= OnSectorBoxTextChanged;
                sectorBox.PreviewKeyDown -= OnSectorEditorPreviewKeyDown;
                _formulaController.OnCellEditEnding(sectorBox);
                mat.SetSector(slot, sectorBox.Text);
                _vm.RecalculateActiveMaterialFormulas();
            }
            else if (e.EditingElement is TextBox valBox && e.Column is DataGridColumn col)
            {
                if (col == MatPriceColumn || (col.Header is string hm && (hm.Contains("Mat") || hm.Contains("MAT"))))
                {
                    mat.MatClpText = valBox.Text;
                    _vm.RecalculateActiveMaterialFormulas();
                }
                else if (col == LaborPriceColumn || (col.Header is string hmo && (hmo.Contains("MO") || hmo.Contains("Mo"))))
                {
                    mat.MoClpText = valBox.Text;
                    _vm.RecalculateActiveMaterialFormulas();
                }
                else if (col == GgUtColumn || (col.Header is string hgg && hgg.Contains("GG")))
                {
                    mat.GgUtFactorText = valBox.Text;
                    _vm.RecalculateActiveMaterialFormulas();
                }
                else if (col == ObservacionesColumn || (col.Header is string ho && ho.Contains("OBSERV")))
                {
                    mat.NotaInterna = valBox.Text;
                }
            }
        }
    }

    private void OnCodigoEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string query = tb.Text?.Trim() ?? "";
        if (_priceBankStore != null && query.Length >= 2)
        {
            var matches = _priceBankStore.Search(query).Take(15).ToList();
            if (matches.Count > 0)
            {
                AutocompleteList.ItemsSource = matches;
                AutocompleteList.SelectedIndex = 0;
                AutocompletePopup.PlacementTarget = tb;
                AutocompletePopup.IsOpen = true;
                return;
            }
        }
        AutocompletePopup.IsOpen = false;
    }

    private void OnCodigoEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!AutocompletePopup.IsOpen) return;

        if (e.Key == Key.Down)
        {
            if (AutocompleteList.SelectedIndex < AutocompleteList.Items.Count - 1)
                AutocompleteList.SelectedIndex++;
            AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (AutocompleteList.SelectedIndex > 0)
                AutocompleteList.SelectedIndex--;
            AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Tab)
        {
            if (AutocompleteList.SelectedItem is PriceBankEntry selected)
            {
                ApplyAutocompleteSelection(selected);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            AutocompletePopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnAutocompleteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PriceBankEntry selected)
        {
            ApplyAutocompleteSelection(selected);
        }
        else if (AutocompleteList.SelectedItem is PriceBankEntry sel)
        {
            ApplyAutocompleteSelection(sel);
        }
    }

    private void OnAutocompleteKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab && AutocompleteList.SelectedItem is PriceBankEntry selected)
        {
            ApplyAutocompleteSelection(selected);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AutocompletePopup.IsOpen = false;
            _currentCodigoEditor?.Focus();
            e.Handled = true;
        }
    }

    private void ApplyAutocompleteSelection(PriceBankEntry entry)
    {
        AutocompletePopup.IsOpen = false;
        var mat = ActiveMaterialesGrid.CurrentCell.Item as ChangeOrderMaterialVm ?? _currentCodigoEditor?.DataContext as ChangeOrderMaterialVm;
        if (mat != null)
        {
            mat.Codigo = entry.Codigo;
            mat.Descripcion = entry.Descripcion;
            if (!string.IsNullOrWhiteSpace(entry.Unidad))
                mat.Unidad = entry.Unidad;
            mat.MatClp = entry.MatClpConFallback;
            mat.MoClp = entry.MoClpConFallback;
            mat.Recalculate();
            _vm.RecalculateActiveMaterialFormulas();
            _vm.RecalculateTotals();
        }
        if (_currentCodigoEditor != null)
        {
            _currentCodigoEditor.Text = entry.Codigo;
        }
        ActiveMaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ActiveMaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void OnVerResumenMaterialesClick(object sender, RoutedEventArgs e)
    {
        ChangeOrderMaterialsSummaryDialog.Show(this, _vm.Lineas, _vm.MuestraUf, _vm.UfRate);
    }

    private bool _allowClose;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (AutocompletePopup.IsOpen)
            {
                AutocompletePopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            e.Handled = true;
            RequestClose();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        RequestClose();
    }

    private void RequestClose()
    {
        if (_vm.HasChanges())
        {
            var result = AppMessageDialog.Show(
                "Guardar cambios",
                "Ha realizado modificaciones en el adicional. \u00BFDesea guardar los cambios antes de salir?",
                primaryText: "Guardar",
                secondaryText: "Salir sin guardar",
                cancelText: "Continuar editando",
                isWarning: true);

            if (result == AppMessageDialogResult.Primary)
            {
                if (_vm.TrySave(_draft))
                {
                    _allowClose = true;
                    DialogResult = true;
                }
                else if (!string.IsNullOrWhiteSpace(_vm.ValidationErrorMessage))
                {
                    WpfDialogService.Warning("Validaci\u00F3n", _vm.ValidationErrorMessage);
                }
                return;
            }

            if (result == AppMessageDialogResult.Secondary)
            {
                _allowClose = true;
                DialogResult = false;
                return;
            }

            // Continuar editando
            return;
        }

        _allowClose = true;
        DialogResult = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_allowClose)
            return;

        if (_vm.HasChanges())
        {
            var result = AppMessageDialog.Show(
                "Guardar cambios",
                "Ha realizado modificaciones en el adicional. \u00BFDesea guardar los cambios antes de salir?",
                primaryText: "Guardar",
                secondaryText: "Salir sin guardar",
                cancelText: "Continuar editando",
                isWarning: true);

            if (result == AppMessageDialogResult.Primary)
            {
                if (_vm.TrySave(_draft))
                {
                    _allowClose = true;
                    DialogResult = true;
                    return;
                }

                e.Cancel = true;
                if (!string.IsNullOrWhiteSpace(_vm.ValidationErrorMessage))
                {
                    WpfDialogService.Warning("Validaci\u00F3n", _vm.ValidationErrorMessage);
                }
                return;
            }

            if (result == AppMessageDialogResult.Secondary)
            {
                _allowClose = true;
                DialogResult = false;
                return;
            }

            e.Cancel = true;
            return;
        }

        _allowClose = true;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave(_draft))
        {
            _allowClose = true;
            DialogResult = true;
        }
    }

    public static bool Show(
        ChangeOrderRow oc,
        bool isNew,
        bool muestraUf = true,
        decimal ufRate = 0m,
        IEnumerable<ChangeOrderPartidaPickerItem>? contractPartidas = null,
        IEnumerable<CubicacionRow>? baseProjectCubicacion = null,
        IPriceBankStore? priceBankStore = null,
        BudgetProjectSession? session = null)
    {
        var dlg = new ChangeOrderEditorDialog(oc, muestraUf, ufRate, contractPartidas, baseProjectCubicacion, priceBankStore, session);
        dlg.Configure(isNew);
        return dlg.ShowDialog() == true;
    }
}
