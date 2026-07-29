using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class PrecioListaResumenDialog : Window
{
    private readonly PrecioListaResumenVm vm;
    private TextBox? _activeEditor;
    private bool _isEditingCodigo;

    public PrecioListaResumenDialog(BudgetProjectSession session, IPriceBankStore priceBankStore, string autoFilterCodigo = "")
    {
        InitializeComponent();
        vm = new PrecioListaResumenVm(session, priceBankStore, autoFilterCodigo);
        DataContext = vm;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (MainGrid.Items.Count > 0)
        {
            MainGrid.Focus();
            if (MainGrid.Columns.Count > 3)
            {
                MainGrid.CurrentCell = new DataGridCellInfo(MainGrid.Items[0], MainGrid.Columns[3]);
                MainGrid.BeginEdit();
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (vm.AutocompleteVisible)
            {
                vm.AutocompleteVisible = false;
                e.Handled = true;
                return;
            }
            Close();
            e.Handled = true;
        }
    }

    private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (_activeEditor != null)
        {
            _activeEditor.TextChanged -= OnEditorTextChanged;
            _activeEditor.PreviewKeyDown -= OnEditorPreviewKeyDown;
            _activeEditor.LostFocus -= OnEditorLostFocus;
        }

        if (e.EditingElement is not TextBox tb) return;
        var col = e.Column.Header as string;
        if (col != "CÓDIGO" && col != "DESCRIPCIÓN") return;

        _activeEditor = tb;
        _isEditingCodigo = col == "CÓDIGO";
        tb.TextChanged += OnEditorTextChanged;
        tb.PreviewKeyDown += OnEditorPreviewKeyDown;
        AutocompletePopup.PlacementTarget = tb;
        tb.LostFocus += OnEditorLostFocus;
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_activeEditor == null) return;
        var query = _activeEditor.Text;
        vm.SearchBank(query);
    }

    private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!vm.AutocompleteVisible) return;

        switch (e.Key)
        {
            case Key.Down:
                AutocompleteList.SelectedIndex = Math.Min(AutocompleteList.SelectedIndex + 1, AutocompleteList.Items.Count - 1);
                if (AutocompleteList.SelectedIndex < 0) AutocompleteList.SelectedIndex = 0;
                e.Handled = true;
                break;

            case Key.Up:
                AutocompleteList.SelectedIndex = Math.Max(AutocompleteList.SelectedIndex - 1, 0);
                e.Handled = true;
                break;

            case Key.Enter:
                if (AutocompleteList.SelectedItem is PriceBankSuggestionVm sel)
                    ApplySuggestion(sel);
                e.Handled = true;
                break;

            case Key.Escape:
                vm.AutocompleteVisible = false;
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
        if (AutocompleteList.SelectedItem is PriceBankSuggestionVm sel)
            ApplySuggestion(sel);
    }

    private void ApplySuggestion(PriceBankSuggestionVm suggestion)
    {
        if (MainGrid.CurrentItem is not PrecioListaMaterialVm row) return;
        vm.ApplySuggestion(row, suggestion);
        vm.AutocompleteVisible = false;
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
    }

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        var dialog = new PrecioListaEditarDialog(vm, null) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnEliminarClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PrecioListaMaterialVm material })
        {
            if (WpfDialogService.Confirm("Eliminar material",
                $"¿Eliminar '{material.Codigo}' · {material.Descripcion} de la lista de precios?"))
            {
                vm.EliminarMaterial(material);
            }
        }
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel) return;
        if (e.Row.Item is not PrecioListaMaterialVm item) return;
        if (e.Column is not DataGridTextColumn col) return;
        if (e.EditingElement is not TextBox tb) return;

        var header = col.Header as string;

        if (header == "CÓDIGO" || header == "DESCRIPCIÓN")
        {
            var text = tb.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            if (header == "CÓDIGO")
            {
                item.Codigo = text;
                var match = vm.LookupByCodigo(text);
                if (match != null)
                {
                    item.Descripcion = match.Descripcion ?? "";
                    item.Unidad = match.Unidad ?? "";
                    item.MatClp = match.MatClp;
                    item.MoClp = match.MoClp;
                }
            }
            else
            {
                item.Descripcion = text;
                var prices = vm.LookupPricesByDescripcion(text);
                if (prices.HasValue)
                {
                    item.MatClp = prices.Value.MatClp;
                    item.MoClp = prices.Value.MoClp;
                }
            }

            item.FechaPrecio = DateTime.Today;
            vm.ActualizarPrecioEnSesion(item);
            return;
        }

        if (header != "MAT $ CLP" && header != "MO $ CLP") return;
        var raw = tb.Text.Replace(".", "").Replace(",", "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (!decimal.TryParse(raw, out var nuevoValor)) return;
        if (nuevoValor < 0) return;

        if (header == "MAT $ CLP")
            item.MatClp = nuevoValor;
        else
            item.MoClp = nuevoValor;

        item.FechaPrecio = DateTime.Today;
        vm.ActualizarPrecioEnSesion(item);
    }
}
