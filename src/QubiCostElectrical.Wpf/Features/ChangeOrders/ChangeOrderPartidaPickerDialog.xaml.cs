using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed class ChangeOrderPartidaPickerItem
{
    public string Id { get; init; } = "";
    public string Item { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "";
    public decimal CantidadContrato { get; init; }
    public decimal PrecioUnitarioClp { get; init; }
    public decimal PrecioUnitarioUf { get; init; }
    public bool MuestraUf { get; init; } = true;

    public string PrecioUnitarioDisplay => MuestraUf
        ? $"{PrecioUnitarioUf:N3} UF"
        : $"$ {Math.Round(PrecioUnitarioClp, 0, MidpointRounding.AwayFromZero):N0}";

    public string CantidadDisplay => CantidadContrato % 1 == 0 ? $"{Math.Round(CantidadContrato, MidpointRounding.AwayFromZero):N0} {Unidad}" : $"{CantidadContrato:#,##0.##} {Unidad}";
}

public partial class ChangeOrderPartidaPickerDialog : Window
{
    private readonly List<ChangeOrderPartidaPickerItem> _allItems;
    private List<ChangeOrderPartidaPickerItem> _filteredItems;

    public ChangeOrderPartidaPickerItem? SelectedItem { get; private set; }

    public ChangeOrderPartidaPickerDialog(IEnumerable<ChangeOrderPartidaPickerItem> items)
    {
        _allItems = items?.ToList() ?? new List<ChangeOrderPartidaPickerItem>();
        _filteredItems = new List<ChangeOrderPartidaPickerItem>(_allItems);

        InitializeComponent();
        Owner = Application.Current?.MainWindow;

        PartidasGrid.ItemsSource = _filteredItems;
        UpdateCount();

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            if (_filteredItems.Count > 0)
            {
                PartidasGrid.SelectedIndex = 0;
            }
        };
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text.Trim();
        SearchWatermark.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(text))
        {
            _filteredItems = new List<ChangeOrderPartidaPickerItem>(_allItems);
        }
        else
        {
            _filteredItems = _allItems.Where(p =>
                p.Item.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Descripcion.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Unidad.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        PartidasGrid.ItemsSource = _filteredItems;
        UpdateCount();

        if (_filteredItems.Count > 0)
            PartidasGrid.SelectedIndex = 0;
    }

    private void UpdateCount()
    {
        CountLabel.Text = $"{_filteredItems.Count} de {_allItems.Count} partidas";
    }

    private void OnSearchPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && _filteredItems.Count > 0)
        {
            PartidasGrid.Focus();
            if (PartidasGrid.SelectedIndex < 0)
                PartidasGrid.SelectedIndex = 0;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && PartidasGrid.SelectedItem is ChangeOrderPartidaPickerItem item)
        {
            ConfirmSelection(item);
            e.Handled = true;
        }
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PartidasGrid.SelectedItem is ChangeOrderPartidaPickerItem item)
            ConfirmSelection(item);
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectBtn.IsEnabled = PartidasGrid.SelectedItem is ChangeOrderPartidaPickerItem;
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        if (PartidasGrid.SelectedItem is ChangeOrderPartidaPickerItem item)
            ConfirmSelection(item);
    }

    private void ConfirmSelection(ChangeOrderPartidaPickerItem item)
    {
        SelectedItem = item;
        DialogResult = true;
    }

    public static ChangeOrderPartidaPickerItem? SelectPartida(Window? owner, IEnumerable<ChangeOrderPartidaPickerItem> items)
    {
        var dlg = new ChangeOrderPartidaPickerDialog(items);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.SelectedItem : null;
    }
}
