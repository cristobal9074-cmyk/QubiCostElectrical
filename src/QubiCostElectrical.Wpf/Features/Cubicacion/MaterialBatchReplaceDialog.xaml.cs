using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed record MaterialComboItem(MaterialRow Material, string DisplayText);

public sealed record TargetSuggestionItem(string Codigo, string Descripcion, string PrecioDisplay, MaterialRow Material);

public partial class MaterialBatchReplaceDialog : Window
{
    private readonly BudgetProjectSession session;
    private readonly IPriceBankStore priceBankStore;
    private readonly WpfUndoService undo;
    private readonly IMaterialBatchReplaceService batchService;
    private readonly List<MaterialRow> usedMaterials;
    private MaterialRow? selectedSourceMaterial;
    private MaterialRow? selectedTargetMaterial;

    public MaterialBatchReplaceResult? Result { get; private set; }

    public MaterialBatchReplaceDialog(BudgetProjectSession session, IPriceBankStore priceBankStore, WpfUndoService undo)
    {
        InitializeComponent();
        this.session = session;
        this.priceBankStore = priceBankStore;
        this.undo = undo;
        this.batchService = new MaterialBatchReplaceService();

        usedMaterials = batchService.GetUsedMaterials(session).ToList();

        var comboItems = usedMaterials.Select(m => new MaterialComboItem(
            m,
            $"{(string.IsNullOrWhiteSpace(m.Codigo) ? "" : m.Codigo + " · ")}{m.Descripcion}"
        )).ToList();

        SourceMaterialCombo.ItemsSource = comboItems;
        if (comboItems.Count > 0)
            SourceMaterialCombo.SelectedIndex = 0;
    }

    public static MaterialBatchReplaceResult? Execute(Window? owner, BudgetProjectSession session, IPriceBankStore priceBankStore, WpfUndoService undo)
    {
        var dlg = new MaterialBatchReplaceDialog(session, priceBankStore, undo);
        if (owner != null) dlg.Owner = owner;
        if (dlg.ShowDialog() == true)
            return dlg.Result;
        return null;
    }

    private void OnSourceMaterialSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceMaterialCombo.SelectedItem is MaterialComboItem item)
        {
            selectedSourceMaterial = item.Material;
            SourceDetailsText.Text = $"Precio actual: $ {selectedSourceMaterial.MatClp:N0} CLP ({selectedSourceMaterial.Unidad})";
            RefreshPreview();
        }
    }

    private void OnTargetSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = TargetMaterialSearchBox.Text?.Trim() ?? "";
        if (query.Length < 2)
        {
            TargetSearchPopup.IsOpen = false;
            return;
        }

        var suggestions = new List<TargetSuggestionItem>();

        // 1. Buscar en Banco de Precios
        try
        {
            var bankMatches = priceBankStore.Search(query).Take(15);
            foreach (var b in bankMatches)
            {
                decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
                if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;
                decimal clp = b.MatClp > 0m ? b.MatClp : b.MatUf * valorUf;

                var row = new MaterialRow
                {
                    Codigo = b.Codigo,
                    Descripcion = b.Descripcion,
                    Unidad = b.Unidad,
                    Categoria = b.Categoria,
                    MatClp = clp,
                    MoClp = b.MoClp,
                    MatUf = b.MatUf,
                    MoUf = b.MoUf,
                    Proveedor = b.Proveedor
                };

                suggestions.Add(new TargetSuggestionItem(
                    b.Codigo,
                    b.Descripcion,
                    $"$ {clp:N0} ({b.Unidad})",
                    row
                ));
            }
        }
        catch { }

        // 2. Buscar en Materiales del Proyecto
        var projectMatches = session.Materiales
            .Where(m => (m.Codigo?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                        (m.Descripcion?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .Take(10);

        foreach (var m in projectMatches)
        {
            if (!suggestions.Any(s => string.Equals(s.Codigo, m.Codigo, StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add(new TargetSuggestionItem(
                    m.Codigo,
                    m.Descripcion,
                    $"$ {m.MatClp:N0} ({m.Unidad})",
                    m
                ));
            }
        }

        TargetSuggestionsList.ItemsSource = suggestions;
        TargetSearchPopup.IsOpen = suggestions.Count > 0;
    }

    private void OnTargetSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TargetSuggestionsList.SelectedItem is TargetSuggestionItem item)
        {
            selectedTargetMaterial = item.Material;
            TargetMaterialSearchBox.Text = $"{item.Codigo} · {item.Descripcion}";
            TargetDetailsText.Text = $"Nuevo precio: $ {selectedTargetMaterial.MatClp:N0} CLP ({selectedTargetMaterial.Unidad})";
            TargetSearchPopup.IsOpen = false;
            RefreshPreview();
        }
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (selectedSourceMaterial == null || selectedTargetMaterial == null)
        {
            PreviewGrid.ItemsSource = null;
            ExecuteButton.IsEnabled = false;
            SummaryPartidasCountText.Text = "0 partidas afectadas";
            SummaryTotalDeltaText.Text = "Variación: $ 0 CLP";
            return;
        }

        string sourceKey = !string.IsNullOrWhiteSpace(selectedSourceMaterial.Codigo)
            ? selectedSourceMaterial.Codigo
            : selectedSourceMaterial.Descripcion;

        var previews = batchService.PreviewReplacements(session, sourceKey, selectedTargetMaterial);
        PreviewGrid.ItemsSource = previews;

        int count = previews.Select(p => p.ItemId).Distinct().Count();
        decimal totalDelta = previews.Sum(p => p.DeltaClp);

        SummaryPartidasCountText.Text = $"{count} partidas afectadas ({previews.Count} líneas)";
        SummaryTotalDeltaText.Text = $"Variación Total: {(totalDelta >= 0 ? "+ " : "")}$ {totalDelta:N0} CLP";
        ExecuteButton.IsEnabled = previews.Count > 0;
    }

    private void OnExecuteReplaceClick(object sender, RoutedEventArgs e)
    {
        if (selectedSourceMaterial == null || selectedTargetMaterial == null)
            return;

        string sourceKey = !string.IsNullOrWhiteSpace(selectedSourceMaterial.Codigo)
            ? selectedSourceMaterial.Codigo
            : selectedSourceMaterial.Descripcion;

        bool inCubicacion = ReplaceCubicacionCheck.IsChecked == true;
        bool inApu = ReplaceApuCheck.IsChecked == true;

        Result = batchService.ExecuteReplacement(
            session,
            (s, desc) => undo.PushUndoPoint(s, desc),
            sourceKey,
            selectedTargetMaterial,
            replaceInCubicacion: inCubicacion,
            replaceInApu: inApu);

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
