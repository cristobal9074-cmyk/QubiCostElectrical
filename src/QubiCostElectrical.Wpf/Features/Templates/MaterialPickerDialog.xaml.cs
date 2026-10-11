using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

public partial class MaterialPickerDialog : Window
{
    private readonly List<MaterialRow> allMaterials;

    public MaterialPickerDialog(IEnumerable<MaterialRow> materials)
    {
        allMaterials = (materials ?? Enumerable.Empty<MaterialRow>()).ToList();
        InitializeComponent();

        // Cultura de los bindings = cultura del sistema (es-CL): "1,5" y precios con coma.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        MaterialsGrid.ItemsSource = allMaterials;
        if (MaterialsGrid.Items.Count > 0)
        {
            MaterialsGrid.SelectedIndex = 0;
        }

        // Enter en la grilla agrega (no navega como Excel) y Escape cancela: handler antes
        // de activar DataGridExcelNavigation, que se tragaría ambas teclas.
        MaterialsGrid.PreviewKeyDown += MaterialsGrid_PreviewKeyDown;
        DataGridExcelNavigation.SetEnable(MaterialsGrid, true);

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

        // Primer resultado preseleccionado: teclear → Enter agrega sin tocar el ratón.
        if (MaterialsGrid.Items.Count > 0)
        {
            MaterialsGrid.SelectedIndex = 0;
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // ↓ desde la búsqueda entra a la grilla (ya con el primer resultado seleccionado).
        if (e.Key == Key.Down && MaterialsGrid.Items.Count > 0)
        {
            if (MaterialsGrid.SelectedIndex < 0)
            {
                MaterialsGrid.SelectedIndex = 0;
            }

            MaterialsGrid.Focus();
            e.Handled = true;
        }
    }

    private void MaterialsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
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

        // Enter con el cursor recién llegado a la grilla (sin click): toma el primer filtro.
        if (SelectedMaterials.Count == 0 && MaterialsGrid.Items.Count > 0)
        {
            SelectedMaterials = MaterialsGrid.Items.Cast<MaterialRow>().Take(1).ToList();
        }

        if (SelectedMaterials.Count > 0)
        {
            DialogResult = true;
        }
    }
}
