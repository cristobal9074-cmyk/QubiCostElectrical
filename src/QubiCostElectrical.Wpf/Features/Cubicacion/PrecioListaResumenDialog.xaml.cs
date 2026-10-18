using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class PrecioListaResumenDialog : Window
{
    private readonly PrecioListaResumenVm vm;
    private TextBox? _activeEditor;
    private bool _isEditingCodigo;

    /// <summary>
    /// Código del último material creado desde esta ventana (alta rápida o alta manual). Quien la abre
    /// lo usa para encadenarlo a la cubicación: el material recién creado entra a la partida en curso.
    /// </summary>
    public string? MaterialCreado { get; private set; }

    public PrecioListaResumenDialog(BudgetProjectSession session, IPriceBankStore priceBankStore, string autoFilterCodigo = "")
    {
        InitializeComponent();
        vm = new PrecioListaResumenVm(session, priceBankStore, autoFilterCodigo);
        DataContext = vm;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusMainGrid();
    }

    private void FocusMainGrid()
    {
        if (MainGrid.Items.Count == 0) return;
        MainGrid.Focus();

        var column = MainGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible) ?? MainGrid.Columns[0];
        var item = (MainGrid.CurrentCell.IsValid && MainGrid.CurrentCell.Item != null)
            ? MainGrid.CurrentCell.Item
            : MainGrid.Items[0];

        if (item == null) return;

        DataGridSelection.SeleccionarFila(MainGrid, item, column);

        MainGrid.CurrentCell = new DataGridCellInfo(item, column);
        MainGrid.ScrollIntoView(item);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ctrl++: alta rápida de material — pide familia, precio y si usa Material × % × FA, y lo
        // ingresa al instante en la lista de precios del proyecto.
        if (EsAltaRapida(key, Keyboard.Modifiers))
        {
            AbrirAltaRapida();
            e.Handled = true;
            return;
        }

        // Escape: cerrar sugerencias, o limpiar buscador, o cerrar ventana
        if (key == Key.Escape)
        {
            if (vm.AutocompleteVisible)
            {
                vm.AutocompleteVisible = false;
                e.Handled = true;
                return;
            }

            if (SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                FocusMainGrid();
                e.Handled = true;
                return;
            }

            Close();
            e.Handled = true;
            return;
        }

        // Ctrl+B o Ctrl+F: activar buscador
        if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.B || key == Key.F))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (key == Key.Enter && vm.AutocompleteVisible)
        {
            vm.AutocompleteVisible = false;
            e.Handled = true;
            return;
        }
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Down || key == Key.Enter)
        {
            FocusMainGrid();
            e.Handled = true;
        }
    }

    private void OnFilterChipClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilterChipVm chip })
        {
            vm.ActiveFilter = chip.Nombre;
            FocusMainGrid();
        }
    }

    private void OnGridPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (_activeEditor != null)
        {
            _activeEditor.TextChanged -= OnEditorTextChanged;
            _activeEditor.PreviewKeyDown -= OnEditorPreviewKeyDown;
            _activeEditor.LostFocus -= OnEditorLostFocus;
        }

        if (e.EditingElement is not TextBox textBox) return;
        var header = e.Column.Header as string;
        if (header is not ("CÓDIGO" or "DESCRIPCIÓN")) return;

        _activeEditor = textBox;
        _isEditingCodigo = header == "CÓDIGO";
        textBox.TextChanged += OnEditorTextChanged;
        textBox.PreviewKeyDown += OnEditorPreviewKeyDown;
        AutocompletePopup.PlacementTarget = textBox;
        textBox.LostFocus += OnEditorLostFocus;
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_activeEditor != null)
            vm.SearchBank(_activeEditor.Text);
    }

    private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!vm.AutocompleteVisible) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.AutocompleteVisible = false;
                e.Handled = true;
                break;
            case Key.Tab:
                if (AutocompleteList.SelectedItem is PriceBankSuggestionVm suggestion)
                    ApplySuggestion(suggestion);
                e.Handled = true;
                break;
            case Key.Up:
                AutocompleteList.SelectedIndex = Math.Max(AutocompleteList.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
            case Key.Down:
                AutocompleteList.SelectedIndex = Math.Min(
                    AutocompleteList.SelectedIndex + 1,
                    AutocompleteList.Items.Count - 1);
                if (AutocompleteList.SelectedIndex < 0)
                    AutocompleteList.SelectedIndex = 0;
                e.Handled = true;
                break;
        }
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!AutocompletePopup.IsMouseOver)
            vm.AutocompleteVisible = false;
    }

    private void OnAutocompleteMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (AutocompleteList.SelectedItem is PriceBankSuggestionVm suggestion)
            ApplySuggestion(suggestion);
    }

    private void ApplySuggestion(PriceBankSuggestionVm suggestion)
    {
        if (MainGrid.CurrentItem is not PrecioListaMaterialVm target) return;
        vm.ApplySuggestion(target, suggestion);
        vm.AutocompleteVisible = false;
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
    }

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        var dialog = new PrecioListaEditarDialog(vm, null) { Owner = this };
        dialog.ShowDialog();
        if (!string.IsNullOrWhiteSpace(dialog.CodigoCreado))
        {
            MaterialCreado = dialog.CodigoCreado;
            Close();
        }
    }

    private void OnAltaRapidaClick(object sender, RoutedEventArgs e) => AbrirAltaRapida();

    /// <summary>
    /// Gestor de teclado del alta rápida: Ctrl++ en sus tres formas reales — teclado numérico
    /// (Add), la tecla "+/=" y las variantes desplazadas de teclados latinos (Ctrl+Shift+').
    /// Estático y aparte del evento para poder probarlo: <c>Keyboard.Modifiers</c> no se puede
    /// simular desde un test.
    /// </summary>
    internal static bool EsAltaRapida(Key key, ModifierKeys modifiers)
    {
        if ((modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return false;
        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            return false;

        if (key is Key.Add or Key.OemPlus)
            return true;

        bool shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        return shift && key is Key.Oem1 or Key.OemQuestion;
    }

    /// <summary>
    /// Alta rápida de material (Ctrl++): abre el formulario que pide familia, precio y si la M.O.
    /// se calcula con Material × % × FA, e ingresa el material en la lista de precios del proyecto.
    /// Al crearlo, cierra la ventana para volver de inmediato a la cubicación con el material asignado.
    /// </summary>
    private void AbrirAltaRapida()
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);

        // El autocompletado de la celda en edición es un Popup: si queda abierto, captura el mouse
        // por encima del modal y la app entera deja de responder a los clics.
        vm.AutocompleteVisible = false;

        var dialog = new PrecioListaAltaRapidaDialog(vm) { Owner = this };
        bool creado = dialog.ShowDialog() == true;

        if (creado && !string.IsNullOrWhiteSpace(dialog.CodigoCreado))
        {
            MaterialCreado = dialog.CodigoCreado;
            Close();
            return;
        }

        FocusMainGrid();
    }

    /// <summary>Lleva la selección a la fila del material recién creado (y la muestra en pantalla).</summary>
    private void SelectRowByCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return;

        var item = vm.Items?.OfType<PrecioListaMaterialVm>()
            .FirstOrDefault(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (item == null) return;

        var column = MainGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible) ?? MainGrid.Columns[0];

        // SeleccionarFila se ocupa del SelectionUnit de la grilla: esta es de celdas
        // (DataGrid.Workspace.Excel), y marcar SelectedItem aquí reventaba con "SelectionUnit …
        // impide seleccionar filas" al generarse el contenedor de la fila recién creada, dejando la
        // app congelada (ver DataGridSelection.SeleccionarFila).
        DataGridSelection.SeleccionarFila(MainGrid, item, column);
        MainGrid.CurrentCell = new DataGridCellInfo(item, column);
        MainGrid.ScrollIntoView(item);
    }

    private void OnEliminarClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PrecioListaMaterialVm row }) return;
        int usos = vm.ContarUsosEnCubicacion(row.Codigo);
        string avisoUso = usos > 0
            ? $"\n\n⚠️ ATENCIÓN: Este material está utilizado en {usos} fila(s) de la cubicación actual. Si lo eliminas, los recursos mantendrán sus importes actuales pero quedarán desvinculados del catálogo."
            : "";

        if (!WpfDialogService.Confirm("Eliminar material",
                $"¿Eliminar '{row.Codigo}' · {row.Descripcion} del catálogo de este proyecto?{avisoUso}",
                isWarning: usos > 0))
            return;

        vm.EliminarMaterial(row);
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel
            || e.Row.Item is not PrecioListaMaterialVm row
            || e.Column is not DataGridTextColumn textColumn
            || e.EditingElement is not TextBox textBox)
        {
            return;
        }

        var header = textColumn.Header as string;
        var text = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        switch (header)
        {
            case "CÓDIGO":
                row.Codigo = text;
                var byCodigo = vm.LookupByCodigo(text);
                if (byCodigo != null)
                {
                    row.Descripcion = byCodigo.Descripcion ?? "";
                    row.Unidad = byCodigo.Unidad ?? "";
                    row.MatClp = byCodigo.MatClp;
                    row.MoClp = byCodigo.MoClp;
                }
                row.FechaPrecio = DateTime.Today;
                vm.ActualizarPrecioEnSesion(row);
                break;
            case "DESCRIPCIÓN":
                row.Descripcion = text;
                var prices = vm.LookupPricesByDescripcion(text);
                if (prices.HasValue)
                {
                    row.MatClp = prices.Value.MatClp;
                    row.MoClp = prices.Value.MoClp;
                }
                row.FechaPrecio = DateTime.Today;
                vm.ActualizarPrecioEnSesion(row);
                break;
            case "MAT $ CLP" or "MO $ CLP":
                var normalized = text.Replace(".", "").Replace(",", "").Trim();
                if (!decimal.TryParse(normalized, out var amount) || amount < 0m) return;
                if (header == "MAT $ CLP") row.MatClp = amount;
                else row.MoClp = amount;
                row.FechaPrecio = DateTime.Today;
                vm.ActualizarPrecioEnSesion(row);
                break;
        }
    }
}
