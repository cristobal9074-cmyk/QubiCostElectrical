using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

public partial class ApuTemplatePickerDialog : Window
{
    private readonly IApuTemplateStore store;
    private List<ApuTemplateItemVm> allTemplates = new();

    public ApuTemplatePickerDialog(IApuTemplateStore store)
    {
        this.store = store;
        InitializeComponent();
        Loaded += (_, _) => LoadAll();
    }

    public ApuTemplateRow? SelectedTemplate { get; private set; }
    public bool Reemplazar => RbReemplazar.IsChecked == true;

    private void LoadAll()
    {
        // ApuTemplateItemVm calcula RecursoCount deserializando JsonRecursos — el ListBox
        // de este diálogo lo usa para mostrar "N rec." por plantilla (antes bindeaba
        // directo a ApuTemplateRow, que no tiene esa propiedad, y mostraba vacío).
        allTemplates = store.GetAll().Select(r => new ApuTemplateItemVm(r)).ToList();
        TemplateList.ItemsSource = allTemplates;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterBox.Text.Trim();
        TemplateList.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? allTemplates
            : allTemplates.Where(t =>
                t.Nombre.Contains(text, System.StringComparison.OrdinalIgnoreCase) ||
                t.Categoria.Contains(text, System.StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = TemplateList.SelectedItem as ApuTemplateItemVm;
        BtnAplicar.IsEnabled = sel != null;
        LoadPreview(sel?.Source);
    }

    private void LoadPreview(ApuTemplateRow? template)
    {
        PreviewWarningTextBlock.Visibility = Visibility.Collapsed;
        PreviewWarningTextBlock.Text = "";

        if (template == null || string.IsNullOrWhiteSpace(template.JsonRecursos))
        {
            PreviewGrid.ItemsSource = null;
            return;
        }
        try
        {
            var recursos = JsonSerializer.Deserialize<List<ApuRecursoRow>>(template.JsonRecursos,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            PreviewGrid.ItemsSource = recursos;
        }
        catch (Exception ex)
        {
            PreviewGrid.ItemsSource = null;
            PreviewWarningTextBlock.Text = "No se pudo leer la vista previa de la plantilla. Se mantiene la plantilla, pero sin previsualización.";
            PreviewWarningTextBlock.Visibility = Visibility.Visible;
            WpfAppHost.Logger?.Warning("ApuTemplatePicker", "No se pudo deserializar la vista previa de una plantilla APU.", ex);
        }
    }

    private void BtnAplicar_Click(object sender, RoutedEventArgs e)
    {
        SelectedTemplate = (TemplateList.SelectedItem as ApuTemplateItemVm)?.Source;
        if (SelectedTemplate == null) return;
        DialogResult = true;
    }
}
