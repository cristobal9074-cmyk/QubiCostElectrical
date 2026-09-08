using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Quotes;

public sealed partial class QuotesViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public QuotesViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Services.MaterialSearch, host.Services.PriceBankStore) { }

    public QuotesViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IMaterialSearchService materialSearch, IPriceBankStore store) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.materialSearch = materialSearch;
        this.store = store;

        refreshCommand = new RelayCommand(Refresh);
        clearFiltersCommand = new RelayCommand(ClearFilters);
        importQuoteCommand = new RelayCommand(ImportQuote);
        applyPriceCommand = new RelayCommand<QuoteComparisonRowVm>(ApplyPrice, r => r != null && !session.IsReadOnly);
        applyAllBestPricesCommand = new RelayCommand(ApplyAllBestPrices, () => FilteredComparisonRows.Any() && !session.IsReadOnly);
        applySelectionToBankCommand = new RelayCommand(ApplySelectionToBank, CanApplySelectionToBank);

        providerSummaryView = CollectionViewSource.GetDefaultView(providerSummaries);
        providerSummaryView.Filter = FilterProvider;
        providerSummaryView.SortDescriptions.Add(new SortDescription(nameof(QuoteProviderSummaryVm.Materiales), ListSortDirection.Descending));
        providerSummaryView.SortDescriptions.Add(new SortDescription(nameof(QuoteProviderSummaryVm.Proveedor), ListSortDirection.Ascending));

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        UpdateProjectSummary();

    public ObservableCollection<QuoteProviderSummaryVm> ProviderSummaries => providerSummaries;
    public ICollectionView FilteredProviderSummaries => providerSummaryView;
    public ObservableCollection<QuoteEntryVm> ProviderEntries => providerEntries;
    public ObservableCollection<QuoteComparisonRowVm> ComparisonRows => comparisonRows;
    public IEnumerable<QuoteComparisonRowVm> FilteredComparisonRows => comparisonRows;
    public ObservableCollection<string> CategoryOptions => categoryOptions;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            providerSummaryView.Refresh();
            RefreshSelectedProvider();
            UpdateSummary();
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (!SetProperty(ref selectedCategory, value))
                return;
            RebuildProvidersInSelectedCategory();
            providerSummaryView.Refresh();
            RefreshSelectedProvider();
            UpdateSummary();
        }
    }

    public QuoteProviderSummaryVm? SelectedProvider
    {
        get => selectedProvider;
        set
        {
            if (!SetProperty(ref selectedProvider, value))
                return;
            RefreshSelectedProvider();
            UpdateSummary();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string ProjectSummary
    {
        get => projectSummary;
        private set => SetProperty(ref projectSummary, value);
    }

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }

    public QuoteComparisonRowVm? SelectedComparisonRow
    {
        get => selectedComparisonRow;
        set
        {
            if (!SetProperty(ref selectedComparisonRow, value))
                return;
            applyPriceCommand.RaiseCanExecuteChanged();
            applySelectionToBankCommand.RaiseCanExecuteChanged();
        }
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand ClearFiltersCommand => clearFiltersCommand;
    public ICommand ImportQuoteCommand => importQuoteCommand;
    public ICommand ApplyPriceCommand => applyPriceCommand;
    public ICommand ApplyAllBestPricesCommand => applyAllBestPricesCommand;
    public ICommand ApplySelectionToBankCommand => applySelectionToBankCommand;

    public bool IsLoading { get; private set; }

    public void Refresh()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(IsLoading));
        try
        {
            BuildProviderSummaries();
            UpdateProjectSummary();

            if (SelectedProvider == null)
                SelectedProvider = providerSummaries.FirstOrDefault();

            RefreshSelectedProvider();
            UpdateSummary();
            applyAllBestPricesCommand.RaiseCanExecuteChanged();
            applyPriceCommand.RaiseCanExecuteChanged();
            applySelectionToBankCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private void BuildProviderSummaries()
    {
        try
        {
            allEntries = store.GetAll()
                .Where(x => !string.IsNullOrWhiteSpace(x.Proveedor))
                .ToList();
        }
        catch (Exception ex)
        {
            allEntries = Array.Empty<PriceBankEntry>();
            StatusMessage = OperationFailureMessages.ToUserMessage(ex);
        }

        var summaries = allEntries
            .GroupBy(x => Normalize(x.Proveedor))
            .Select(g =>
            {
                var rows = g.ToList();
                var prices = rows.Select(x => x.MatUf + x.MoUf).Where(x => x > 0m).ToList();
                return new QuoteProviderSummaryVm
                {
                    Proveedor = rows.Select(x => x.Proveedor).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? g.Key,
                    Materiales = rows.Count,
                    Categorias = rows.Select(x => x.Categoria).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    PrecioPromedioUf = prices.Count == 0 ? 0m : prices.Average(),
                    PrecioMinUf = prices.Count == 0 ? 0m : prices.Min(),
                    PrecioMaxUf = prices.Count == 0 ? 0m : prices.Max(),
                    UltimaFechaPrecio = rows.Max(x => x.FechaPrecio)
                };
            })
            .OrderByDescending(x => x.Materiales)
            .ThenBy(x => x.Proveedor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        providerSummaries.Clear();
        foreach (var item in summaries)
            providerSummaries.Add(item);

        BuildCategoryOptions(allEntries);
        RebuildProvidersInSelectedCategory();
        providerSummaryView.Refresh();
        ShowEmptyState = providerSummaries.Count == 0;
    }

    private void RebuildProvidersInSelectedCategory()
    {
        providersInSelectedCategory.Clear();
        if (string.Equals(SelectedCategory, "Todas", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var entry in allEntries)
        {
            string categoria = string.IsNullOrWhiteSpace(entry.Categoria) ? "Sin categoría" : entry.Categoria.Trim();
            if (string.Equals(categoria, SelectedCategory, StringComparison.OrdinalIgnoreCase))
                providersInSelectedCategory.Add(Normalize(entry.Proveedor));
        }
    }

    private void BuildCategoryOptions(IEnumerable<PriceBankEntry> allEntries)
    {
        var values = allEntries
            .Select(x => string.IsNullOrWhiteSpace(x.Categoria) ? "Sin categoría" : x.Categoria.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        categoryOptions.Clear();
        categoryOptions.Add("Todas");
        foreach (var value in values)
            categoryOptions.Add(value);

        if (!categoryOptions.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
            SelectedCategory = "Todas";
    }

    private bool FilterProvider(object obj)
    {
        if (obj is not QuoteProviderSummaryVm provider)
            return false;

        string query = materialSearch.Normalize(SearchText);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string searchable = materialSearch.Normalize(string.Join(' ', new[]
            {
                provider.Proveedor,
                provider.Materiales.ToString(),
                provider.Categorias.ToString()
            }));

            if (!searchable.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.Equals(SelectedCategory, "Todas", StringComparison.OrdinalIgnoreCase))
        {
            if (!providersInSelectedCategory.Contains(Normalize(provider.Proveedor)))
                return false;
        }

        return true;
    }

    private void RefreshSelectedProvider()
    {
        providerEntries.Clear();
        comparisonRows.Clear();

        if (SelectedProvider == null)
        {
            ShowEmptyState = providerSummaries.Count == 0;
            return;
        }

        decimal valorUf = session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;
        var selected = allEntries
            .Where(x => string.Equals(Normalize(x.Proveedor), Normalize(SelectedProvider.Proveedor), StringComparison.OrdinalIgnoreCase))
            .Where(FilterEntry)
            .OrderBy(x => x.Categoria, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(x => new QuoteEntryVm(x, valorUf))
            .ToList();

        foreach (var item in selected)
            providerEntries.Add(item);

        BuildComparisonRows(allEntries, selected);
        ShowEmptyState = providerEntries.Count == 0;
        StatusMessage = providerEntries.Count == 0
            ? "El proveedor seleccionado no tiene cotizaciones visibles con estos filtros."
            : $"Proveedor {SelectedProvider.Proveedor} con {providerEntries.Count:N0} materiales.";
    }

    private bool FilterEntry(PriceBankEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = materialSearch.Normalize(SearchText);
            string searchable = materialSearch.Normalize(string.Join(' ', new[]
            {
                entry.Codigo,
                entry.Descripcion,
                entry.Categoria,
                entry.Marca,
                entry.OrigenPrecio
            }));

            if (!searchable.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.Equals(SelectedCategory, "Todas", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(string.IsNullOrWhiteSpace(entry.Categoria) ? "Sin categoría" : entry.Categoria.Trim(), SelectedCategory, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void BuildComparisonRows(IReadOnlyList<PriceBankEntry> all, IReadOnlyList<QuoteEntryVm> selected)
    {
        var byCode = all
            .GroupBy(x => Normalize(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var selectedByCode = selected
            .GroupBy(x => Normalize(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<QuoteComparisonRowVm>();

        decimal valorUf = session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;
        decimal EffectiveUf(PriceBankEntry e)
        {
            decimal uf = e.MatUf + e.MoUf;
            if (uf > 0m) return uf;
            decimal clp = e.MatClpConFallback + e.MoClpConFallback;
            return clp > 0m ? PriceConversion.ClpToUf(clp, valorUf) : 0m;
        }

        foreach (var (code, selectedEntry) in selectedByCode)
        {
            if (!byCode.TryGetValue(code, out var candidates) || candidates.Count == 0)
                continue;

            var validCandidates = candidates.Where(x => EffectiveUf(x) > 0m).ToList();
            if (validCandidates.Count == 0)
                validCandidates = candidates;

            var best = validCandidates
                .OrderBy(x => EffectiveUf(x))
                .First();

            decimal bestUf = EffectiveUf(best);
            decimal bestClp = (best.MatClpConFallback + best.MoClpConFallback) > 0m
                ? (best.MatClpConFallback + best.MoClpConFallback)
                : PriceConversion.UfToClp(bestUf, valorUf);

            rows.Add(new QuoteComparisonRowVm(
                selectedEntry.Codigo,
                selectedEntry.Descripcion,
                selectedEntry.Proveedor,
                selectedEntry.PrecioUf,
                best.Proveedor,
                bestUf,
                selectedEntry.Categoria,
                selectedEntry.PrecioClp,
                bestClp));
        }

        rows = rows
            .OrderByDescending(x => x.DeltaUf)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        comparisonRows.Clear();
        foreach (var item in rows)
            comparisonRows.Add(item);
    }

    private void UpdateSummary()
    {
        SummaryText = providerSummaries.Count == 0
            ? "0 proveedores"
            : $"{providerSummaries.Count:N0} proveedores · {providerEntries.Count:N0} materiales visibles · {comparisonRows.Count:N0} comparaciones";
    }

    private void UpdateProjectSummary()
    {
        ProjectSummary = workspace.HasOpenProject
            ? $"{session.ProjectInfo.Nombre} · banco activo"
            : "Sin proyecto abierto";
    }

    private void ClearFilters()
    {
        SearchText = "";
        SelectedCategory = "Todas";
        providerSummaryView.Refresh();
        RefreshSelectedProvider();
        UpdateSummary();
    }

    private void ImportQuote()
    {
        var file = WpfDialogService.PromptOpenExcel();
        if (file == null)
            return;

        try
        {
            var entries = PriceBankSeeder.ParseFile(file);
            if (entries.Count == 0)
            {
                WpfDialogService.Info("Importar cotización", "El archivo no contiene filas con datos reconocidos.");
                return;
            }

            int saved = store.UpsertMany(entries);
            Refresh();
            StatusMessage = $"{saved} precio(s) importados desde {Path.GetFileName(file)}.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Quotes", "Falló importar cotización", ex);
            WpfDialogService.Error("Importar cotización", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ApplyPrice(QuoteComparisonRowVm? row)
    {
        if (row == null || row.PrecioMejor <= 0m)
            return;

        if (!TryApplyBestPrice(row))
            return;

        StatusMessage = $"Precio aplicado a {row.Codigo}: proveedor {row.ProveedorMejor}.";
    }

    private void ApplyAllBestPrices()
    {
        if (session.IsReadOnly)
            return;

        int applied = 0;
        foreach (var row in FilteredComparisonRows.ToList())
        {
            if (TryApplyBestPrice(row))
                applied++;
        }

        if (applied > 0)
            session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);

        StatusMessage = applied == 0
            ? "No hay mejores precios aplicables en la comparación visible."
            : $"Aplicados {applied:N0} mejores precios a materiales del proyecto.";
        applyAllBestPricesCommand.RaiseCanExecuteChanged();
        applySelectionToBankCommand.RaiseCanExecuteChanged();
    }

    private bool CanApplySelectionToBank() =>
        !session.IsReadOnly && (SelectedComparisonRow != null || FilteredComparisonRows.Any());

    private void ApplySelectionToBank()
    {
        if (session.IsReadOnly)
            return;

        IReadOnlyList<QuoteComparisonRowVm> rows = SelectedComparisonRow != null
            ? new[] { SelectedComparisonRow }
            : FilteredComparisonRows.ToList();

        var entries = new List<PriceBankEntry>();
        foreach (var row in rows)
        {
            var source = allEntries.FirstOrDefault(e =>
                string.Equals(Normalize(e.Codigo), Normalize(row.Codigo), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Proveedor, row.ProveedorSeleccionado, StringComparison.OrdinalIgnoreCase));

            if (source == null)
                continue;

            var clone = source.Clone();
            clone.OrigenPrecio = "Adjudicación cotización";
            clone.FechaPrecio = DateTime.Today;
            clone.ActualizadoEn = AppClock.Now;
            clone.ProyectoOrigen = workspace.HasOpenProject
                ? session.ProjectInfo.Nombre
                : clone.ProyectoOrigen;
            entries.Add(clone);
        }

        if (entries.Count == 0)
        {
            StatusMessage = "Seleccione una fila de comparación para adjudicar al banco.";
            return;
        }

        int saved = store.UpsertMany(entries);
        Refresh();
        StatusMessage = saved == 1
            ? $"Adjudicado {entries[0].Codigo} ({entries[0].Proveedor}) al banco de precios."
            : $"Adjudicados {saved:N0} precios de la selección al banco.";
    }

    private bool TryApplyBestPrice(QuoteComparisonRowVm row)
    {
        if (row.PrecioMejor <= 0m)
            return false;

        var material = session.Materiales.FirstOrDefault(m =>
            string.Equals(m.Codigo, row.Codigo, StringComparison.OrdinalIgnoreCase));

        if (material == null)
            return false;

        decimal valorUf = session.ProjectInfo.ValorUfClp > 0m ? session.ProjectInfo.ValorUfClp : PriceConversion.DefaultValorUfClp;
        material.MatClp = row.PrecioMejorClp > 0m ? row.PrecioMejorClp : row.PrecioMejor * valorUf;
        material.RecalcularUf(valorUf);
        session.SyncCubicacionPricesFromMateriales();
        session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
        return true;
    }

    private static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
