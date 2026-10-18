using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public sealed class MaterialPickerItem : ViewModelBase
{
    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "UN";
    public string Categoria { get; set; } = "";
    public decimal CantidadCubicada { get; set; }
    public decimal CantidadSolicitada { get; set; }
    public decimal CantidadDisponible => Math.Max(0m, CantidadCubicada - CantidadSolicitada);
    public bool TieneSobrepedido => CantidadSolicitada > CantidadCubicada;
}

public partial class SolicitudMaterialPickerDialog : Window
{
    private readonly List<MaterialPickerItem> allItems;
    private readonly ObservableCollection<MaterialPickerItem> filteredItems = new();

    public IReadOnlyList<MaterialPickerItem> SelectedItems { get; private set; } = Array.Empty<MaterialPickerItem>();

    public SolicitudMaterialPickerDialog(IEnumerable<MaterialPickerItem> items)
    {
        InitializeComponent();

        allItems = items?.ToList() ?? new List<MaterialPickerItem>();
        DgMaterials.ItemsSource = filteredItems;

        PopulateCategories();
        ApplyFilter();
    }

    private void PopulateCategories()
    {
        var categories = allItems
            .Select(x => string.IsNullOrWhiteSpace(x.Categoria) ? "General" : x.Categoria.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        categories.Insert(0, "Todas las categorías");
        CmbCategory.ItemsSource = categories;
        CmbCategory.SelectedIndex = 0;
    }

    private void ApplyFilter()
    {
        var search = TxtSearch.Text?.Trim() ?? "";
        var category = CmbCategory.SelectedItem as string ?? "Todas las categorías";

        var query = allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                (x.Codigo ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.Descripcion ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.Categoria ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(category, "Todas las categorías", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => string.Equals(x.Categoria, category, StringComparison.OrdinalIgnoreCase));
        }

        filteredItems.Clear();
        foreach (var item in query)
        {
            filteredItems.Add(item);
        }

        TxtCount.Text = $"{filteredItems.Count} de {allItems.Count} materiales";
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk)
        {
            bool check = chk.IsChecked == true;
            foreach (var item in filteredItems)
            {
                item.IsSelected = check;
            }
        }
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgMaterials.SelectedItem is MaterialPickerItem item)
        {
            item.IsSelected = true;
            SelectedItems = new List<MaterialPickerItem> { item };
            DialogResult = true;
            Close();
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var selected = allItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0 && DgMaterials.SelectedItem is MaterialPickerItem current)
        {
            selected.Add(current);
        }

        if (selected.Count == 0)
        {
            WpfDialogService.InfoModal("Selección", "Por favor seleccione al menos un material marcando su casilla o haciendo doble clic.");
            return;
        }

        SelectedItems = selected;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
