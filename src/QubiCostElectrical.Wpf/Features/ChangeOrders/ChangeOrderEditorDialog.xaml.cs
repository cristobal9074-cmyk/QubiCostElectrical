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
        if (availableContractPartidas != null)
        {
            _vm.AvailableContractPartidas = availableContractPartidas.ToList();
        }
        _vm.LoadFrom(draft);

        InitializeComponent();
        DataContext = _vm;
        Owner = Application.Current?.MainWindow;

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
        var headerStyle = (Style)FindResource("DataGridColumnHeader.Prototype");

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

        // Insertar las columnas de zonas justo antes de las columnas de precio (P. Mat, Subtotal, Quitar)
        int insertIndex = ActiveMaterialesGrid.Columns.Count - 3;
        if (insertIndex < 5) insertIndex = 5;

        for (int i = 0; i < zoneNames.Count; i++)
        {
            int slot = i;
            string zName = zoneNames[i];

            var headerPanel = new StackPanel
            {
                ToolTip = string.IsNullOrEmpty(zName) ? null : zName,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            if (!string.IsNullOrEmpty(zName))
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = zName,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    MaxWidth = 54,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            var col = new DataGridTextColumn
            {
                Header = headerPanel,
                Width = 56,
                MinWidth = 48,
                CanUserSort = false,
                Binding = new Binding($"SectorValues[{slot}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                CellStyle = baseCellStyle,
                HeaderStyle = headerStyle,
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
        if (SubtotalMatColumn != null) SubtotalMatColumn.Visibility = casillas ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Configure(bool isNew)
    {
        TitleBlock.Text = isNew ? "Nuevo Adicional de Obra & Cubicación" : "Espacio de Trabajo — Adicional de Obra & Cubicación";
        Loaded += (_, _) => TituloBox.Focus();
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
        if (!_vm.HasSelectedLine)
        {
            WpfDialogService.Warning("Seleccione una partida", "Primero seleccione una partida en la tabla superior para agregarle materiales a cubicar.");
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
        if (!_vm.HasSelectedLine)
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

    private void OnPartidaCantBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab && sender is TextBox tb)
        {
            ApplyPartidaCantEdit(tb);
            tb.SelectAll();
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

    private void ApplyPartidaCantEdit(TextBox tb)
    {
        if (_vm.SelectedLine == null) return;
        if (ChangeOrderEditorViewModel.ParseDecimal(tb.Text, out var val))
        {
            _vm.SelectedLine.CantidadAdicional = Math.Max(0m, val);
            _vm.SelectedLine.Recalculate();
            _vm.RecalculateTotals();
        }
        else
        {
            tb.Text = _vm.SelectedLine.CantidadAdicionalText;
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
            if (slot >= 0)
            {
                sectorBox.Tag = new SectorEditContext(mat, slot);
                sectorBox.TextChanged -= OnSectorBoxTextChanged;
                sectorBox.TextChanged += OnSectorBoxTextChanged;
            }
        }
    }

    private void OnSectorBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is SectorEditContext ctx)
        {
            ctx.Material.SetSector(ctx.Slot, tb.Text);
            if (ctx.Material.Cantidad > 0m && _vm.SelectedLine != null &&
                !string.Equals(_vm.SelectedLine.Unidad, "GL", StringComparison.OrdinalIgnoreCase))
            {
                _vm.SelectedLine.CantidadAdicional = ctx.Material.Cantidad;
            }
            _vm.RecalculateTotals();
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
                _vm.RecalculateTotals();
            }
        }
        else if (e.Row.Item is ChangeOrderMaterialVm mat)
        {
            int slot = _sectorColumns.IndexOf(e.Column);
            if (slot >= 0 && e.EditingElement is TextBox sectorBox)
            {
                sectorBox.TextChanged -= OnSectorBoxTextChanged;
                mat.SetSector(slot, sectorBox.Text);
                if (mat.Cantidad > 0m && _vm.SelectedLine != null &&
                    !string.Equals(_vm.SelectedLine.Unidad, "GL", StringComparison.OrdinalIgnoreCase))
                {
                    _vm.SelectedLine.CantidadAdicional = mat.Cantidad;
                }
                _vm.RecalculateTotals();
            }
            else if (e.EditingElement is TextBox valBox && e.Column.Header is string h && h.Contains("Mat"))
            {
                mat.MatClpText = valBox.Text;
                _vm.RecalculateTotals();
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
        if (ActiveMaterialesGrid.SelectedItem is ChangeOrderMaterialVm mat)
        {
            mat.Codigo = entry.Codigo;
            mat.Descripcion = entry.Descripcion;
            if (!string.IsNullOrWhiteSpace(entry.Unidad))
                mat.Unidad = entry.Unidad;
            mat.MatClp = entry.MatClpConFallback;
            mat.MoClp = entry.MoClpConFallback;
            mat.Recalculate();
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

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave(_draft))
            DialogResult = true;
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
