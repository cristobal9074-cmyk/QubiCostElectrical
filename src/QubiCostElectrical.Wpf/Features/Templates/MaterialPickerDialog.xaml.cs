using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Templates;

public partial class MaterialPickerDialog : Window
{
    private readonly List<MaterialRow> allMaterials;

    public MaterialPickerDialog(IEnumerable<MaterialRow> materials)
    {
        allMaterials = (materials ?? Enumerable.Empty<MaterialRow>()).ToList();
        InitializeComponent();
        MaterialsGrid.ItemsSource = allMaterials;
        
        Loaded += (_, _) => SearchBox.Focus();
    }

    public List<MaterialRow> SelectedMaterials { get; private set; } = new();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text.Trim();
        MaterialsGrid.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? allMaterials
            : allMaterials.Where(m =>
                (m.Descripcion ?? "").Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (m.Codigo ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void MaterialsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        SelectedMaterials = MaterialsGrid.SelectedItems.Cast<MaterialRow>().ToList();
        if (SelectedMaterials.Count > 0)
        {
            DialogResult = true;
        }
    }
}
