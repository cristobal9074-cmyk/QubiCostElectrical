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
    private bool isInitialized;
    private bool suppressTargetSearch;

    public MaterialBatchReplaceResult? Result { get; private set; }

    public MaterialBatchReplaceDialog(BudgetProjectSession session, IPriceBankStore priceBankStore, WpfUndoService undo)
    {
        this.session = session;
        this.priceBankStore = priceBankStore;
        this.undo = undo;
        this.batchService = new MaterialBatchReplaceService();

        InitializeComponent();

        usedMaterials = batchService.GetUsedMaterials(session).ToList();

        var comboItems = usedMaterials.Select(m => new MaterialComboItem(
            m,
            $"{(string.IsNullOrWhiteSpace(m.Codigo) ? "" : m.Codigo + " · ")}{m.Descripcion}"
        )).ToList();

        SourceMaterialCombo.ItemsSource = comboItems;
        isInitialized = true;

        if (comboItems.Count > 0)
            SourceMaterialCombo.SelectedIndex = 0;
        else
            RefreshPreview();
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
        if (!isInitialized) return;

        if (SourceMaterialCombo.SelectedItem is MaterialComboItem item)
        {
            selectedSourceMaterial = item.Material;
            SourceDetailsText.Text = $"Precio actual: $ {selectedSourceMaterial.MatClp:N0} CLP ({selectedSourceMaterial.Unidad})";
            RefreshPreview();
        }
    }

    private void OnTargetIncludeBankCheckChanged(object sender, RoutedEventArgs e)
    {
        if (isInitialized)
            OnTargetSearchTextChanged(TargetMaterialSearchBox, null!);
    }

    private void OnTargetSearchPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) =>
        SuggestionListKeyboard.Handle(e, TargetSuggestionsList, TargetSearchPopup);

    private void OnTargetSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressTargetSearch || !isInitialized)
            return;

        string query = TargetMaterialSearchBox.Text?.Trim() ?? "";
        if (selectedTargetMaterial != null)
        {
            string chosen = $"{selectedTargetMaterial.Codigo} · {selectedTargetMaterial.Descripcion}";
            if (!string.Equals(query, chosen, StringComparison.OrdinalIgnoreCase))
            {
                selectedTargetMaterial = null;
                TargetDetailsText.Text = "";
                RefreshPreview();
            }
        }

        var suggestions = new List<TargetSuggestionItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool listAll = query.Length == 0;

        // 1. Buscar en Materiales de la Cubicación del proyecto (prioridad estricta)
        var cubMatches = session.Cubicacion
            .Where(c => !string.IsNullOrWhiteSpace(c.Codigo) || !string.IsNullOrWhiteSpace(c.Descripcion))
            .Where(c => listAll ||
                        (c.Codigo?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                        (c.Descripcion?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .GroupBy(c => !string.IsNullOrWhiteSpace(c.Codigo) ? c.Codigo.Trim() : c.Descripcion.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(30);

        foreach (var c in cubMatches)
        {
            string code = c.Codigo ?? "";
            seenCodes.Add(code);
            var row = new MaterialRow
            {
                Codigo = code,
                Descripcion = c.Descripcion ?? "",
                Unidad = c.Unidad ?? "",
                Categoria = c.Categoria ?? "",
                MatClp = c.PrecioUnitClp,
                MatUf = c.MatUf,
                MoClp = c.MoClp,
                MoUf = c.MoUf,
                Proveedor = ""
            };

            suggestions.Add(new TargetSuggestionItem(
                code,
                c.Descripcion ?? "",
                $"$ {c.PrecioUnitClp:N0} ({c.Unidad}) [Cubicación]",
                row
            ));
        }

        // 2. Si el usuario activó "Buscar en Banco", buscar en el banco de precios central
        string? bankNotice = null;
        if (!listAll && TargetIncludeBankCheck?.IsChecked == true)
        {
            try
            {
                var bankMatches = priceBankStore.Search(query);
                foreach (var b in bankMatches)
                {
                    if (seenCodes.Contains(b.Codigo)) continue;
                    seenCodes.Add(b.Codigo);

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
                        $"$ {clp:N0} ({b.Unidad}) [Banco]",
                        row
                    ));

                    if (suggestions.Count >= 30) break;
                }
            }
            catch
            {
                bankNotice = "No se pudo consultar el banco de precios. La cubicación no cambió.";
            }
        }

        TargetSearchStatus.Text = bankNotice
            ?? (suggestions.Count == 0 ? "Sin coincidencias." : "");
        TargetSuggestionsList.ItemsSource = suggestions;
        TargetSearchPopup.IsOpen = suggestions.Count > 0;
    }

    private void OnTargetSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TargetSuggestionsList.SelectedItem is TargetSuggestionItem item)
        {
            selectedTargetMaterial = item.Material;
            suppressTargetSearch = true;
            TargetMaterialSearchBox.Text = $"{item.Codigo} · {item.Descripcion}";
            suppressTargetSearch = false;
            TargetSearchStatus.Text = "";
            TargetDetailsText.Text = $"Nuevo precio: $ {selectedTargetMaterial.MatClp:N0} CLP ({selectedTargetMaterial.Unidad})";
            TargetSearchPopup.IsOpen = false;
            RefreshPreview();
        }
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!isInitialized) return;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!isInitialized || PreviewGrid == null || ExecuteButton == null ||
            SummaryPartidasCountText == null || SummaryTotalDeltaText == null)
            return;

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
