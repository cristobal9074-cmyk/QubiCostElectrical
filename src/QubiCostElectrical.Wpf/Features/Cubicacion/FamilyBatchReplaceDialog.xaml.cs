using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed record FamilyRowSuggestionItem(string Codigo, string Descripcion, string PrecioDisplay, PriceBankEntry Entry);

public partial class FamilyBatchReplaceDialog : Window
{
    private readonly BudgetProjectSession session;
    private readonly IPriceBankStore priceBankStore;
    private readonly WpfUndoService undo;
    private readonly IFamilyBatchReplaceService batchService;
    private readonly IFamilyConversionPresetStore presetStore;

    private readonly ObservableCollection<FamilyItemMapping> displayedMappings = new();
    private FamilyItemMapping? selectedMapping;
    private FamilyConversionPreset? currentPreset;
    private bool isInitialized;
    private bool suppressRowSearch;

    public FamilyBatchReplaceResult? Result { get; private set; }

    public FamilyBatchReplaceDialog(
        BudgetProjectSession session,
        IPriceBankStore priceBankStore,
        WpfUndoService undo,
        IFamilyBatchReplaceService? batchService = null,
        IFamilyConversionPresetStore? presetStore = null)
    {
        this.session = session;
        this.priceBankStore = priceBankStore;
        this.undo = undo;
        this.batchService = batchService ?? new FamilyBatchReplaceService();
        this.presetStore = presetStore ?? new FamilyConversionPresetStore();

        InitializeComponent();

        MappingGrid.ItemsSource = displayedMappings;

        isInitialized = true;
        LoadPresets();
        ScanAndDisplayItems();
        UpdateCockpit();
    }

    public static FamilyBatchReplaceResult? Execute(
        Window? owner,
        BudgetProjectSession session,
        IPriceBankStore priceBankStore,
        WpfUndoService undo,
        string? initialSourcePrefix = null,
        string? initialTargetPrefix = null)
    {
        var dlg = new FamilyBatchReplaceDialog(session, priceBankStore, undo);
        if (owner != null) dlg.Owner = owner;

        if (!string.IsNullOrWhiteSpace(initialSourcePrefix))
        {
            dlg.SourcePrefixBox.Text = initialSourcePrefix;
            dlg.FilterBox.Text = initialSourcePrefix;
            dlg.ScanAndDisplayItems(initialSourcePrefix);
        }
        if (!string.IsNullOrWhiteSpace(initialTargetPrefix))
        {
            dlg.TargetPrefixBox.Text = initialTargetPrefix;
            dlg.ApplyAutoMapping(dlg.SourcePrefixBox.Text, initialTargetPrefix);
        }

        if (dlg.ShowDialog() == true)
            return dlg.Result;
        return null;
    }

    private void LoadPresets()
    {
        var presets = presetStore.GetAllPresets();
        PresetComboBox.ItemsSource = presets;
        if (presets.Count > 0)
        {
            PresetComboBox.SelectedIndex = 0;
        }
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized) return;

        if (PresetComboBox.SelectedItem is FamilyConversionPreset preset)
        {
            currentPreset = preset;
            SourcePrefixBox.Text = preset.SourcePrefix;
            TargetPrefixBox.Text = preset.TargetPrefix;
            FilterBox.Text = preset.SourcePrefix;
            DeletePresetBtn.IsEnabled = !preset.IsFactory;

            // Auto-filtrar y auto-mapear si hay prefijo
            ScanAndDisplayItems(preset.SourcePrefix);
            ApplyAutoMapping(preset.SourcePrefix, preset.TargetPrefix, preset.ItemOverrides);
        }
    }

    private void ScanAndDisplayItems(string? pattern = null)
    {
        string query = pattern ?? FilterBox.Text?.Trim() ?? string.Empty;
        var scanned = batchService.ScanMatchingMaterials(session, query, category: null, scanCubicacionOnly: true);

        displayedMappings.Clear();
        foreach (var item in scanned)
        {
            displayedMappings.Add(item);
        }

        UpdateCockpit();
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        ScanAndDisplayItems();
    }

    private void OnFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ScanAndDisplayItems();
        }
    }

    private void OnAutoMapClick(object sender, RoutedEventArgs e)
    {
        string srcPrefix = SourcePrefixBox.Text?.Trim() ?? string.Empty;
        string tgtPrefix = TargetPrefixBox.Text?.Trim() ?? string.Empty;

        ApplyAutoMapping(srcPrefix, tgtPrefix, currentPreset?.ItemOverrides);
    }

    private void ApplyAutoMapping(string srcPrefix, string tgtPrefix, IReadOnlyDictionary<string, string>? overrides = null)
    {
        if (displayedMappings.Count == 0) return;

        decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
        batchService.AutoMapWithPriceBank(displayedMappings, srcPrefix, tgtPrefix, priceBankStore, valorUf, overrides, session);

        MappingGrid.Items.Refresh();
        UpdateCockpit();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var m in displayedMappings)
            m.IsSelected = true;
        MappingGrid.Items.Refresh();
        UpdateCockpit();
    }

    private void OnDeselectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var m in displayedMappings)
            m.IsSelected = false;
        MappingGrid.Items.Refresh();
        UpdateCockpit();
    }

    private void OnGridCellCheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateCockpit();
    }

    private void OnIncludePriceBankCheckChanged(object sender, RoutedEventArgs e)
    {
        if (isInitialized)
            OnRowSearchTextChanged(RowSearchBox, null!);
    }

    private void OnTargetCodeCellLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is FamilyItemMapping mapping)
        {
            string newCode = tb.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(newCode))
            {
                LookupAndAssignCode(mapping, newCode);
            }
        }
    }

    private void OnMappingGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MappingGrid.SelectedItem is FamilyItemMapping mapping)
        {
            selectedMapping = mapping;
            SelectedRowInfoText.Text = $"Fila: {mapping.SourceCodigo} ({mapping.SourceDescripcion})";
            RowSearchBox.Text = "";
            RowSearchPopup.IsOpen = false;
        }
        else
        {
            selectedMapping = null;
            SelectedRowInfoText.Text = "(Seleccione una fila en la tabla)";
        }
    }

    private void OnRowSearchPreviewKeyDown(object sender, KeyEventArgs e) =>
        SuggestionListKeyboard.Handle(e, RowSearchSuggestionsList, RowSearchPopup);

    private void OnRowSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressRowSearch || !isInitialized || selectedMapping == null)
            return;

        string query = RowSearchBox.Text?.Trim() ?? "";
        bool listAll = query.Length == 0;

        var matches = new List<FamilyRowSuggestionItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Buscar en los materiales de la Cubicación del proyecto (prioridad estricta)
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
            var entry = new PriceBankEntry
            {
                Codigo = code,
                Descripcion = c.Descripcion ?? "",
                Unidad = c.Unidad ?? "",
                Categoria = c.Categoria ?? "",
                MatUf = c.MatUf,
                MoUf = c.MoUf,
                MonedaPrecio = "CLP",
                OrigenPrecio = "Cubicación",
                ProyectoOrigen = session.ProjectInfo?.Nombre ?? "",
                MatClp = c.PrecioUnitClp,
                MoClp = c.MoClp
            };

            matches.Add(new FamilyRowSuggestionItem(
                code,
                c.Descripcion ?? "",
                $"$ {c.PrecioUnitClp:N0} / {c.Unidad} (En Cubicación)",
                entry
            ));
        }

        // 2. Si el usuario activó "Buscar también en Banco de Precios", agregar resultados externos
        string? bankNotice = null;
        if (!listAll && IncludePriceBankCheck?.IsChecked == true)
        {
            try
            {
                decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
                if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

                var bankResults = priceBankStore.Search(query);
                foreach (var b in bankResults)
                {
                    if (seenCodes.Contains(b.Codigo)) continue;
                    seenCodes.Add(b.Codigo);

                    decimal clp = b.MatClp > 0m ? b.MatClp : b.MatUf * valorUf;
                    matches.Add(new FamilyRowSuggestionItem(
                        b.Codigo,
                        b.Descripcion,
                        $"$ {clp:N0} / {b.Unidad} (Banco)",
                        b
                    ));

                    if (matches.Count >= 30) break;
                }
            }
            catch
            {
                bankNotice = "No se pudo consultar el banco de precios. La fila quedó como estaba.";
            }
        }

        RowSearchStatus.Text = bankNotice
            ?? (matches.Count == 0 ? "Sin coincidencias." : "");
        RowSearchSuggestionsList.ItemsSource = matches;
        RowSearchPopup.IsOpen = matches.Count > 0;
    }

    private void OnRowSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (selectedMapping == null || RowSearchSuggestionsList.SelectedItem is not FamilyRowSuggestionItem item)
            return;

        var b = item.Entry;
        decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
        if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

        selectedMapping.TargetCodigo = b.Codigo;
        selectedMapping.TargetDescripcion = b.Descripcion;
        selectedMapping.TargetUnidad = !string.IsNullOrWhiteSpace(b.Unidad) ? b.Unidad : selectedMapping.SourceUnidad;
        selectedMapping.TargetCategoria = b.Categoria;
        selectedMapping.NuevoPrecioClp = b.MatClp > 0m ? b.MatClp : b.MatUf * valorUf;
        selectedMapping.TargetMatUf = b.MatUf;
        selectedMapping.TargetMoClp = b.MoClp;
        selectedMapping.TargetMoUf = b.MoUf;
        selectedMapping.TargetProveedor = b.Proveedor ?? "";
        selectedMapping.Status = b.OrigenPrecio == "Cubicación" ? FamilyMappingStatus.InCubicacion : FamilyMappingStatus.Manual;

        RowSearchPopup.IsOpen = false;
        suppressRowSearch = true;
        RowSearchBox.Text = $"{b.Codigo} · {b.Descripcion}";
        suppressRowSearch = false;
        RowSearchStatus.Text = "";
        MappingGrid.Items.Refresh();
        UpdateCockpit();
    }

    private void LookupAndAssignCode(FamilyItemMapping mapping, string code)
    {
        decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
        if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

        mapping.TargetCodigo = code;

        // 1. Buscar en la Cubicación del proyecto
        var cubMatch = session.Cubicacion.FirstOrDefault(c => string.Equals(c.Codigo?.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (cubMatch != null)
        {
            mapping.TargetDescripcion = cubMatch.Descripcion ?? "";
            mapping.TargetUnidad = !string.IsNullOrWhiteSpace(cubMatch.Unidad) ? cubMatch.Unidad : mapping.SourceUnidad;
            mapping.TargetCategoria = cubMatch.Categoria ?? "";
            mapping.NuevoPrecioClp = cubMatch.PrecioUnitClp;
            mapping.TargetMatUf = cubMatch.MatUf;
            mapping.TargetMoClp = cubMatch.MoClp;
            mapping.TargetMoUf = cubMatch.MoUf;
            mapping.TargetProveedor = "";
            mapping.Status = FamilyMappingStatus.InCubicacion;

            MappingGrid.Items.Refresh();
            UpdateCockpit();
            return;
        }

        // 2. Si no está en Cubicación, buscar en Banco de Precios
        PriceBankEntry? entry = null;
        try
        {
            entry = priceBankStore.GetByCodigo(code) ??
                    priceBankStore.Search(code).FirstOrDefault(x => string.Equals(x.Codigo, code, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            mapping.Status = FamilyMappingStatus.NotFound;
            RowSearchStatus.Text = "No se pudo consultar el banco de precios. El código quedó escrito, sin precio del banco.";
            MappingGrid.Items.Refresh();
            UpdateCockpit();
            return;
        }

        if (entry != null)
        {
            mapping.TargetDescripcion = entry.Descripcion;
            mapping.TargetUnidad = !string.IsNullOrWhiteSpace(entry.Unidad) ? entry.Unidad : mapping.SourceUnidad;
            mapping.TargetCategoria = entry.Categoria;
            mapping.NuevoPrecioClp = entry.MatClp > 0m ? entry.MatClp : entry.MatUf * valorUf;
            mapping.TargetMatUf = entry.MatUf;
            mapping.TargetMoClp = entry.MoClp;
            mapping.TargetMoUf = entry.MoUf;
            mapping.TargetProveedor = entry.Proveedor ?? "";
            mapping.Status = FamilyMappingStatus.InBank;
        }
        else
        {
            mapping.Status = FamilyMappingStatus.NotFound;
        }

        MappingGrid.Items.Refresh();
        UpdateCockpit();
    }

    private void OnScopeOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!isInitialized) return;
        UpdateCockpit();
    }

    private void UpdateCockpit()
    {
        if (!isInitialized || batchService == null || session == null || CockpitPartidasText == null ||
            CockpitUnidadesText == null || CockpitCostosText == null || CockpitDeltaText == null ||
            ExecuteButton == null || CreateVariantButton == null)
            return;

        bool inCubicacion = ReplaceCubicacionCheck?.IsChecked == true;
        bool inApu = ReplaceApuCheck?.IsChecked == true;

        var preview = batchService.CalculatePreview(session, displayedMappings, inCubicacion, inApu);

        CockpitPartidasText.Text = $"{preview.PartidasAfectadasCount} Partidas afectadas";
        decimal unidades = displayedMappings.Where(m => m.IsSelected).Sum(m => m.CantidadTotal);
        CockpitUnidadesText.Text = $"{unidades:N1} Unidades totales";
        CockpitCostosText.Text = $"Costo Anterior: $ {preview.CostoTotalAnteriorClp:N0} ➔ Nuevo: $ {preview.CostoTotalNuevoClp:N0}";

        string sign = preview.DeltaTotalClp > 0 ? "+ " : "";
        CockpitDeltaText.Text = $"Variación: {sign}$ {preview.DeltaTotalClp:N0} CLP ({preview.VariacionPorcentual:N1}%)";

        if (preview.DeltaTotalClp < 0)
        {
            CockpitDeltaText.Foreground = TryFindResource("Brush.Green") as Brush ?? Brushes.Green;
        }
        else if (preview.DeltaTotalClp > 0)
        {
            CockpitDeltaText.Foreground = TryFindResource("Brush.Amber") as Brush ?? Brushes.Goldenrod;
        }
        else
        {
            CockpitDeltaText.Foreground = TryFindResource("Brush.Ink") as Brush ?? Brushes.Black;
        }

        int activeItems = displayedMappings.Count(m => m.IsSelected &&
                                                      !string.IsNullOrWhiteSpace(m.TargetCodigo) &&
                                                      (m.Status == FamilyMappingStatus.InBank || m.Status == FamilyMappingStatus.Manual));

        ExecuteButton.IsEnabled = activeItems > 0 && (inCubicacion || inApu);
        CreateVariantButton.IsEnabled = ExecuteButton.IsEnabled;
    }

    private void OnSavePresetClick(object sender, RoutedEventArgs e)
    {
        string? name = WpfDialogService.PromptText(
            "Guardar Perfil de Conversión",
            "Nombre del perfil (ej: Bticino Magic a Sinthesi)",
            currentPreset?.Name ?? "Mi Regla Personalizada");

        if (string.IsNullOrWhiteSpace(name)) return;

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in displayedMappings.Where(m => !string.IsNullOrWhiteSpace(m.TargetCodigo)))
        {
            overrides[m.SourceCodigo] = m.TargetCodigo;
        }

        var newPreset = new FamilyConversionPreset
        {
            Name = name.Trim(),
            Description = $"Regla personalizada: {SourcePrefixBox.Text} ➔ {TargetPrefixBox.Text}",
            SourcePrefix = SourcePrefixBox.Text?.Trim() ?? "",
            TargetPrefix = TargetPrefixBox.Text?.Trim() ?? "",
            ItemOverrides = overrides,
            IsFactory = false
        };

        presetStore.SavePreset(newPreset);
        LoadPresets();

        // Seleccionar el nuevo
        var match = ((IReadOnlyList<FamilyConversionPreset>)PresetComboBox.ItemsSource)
            .FirstOrDefault(p => string.Equals(p.Id, newPreset.Id, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            PresetComboBox.SelectedItem = match;
        }

        WpfDialogService.Info("Perfil Guardado", $"El perfil «{name}» fue guardado exitosamente.");
    }

    private void OnDeletePresetClick(object sender, RoutedEventArgs e)
    {
        if (currentPreset == null || currentPreset.IsFactory) return;

        if (WpfDialogService.Confirm("Eliminar Perfil", $"¿Desea eliminar el perfil «{currentPreset.Name}»?", isWarning: true))
        {
            presetStore.DeletePreset(currentPreset.Id);
            LoadPresets();
        }
    }

    private void OnExecuteClick(object sender, RoutedEventArgs e)
    {
        bool inCubicacion = ReplaceCubicacionCheck.IsChecked == true;
        bool inApu = ReplaceApuCheck.IsChecked == true;

        Result = batchService.ExecuteReplacement(
            session,
            (s, desc) => undo.PushUndoPoint(s, desc),
            displayedMappings,
            replaceCubicacion: inCubicacion,
            replaceApu: inApu);

        DialogResult = true;
        Close();
    }

    private void OnCreateVariantClick(object sender, RoutedEventArgs e)
    {
        string defaultName = !string.IsNullOrWhiteSpace(TargetPrefixBox.Text)
            ? $"Variante {TargetPrefixBox.Text.Trim('/')}"
            : "Variante Ingeniería de Valor";

        string? variantName = WpfDialogService.PromptText(
            "Crear Oferta Variante / Alternativa",
            "Nombre para la nueva versión variante:",
            defaultName);

        if (string.IsNullOrWhiteSpace(variantName))
            return;

        // 1. Si no hay versiones previas en el proyecto, congelar el estado original como V1 Base
        if (session.Versiones.Count == 0)
        {
            var baseSnapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
            var baseVersion = new BudgetVersionRow
            {
                Codigo = "V1",
                Nombre = "Presupuesto Base Original",
                Estado = "Base",
                CreadoPor = session.ProjectInfo.UsuarioNombre,
                FechaCreacion = AppClock.Now,
                FechaModificacion = AppClock.Now,
                Snapshot = baseSnapshot
            };
            session.Versiones.Add(baseVersion);
        }

        // 2. Ejecutar la sustitución de la familia
        bool inCubicacion = ReplaceCubicacionCheck.IsChecked == true;
        bool inApu = ReplaceApuCheck.IsChecked == true;

        Result = batchService.ExecuteReplacement(
            session,
            (s, desc) => undo.PushUndoPoint(s, desc),
            displayedMappings,
            replaceCubicacion: inCubicacion,
            replaceApu: inApu);

        // 3. Crear la nueva versión variante
        int nextNum = session.Versiones.Count + 1;
        var variantSnapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
        var variantVersion = new BudgetVersionRow
        {
            Codigo = $"V{nextNum}",
            Nombre = variantName.Trim(),
            Estado = "Variante",
            CreadoPor = session.ProjectInfo.UsuarioNombre,
            FechaCreacion = AppClock.Now,
            FechaModificacion = AppClock.Now,
            Snapshot = variantSnapshot
        };

        session.Versiones.Add(variantVersion);
        session.ActiveVersionId = variantVersion.Id;
        session.MarkDirty(ProjectDirtyScope.Versiones);

        WpfDialogService.Info(
            "Oferta Variante Creada",
            $"Se creó la versión «{variantVersion.Codigo} — {variantVersion.Nombre}» con {Result.ItemsReemplazados} materiales actualizados.\nPuede alternar entre la oferta base y esta variante en el módulo Versiones.");

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
