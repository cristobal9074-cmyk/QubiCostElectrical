using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Prices;

public sealed partial class PriceBankViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    public PriceBankViewModel(WpfAppHost host, IShellNavigationHost? shell = null)
        : this(host.Session, host.ProjectWorkspace, host.Permissions, host.MaterialSearch, host.PriceBank, host.PriceBankStore, shell) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PriceBankViewModel(BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, IPermissionService permissions, IMaterialSearchService materialSearch, PriceBankApplicationService priceBank, IPriceBankStore store, IShellNavigationHost? shell = null) : base(session)
    {
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.permissions = permissions;
        this.materialSearch = materialSearch;
        this.shell = shell;
        this.store = store;
        this.priceBank = priceBank;

        refreshCommand = new RelayCommand(Refresh);
        importFileCommand = new RelayCommand(ImportFromFile, () => CanEdit);
        downloadTemplateCommand = new RelayCommand(DownloadImportTemplate);
        importProjectCommand = new RelayCommand(ImportCurrentProject, CanImportCurrentProject);
        saveSelectedCommand = new RelayCommand(SaveSelectedEntry, () => SelectedEntry != null && CanEdit);
        deleteSelectedCommand = new RelayCommand(DeleteSelectedEntry, () => SelectedEntry != null && CanEdit);
        deleteAllCommand = new RelayCommand(DeleteAllEntries, () => entries.Count > 0 && CanEdit);
        newEntryCommand = new RelayCommand(CreateNewEntry, () => CanEdit);
        applyProjectPricesCommand = new RelayCommand(ApplyProjectPrices, CanApplyProjectPrices);
        clearFiltersCommand = new RelayCommand(ClearFilters);
        clearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => HasSearchText);
        OpenProjectCommand = shell != null
            ? new RelayCommand(() => _ = shell.OpenProjectAsync())
            : new RelayCommand(() => { }, () => false);

        // Sin SortDescriptions: la lista se muestra en el orden en que llegó (import/creación —
        // ver PriceBankStore, columna "orden"), no reordenada alfabéticamente por código. El
        // usuario puede seguir ordenando a mano haciendo clic en una cabecera de columna
        // (CanUserSortColumns="True" en la grilla).
        entriesView = CollectionViewSource.GetDefaultView(entries);
        entriesView.Filter = FilterEntry;

        Refresh();
    }

    protected override void DisposeCore()
    {
        if (searchDebounceTimer != null)
        {
            searchDebounceTimer.Stop();
            searchDebounceTimer.Tick -= OnSearchDebounceTick;
        }
    }

    public ObservableCollection<PriceBankEntryVm> Entries => entries;
    public ICollectionView FilteredEntries => entriesView;
    public ObservableCollection<PriceBankUpdateVm> PreviewUpdates => previewUpdates;
    public ObservableCollection<string> ImportLog => importLog;
    public ObservableCollection<string> CategoryOptions => categoryOptions;
    public ObservableCollection<string> ProviderOptions => providerOptions;
    public ObservableCollection<string> CurrencyOptions => currencyOptions;

    public PriceBankEntryVm? SelectedEntry
    {
        get => selectedEntry;
        set
        {
            if (!SetProperty(ref selectedEntry, value))
                return;
            saveSelectedCommand.RaiseCanExecuteChanged();
            deleteSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(searchText);
    public bool HasActiveFilters => HasSearchText || (!string.IsNullOrEmpty(selectedCategory) && !string.Equals(selectedCategory, "Todos", StringComparison.OrdinalIgnoreCase));

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            cachedNormalizedSearch = materialSearch.Normalize(value ?? "");
            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(HasActiveFilters));
            (ClearSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (string.IsNullOrWhiteSpace(value))
            {
                searchDebounceTimer?.Stop();
                entriesView.Refresh();
                UpdateSummary();
            }
            else
            {
                ScheduleSearchRefresh();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (!SetProperty(ref selectedCategory, value))
                return;
            OnPropertyChanged(nameof(HasActiveFilters));
            entriesView.Refresh();
            UpdateSummary();
        }
    }

    public string SelectedProvider
    {
        get => selectedProvider;
        set
        {
            if (!SetProperty(ref selectedProvider, value))
                return;
            entriesView.Refresh();
            UpdateSummary();
        }
    }

    public string SelectedCurrency
    {
        get => selectedCurrency;
        set
        {
            if (!SetProperty(ref selectedCurrency, value))
                return;
            entriesView.Refresh();
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

    public string ImportSummary
    {
        get => importSummary;
        private set => SetProperty(ref importSummary, value);
    }

    public string ProjectSummary
    {
        get => projectSummary;
        private set => SetProperty(ref projectSummary, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public bool IsReadOnly => session.IsReadOnly || !permissions.CanEditGlobalPriceBank;
    public bool CanEdit => !IsReadOnly;

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }
    public bool HasNoFilterResults { get => hasNoFilterResults; private set => SetProperty(ref hasNoFilterResults, value); }

    public bool HasProject => projectWorkspace.HasOpenProject;

    public ICommand OpenProjectCommand { get; }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand ImportFileCommand => importFileCommand;
    public ICommand DownloadTemplateCommand => downloadTemplateCommand;
    public ICommand ImportProjectCommand => importProjectCommand;
    public ICommand SaveSelectedCommand => saveSelectedCommand;
    public ICommand DeleteSelectedCommand => deleteSelectedCommand;
    public ICommand DeleteAllCommand => deleteAllCommand;
    public ICommand NewEntryCommand => newEntryCommand;
    public ICommand ApplyProjectPricesCommand => applyProjectPricesCommand;
    public ICommand ClearFiltersCommand => clearFiltersCommand;
    public ICommand ClearSearchCommand => clearSearchCommand;

    public async void Refresh()
    {
        if (entries.Any(x => x.HasChanges) &&
            !WpfDialogService.Confirm("Cambios sin guardar",
                "Hay cambios sin guardar en el banco de precios. ¿Desea descartarlos y recargar la lista?",
                isWarning: true))
            return;

        IsBusy = true;
        try
        {
            var selectedCode = SelectedEntry?.Codigo;
            SelectedEntry = null;
            entries.Clear();
            decimal valorUf = HasProject ? session.ProjectInfo.ValorUfClp : 0m;
            IReadOnlyList<PriceBankEntry> loaded;
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                loaded = await Task.Run(() => store.GetAll());
            }
            else
            {
                loaded = store.GetAll();
            }

            Action apply = () =>
            {
                foreach (var item in loaded.Select(x => new PriceBankEntryVm(x, valorUf, () => CanEdit, ModoRendimientoActivo, TarifaDhActiva, IndiceMoFaActiva)))
                    entries.Add(item);

                BuildFilterOptions();
                entriesView.Refresh();
                SelectByCode(selectedCode);
                ReloadProjectPreview();
                RecomputeEntryStats();
                UpdateSummary();
                UpdateProjectSummary();
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(CanEdit));
                StatusMessage = entries.Count == 0
                    ? "El banco de precios está vacío."
                    : $"Banco cargado con {entries.Count} registros.";
                ShowEmptyState = entries.Count == 0;
                deleteAllCommand.RaiseCanExecuteChanged();
            };

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(apply);
            }
            else
            {
                apply();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("PriceBank", "Falló cargar el banco de precios", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            StatusMessage = userMessage;
            ShellActivity.Report(userMessage);
            WpfDialogService.Error("Banco de precios", userMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool TryDiscardChanges()
    {
        Refresh();
        return true;
    }

    bool IPageWithPendingChanges.HasPendingChanges => entries.Any(x => x.HasChanges);

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (e.Kind == ProjectSessionChangeKind.ProjectInfo)
            {
                RefreshValorUfEnTodasLasFilas();
                NotifyMonedaChanged();
            }
            if (e.Kind is ProjectSessionChangeKind.ProjectInfo or ProjectSessionChangeKind.Materiales or ProjectSessionChangeKind.Reset or ProjectSessionChangeKind.Saved)
                ReloadProjectPreview();
        });
    }

    // El precio fuente de cada fila es CLP; la UF que se muestra se deriva del valor de UF del
    // proyecto (pestaña Moneda). Si ese valor cambia, toda la lista recalcula su UF al instante
    // — sin esto, la UF mostrada quedaría pegada al valor viejo hasta reabrir el banco.
    private void RefreshValorUfEnTodasLasFilas()
    {
        decimal valorUf = HasProject ? session.ProjectInfo.ValorUfClp : 0m;
        foreach (var entry in entries)
            entry.ValorUfClp = valorUf;
    }

    // entriesView.Refresh() recorre y refiltra TODO el banco de precios cargado en memoria.
    // Sin debounce, un banco corporativo grande (miles de códigos) se congela por un instante
    // en cada tecla al escribir en el buscador. Se agrupan las teclas seguidas en una sola
    // pasada, 200ms después de la última tecla — mismo patrón que ScheduleRefreshTotals en
    // Cubicación.
    private void ScheduleSearchRefresh()
    {
        searchDebounceTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        searchDebounceTimer.Stop();
        searchDebounceTimer.Tick -= OnSearchDebounceTick;
        searchDebounceTimer.Tick += OnSearchDebounceTick;
        searchDebounceTimer.Start();
    }

    private void OnSearchDebounceTick(object? sender, EventArgs e)
    {
        searchDebounceTimer?.Stop();
        entriesView.Refresh();
        UpdateSummary();
    }

    // Selector.SelectedItem es TwoWay por defecto: al hacer target.Clear() sobre la colección
    // bindeada a ItemsSource, WPF pierde la selección del ComboBox y empuja null de vuelta a
    // SelectedCategory/Provider/Currency — que FilterEntry() interpreta como "filtrar por null",
    // rechazando TODAS las filas (0 visibles aunque el banco tenga miles de registros). Por eso
    // se guarda la selección antes de reconstruir las listas y se restaura después (o "Todos"/
    // "Todas" si esa opción ya no existe, ej. tras eliminar la única fila de una categoría).
    private void BuildFilterOptions()
    {
        var prevCategory = SelectedCategory;
        var prevProvider = SelectedProvider;
        var prevCurrency = SelectedCurrency;

        RebuildOptions(categoryOptions, entries.Select(x => x.Categoria));
        RebuildOptions(providerOptions, entries.Select(x => x.Proveedor));
        RebuildOptions(currencyOptions, entries.Select(x => x.MonedaPrecio));

        SelectedCategory = categoryOptions.Contains(prevCategory) ? prevCategory : "Todos";
        SelectedProvider = providerOptions.Contains(prevProvider) ? prevProvider : "Todos";
        SelectedCurrency = currencyOptions.Contains(prevCurrency) ? prevCurrency : "Todas";
    }

    private static void RebuildOptions(ObservableCollection<string> target, IEnumerable<string> source)
    {
        var values = source
            .Select(x => string.IsNullOrWhiteSpace(x) ? "—" : x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        target.Clear();
        target.Add("Todos");
        foreach (var value in values)
            target.Add(value);
    }

    private bool FilterEntry(object obj)
    {
        if (obj is not PriceBankEntryVm entry)
            return false;

        if (!string.IsNullOrWhiteSpace(cachedNormalizedSearch))
        {
            bool match = (entry.Codigo != null && entry.Codigo.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase))
                || (entry.Descripcion != null && entry.Descripcion.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase))
                || (entry.Categoria != null && entry.Categoria.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase))
                || (entry.Proveedor != null && entry.Proveedor.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase))
                || (entry.Marca != null && entry.Marca.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase));

            if (!match)
            {
                string normDesc = materialSearch.Normalize(entry.Descripcion ?? "");
                if (!normDesc.Contains(cachedNormalizedSearch, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        // El único filtro (además del buscador) es Familia — Proveedor/Marca ya no se muestran
        // en la grilla y Moneda quedó fija en CLP, así que dejaron de servir como filtro.
        if (!string.Equals(SelectedCategory, "Todos", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Categoria ?? "—", SelectedCategory, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    // providers/categories distintos dependían de recorrer TODO entries en cada UpdateSummary()
    // — incluyendo llamadas donde entries no cambió, solo el filtro (SelectedCategory,
    // SelectedProvider, SelectedCurrency, ClearFilters, cada tick del debounce de búsqueda).
    // Se cachean y solo se recalculan en RecomputeEntryStats(), invocado desde los sitios donde
    // entries realmente cambia (Refresh, agregar/guardar/eliminar). "filtered" sí depende del
    // filtro activo y se recalcula siempre — es O(n) sobre la vista, no O(n) duplicado.
    private int cachedProviderCount;
    private int cachedCategoryCount;

    private void RecomputeEntryStats()
    {
        cachedProviderCount = entries.Select(x => x.Proveedor).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        cachedCategoryCount = entries.Select(x => x.Categoria).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private void UpdateSummary()
    {
        int filtered = entriesView.Cast<object>().Count();
        SummaryText = $"{filtered:N0} visibles · {entries.Count:N0} total · {cachedCategoryCount:N0} categorías · {cachedProviderCount:N0} proveedores";
        HasNoFilterResults = entries.Count > 0 && filtered == 0;
        if (HasNoFilterResults)
        {
            StatusMessage = "Sin coincidencias.";
            ShellActivity.Report(StatusMessage);
        }
        else if (entries.Count == 0)
        {
            StatusMessage = "El banco de precios está vacío.";
            ShellActivity.Report(StatusMessage);
        }
        else
        {
            if (StatusMessage is "Sin coincidencias." or "El banco de precios está vacío.")
                StatusMessage = $"{filtered:N0} visibles de {entries.Count:N0}.";
            if (ShellActivity.Text is "Sin coincidencias." or "El banco de precios está vacío.")
                ShellActivity.Report("");
        }
    }

    private void UpdateProjectSummary()
    {
        ProjectSummary = HasProject
            ? $"{session.ProjectInfo.Nombre} · {session.Materiales.Count:N0} materiales"
            : "Sin proyecto abierto.";
    }

    private void ReloadProjectPreview()
    {
        PreviewUpdates.Clear();
        if (!HasProject)
        {
            ProjectSummary = "Sin proyecto abierto.";
            return;
        }

        foreach (var update in priceBank.PreviewProjectUpdates(session.Materiales, session.ProjectInfo.ValorUfClp))
            PreviewUpdates.Add(new PriceBankUpdateVm(update));

        UpdateProjectSummary();
        applyProjectPricesCommand.RaiseCanExecuteChanged();
        importProjectCommand.RaiseCanExecuteChanged();
    }

    private bool CanImportCurrentProject() => HasProject && session.Materiales.Count > 0 && CanEdit;

    private bool CanApplyProjectPrices() => HasProject && PreviewUpdates.Count > 0 && CanEdit;

    private void CreateNewEntry()
    {
        if (!CanEdit)
            return;
        var entry = new PriceBankEntry
        {
            Codigo = $"NUEVO-{Guid.NewGuid():N}".Substring(0, 14),
            Descripcion = "Nuevo material",
            Unidad = "",
            Categoria = "General",
            MatClp = 0m,
            MoClp = 0m,
            MonedaPrecio = Moneda.CLP,
            FechaPrecio = DateTime.Today,
            OrigenPrecio = "Edición manual",
            ProyectoOrigen = HasProject ? session.ProjectInfo.Nombre : ""
        };

        var vm = new PriceBankEntryVm(entry, HasProject ? session.ProjectInfo.ValorUfClp : 0m, () => CanEdit, ModoRendimientoActivo, TarifaDhActiva, IndiceMoFaActiva);
        entries.Insert(0, vm);
        entriesView.Refresh();
        SelectedEntry = vm;
        StatusMessage = "Fila nueva creada. Complete los campos y guarde.";
        ShowEmptyState = false;
        RecomputeEntryStats();
        UpdateSummary();
        deleteAllCommand.RaiseCanExecuteChanged();
    }

    private void SaveSelectedEntry()
    {
        if (!CanEdit || SelectedEntry == null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedEntry.Codigo))
        {
            WpfDialogService.Info("Banco de precios", "El código no puede quedar vacío.");
            return;
        }

        var originalCode = SelectedEntry.OriginalCodigo;
        var savedCode = SelectedEntry.Codigo;
        bool renamed = !string.Equals(originalCode, SelectedEntry.Codigo, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(originalCode);

        if (renamed)
            store.Delete(originalCode);

        SelectedEntry.Source.ActualizadoEn = AppClock.Now;
        store.Upsert(SelectedEntry.Source);
        SelectedEntry.MarkSavedAs(SelectedEntry.Codigo);

        // Sincronizar de inmediato con el catálogo del proyecto activo si está abierto y no es de solo lectura.
        if (HasProject && CanEdit)
        {
            var codeToFind = renamed ? originalCode : savedCode;
            var projectMat = session.Materiales.FirstOrDefault(m => CodeHelper.Match(m.Codigo, codeToFind));

            if (projectMat == null)
            {
                bool isUsedInCubicacion = session.Cubicacion.Any(r => CodeHelper.Match(r.Codigo, codeToFind));
                if (isUsedInCubicacion)
                {
                    projectMat = MapEntryToMaterial(SelectedEntry.Source);
                    session.Materiales.Add(projectMat);
                }
            }

            if (projectMat != null)
            {
                if (renamed)
                    projectMat.Codigo = savedCode;
                projectMat.MatClp = SelectedEntry.PrecioClp;
                projectMat.MoClp = SelectedEntry.MoClp;
                projectMat.MoFactorBase = SelectedEntry.MoFactorBase;
                projectMat.RendimientoDh = SelectedEntry.RendimientoDh;
                projectMat.DiaXPareja = SelectedEntry.DiaXPareja;
                projectMat.MoFormulaTipo = SelectedEntry.MoFormulaTipo;
                projectMat.MoPorcentajeMaterial = SelectedEntry.MoPorcentajeMaterial;
                projectMat.Descripcion = SelectedEntry.Descripcion;
                projectMat.Unidad = SelectedEntry.Unidad;
                projectMat.FechaPrecio = SelectedEntry.FechaPrecio;
                projectMat.Proveedor = SelectedEntry.Proveedor;
                projectMat.MonedaPrecio = SelectedEntry.MonedaPrecio;
                projectMat.RecalcularUf(session.ProjectInfo.ValorUfClp);

                // Sync fuerte: no depender de que Cubicación esté abierta para actualizar filas.
                int synced = session.SyncCubicacionPricesFromMateriales();
                if (synced > 0)
                    session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
                else
                    session.MarkDirty(ProjectDirtyScope.Materiales);
            }
        }

        BuildFilterOptions();
        entriesView.Refresh();
        RecomputeEntryStats();
        UpdateSummary();
        ReloadProjectPreview();
        StatusMessage = $"'{savedCode}' guardado en el banco de precios.";
    }

    private void DeleteSelectedEntry()
    {
        if (!CanEdit || SelectedEntry == null)
            return;

        string codigo = SelectedEntry.Codigo;
        if (!WpfDialogService.Confirm("Eliminar precio",
                $"¿Eliminar '{codigo}' del banco de precios? Esta acción no se puede deshacer.",
                isWarning: true))
            return;

        if (!string.IsNullOrWhiteSpace(SelectedEntry.OriginalCodigo))
            store.Delete(SelectedEntry.OriginalCodigo);
        else
            store.Delete(codigo);

        entries.Remove(SelectedEntry);
        SelectedEntry = null;
        entriesView.Refresh();
        BuildFilterOptions();
        RecomputeEntryStats();
        UpdateSummary();
        ReloadProjectPreview();
        StatusMessage = $"'{codigo}' fue eliminado.";
        ShowEmptyState = entries.Count == 0;
        deleteAllCommand.RaiseCanExecuteChanged();
    }

    // Botón "Eliminar todo": vacía por completo el banco de precios central (compartido entre
    // todos los proyectos). Irreversible — confirmación explícita con el conteo de registros
    // antes de tocar la base de datos, con respaldo preventivo automático (.bak).
    private void DeleteAllEntries()
    {
        if (!CanEdit || entries.Count == 0)
            return;

        int total = entries.Count;
        if (!ConfirmDeleteAll(
                $"Se eliminarán los {total:N0} precios del banco de forma permanente. Esta acción NO se puede deshacer.\nSe creará un respaldo de seguridad preventivo antes de continuar.\n\n¿Continuar?"))
            return;

        var bakPath = TryCreateBackupBeforeDeleteAll();
        store.DeleteAll();
        entries.Clear();
        SelectedEntry = null;
        entriesView.Refresh();
        BuildFilterOptions();
        RecomputeEntryStats();
        UpdateSummary();
        ReloadProjectPreview();
        StatusMessage = string.IsNullOrWhiteSpace(bakPath)
            ? $"Se eliminaron {total:N0} precios del banco."
            : $"Se eliminaron {total:N0} precios del banco (respaldo: {Path.GetFileName(bakPath)}).";
        ShowEmptyState = true;
        deleteAllCommand.RaiseCanExecuteChanged();
    }

    internal string? TryCreateBackupBeforeDeleteAll()
    {
        try
        {
            var dbPath = (store as PriceBankStore)?.DatabasePath;
            if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
            {
                var dir = Path.GetDirectoryName(dbPath) ?? "";
                var fileName = Path.GetFileName(dbPath);
                var bakPath = Path.Combine(dir, $"{fileName}.{AppClock.Now:yyyyMMdd_HHmmss}.bak");
                File.Copy(dbPath, bakPath, overwrite: true);
                AppLog.Info("PriceBank", $"Respaldo preventivo del banco de precios creado en: {bakPath}");
                return bakPath;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning("PriceBank", "No se pudo generar el respaldo preventivo del banco de precios", ex);
        }
        return null;
    }

    private void DownloadImportTemplate()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Descargar plantilla de lista de precios",
            Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
            FileName = "Plantilla_Lista_Precios_Qubi.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            return;

        try
        {
            PriceBankSeeder.WriteImportTemplate(dialog.FileName);
            StatusMessage = $"Plantilla guardada: {Path.GetFileName(dialog.FileName)}";
            WpfDialogService.Info(
                "Plantilla lista",
                "Plantilla guardada con las columnas:\nCódigo | Descripción | UN | Precio CLP | Familia | MO\n\n" +
                "Complete sus precios (CLP) e importe el archivo con «Importar lista».");
        }
        catch (Exception ex)
        {
            AppLog.Error("PriceBank", "Falló: Descargar plantilla", ex);
            WpfDialogService.Error("Descargar plantilla", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ClearFilters()
    {
        SearchText = "";
        SelectedCategory = "Todos";
        SelectedProvider = "Todos";
        SelectedCurrency = "Todas";
        entriesView.Refresh();
        UpdateSummary();
        StatusMessage = "Filtros limpiados.";
    }

    public void ImportFromFile() => ImportFromFile(null);

    public void ImportFromFile(string? explicitPath)
    {
        if (!CanEdit)
            return;
        var file = !string.IsNullOrWhiteSpace(explicitPath) ? explicitPath : PromptPriceFile();
        if (file == null)
            return;

        try
        {
            if (entries.Any(x => x.HasChanges) &&
                !WpfDialogService.Confirm("Cambios sin guardar",
                    "Hay cambios sin guardar. ¿Desea descartarlos antes de importar un archivo?",
                    isWarning: true))
                return;

            var parsed = PriceBankSeeder.ParseFile(file);
            if (parsed.Count == 0)
            {
                WpfDialogService.Info(
                    "Importar precios",
                    "El archivo no contiene registros válidos.\n\n" + PriceBankSeeder.ImportFormatHelpMessage);
                return;
            }

            var materials = parsed.Select(MapEntryToMaterial).ToList();
            var result = priceBank.ImportFromProject(materials, Path.GetFileNameWithoutExtension(file) ?? "");
            Refresh();
            ImportSummary = $"Archivo: {Path.GetFileName(file)} · filas: {result.MaterialesProcesados:N0} · consolidadas: {result.EntradasGuardadas:N0}";
            AppendImportLog(ImportSummary);
            StatusMessage = "Importación completada.";
        }
        catch (Exception ex)
        {
            AppLog.Error("PriceBank", "Falló: Importar precios", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            WpfDialogService.Error("Importar precios", userMessage);
            StatusMessage = userMessage;
        }
    }

    private void ImportCurrentProject()
    {
        if (!CanEdit || !CanImportCurrentProject())
        {
            WpfDialogService.Info("Banco de precios", "Abra un proyecto con materiales antes de importar al banco.");
            return;
        }

        try
        {
            if (entries.Any(x => x.HasChanges) &&
                !WpfDialogService.Confirm("Cambios sin guardar",
                    "Hay cambios sin guardar. ¿Desea descartarlos antes de actualizar desde el proyecto?",
                    isWarning: true))
                return;

            var result = priceBank.ImportFromProject(session.Materiales, session.ProjectInfo.Nombre);
            Refresh();
            ImportSummary = $"Proyecto: {session.ProjectInfo.Nombre} · materiales: {result.MaterialesProcesados:N0} · consolidados: {result.EntradasGuardadas:N0}";
            AppendImportLog(ImportSummary);
            StatusMessage = "Banco actualizado desde el proyecto activo.";
        }
        catch (Exception ex)
        {
            AppLog.Error("PriceBank", "Falló: Actualizar banco", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            WpfDialogService.Error("Actualizar banco", userMessage);
            StatusMessage = userMessage;
        }
    }

    private void ApplyProjectPrices()
    {
        if (!CanEdit || !CanApplyProjectPrices())
            return;

        if (!WpfDialogService.Confirm("Aplicar precios",
                "Se aplicarán los precios del banco al proyecto activo. La operación quedará marcada como cambio.",
                isWarning: true))
            return;

        try
        {
            // Asegurar que todos los materiales usados en la cubicación que existen en el banco de precios estén en el catálogo del proyecto.
            var existingCodes = new HashSet<string>(session.Materiales.Select(m => CodeHelper.Normalize(m.Codigo)), StringComparer.OrdinalIgnoreCase);
            var missingCodes = session.Cubicacion
                .Where(r => !string.IsNullOrWhiteSpace(r.Codigo))
                .Select(r => r.Codigo)
                .Distinct()
                .Where(c => !existingCodes.Contains(CodeHelper.Normalize(c)))
                .ToList();

            foreach (var code in missingCodes)
            {
                var entry = store.GetByCodigo(code);
                if (entry != null)
                {
                    session.Materiales.Add(MapEntryToMaterial(entry));
                }
            }

            var updates = priceBank.ApplyBankPricesToProject(
                session.Materiales,
                session.ProjectInfo,
                PreviewUpdates.Select(x => x.Source).ToList(),
                session.ProjectInfo.ValorUfClp,
                allowWhenFrozen: false);

            if (updates.Status == PriceBankApplyStatus.Applied && updates.AppliedCount > 0)
            {
                session.SyncCubicacionPricesFromMateriales();
                session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
                StatusMessage = $"Aplicados {updates.AppliedCount:N0} precios al proyecto.";
                ReloadProjectPreview();
                UpdateSummary();
            }
            else if (updates.Status == PriceBankApplyStatus.BlockedByFrozenPrices)
            {
                WpfDialogService.Info("Aplicar precios", "El proyecto está congelado. No se aplicaron cambios.");
            }
            else
            {
                WpfDialogService.Info("Aplicar precios", "No hay precios nuevos para aplicar.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("PriceBank", "Falló: Aplicar precios", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            WpfDialogService.Error("Aplicar precios", userMessage);
            StatusMessage = userMessage;
        }
    }

    private void AppendImportLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        importLog.Insert(0, $"{DateTime.Now:HH:mm} · {message}");
        while (importLog.Count > 6)
            importLog.RemoveAt(importLog.Count - 1);
    }

    /// <summary>Modo rendimiento del proyecto activo (para mostrar la MO derivada en la grilla).</summary>
    private bool ModoRendimientoActivo =>
        HasProject && string.Equals(
            session.ProjectInfo.ModoMoInstalacion, MaterialMoResolver.ModoRendimiento, StringComparison.OrdinalIgnoreCase);

    private decimal TarifaDhActiva => HasProject ? session.ProjectInfo.TarifaDhClp : 0m;

    /// <summary>
    /// Índice global M.O. (FA) del proyecto activo. 0 = sin calibrar: la M.O. por % se calcula como
    /// Material × % (factor 1) hasta que el índice se derive del plan de obra (Costo → Familias → FACTORES).
    /// </summary>
    private decimal IndiceMoFaActiva =>
        HasProject && session.ProjectInfo.IndiceMoFa > 0m
            ? session.ProjectInfo.IndiceMoFa
            : MaterialMoResolver.IndiceMoFaDefault;

    private static MaterialRow MapEntryToMaterial(PriceBankEntry entry) => new()
    {
        Codigo = entry.Codigo ?? "",
        Descripcion = entry.Descripcion ?? "",
        Unidad = entry.Unidad ?? "",
        Categoria = entry.Categoria ?? "",
        MatClp = entry.MatClp,
        MoClp = entry.MoClp,
        MoFactorBase = entry.MoFactorBase,
        RendimientoDh = entry.RendimientoDh,
        DiaXPareja = entry.DiaXPareja,
        MoFormulaTipo = entry.MoFormulaTipo,
        MoPorcentajeMaterial = entry.MoPorcentajeMaterial,
        MonedaPrecio = string.IsNullOrWhiteSpace(entry.MonedaPrecio) ? Moneda.CLP : entry.MonedaPrecio,
        Proveedor = entry.Proveedor ?? "",
        Marca = entry.Marca ?? "",
        FechaPrecio = entry.FechaPrecio == default ? DateTime.Today : entry.FechaPrecio,
        OrigenPrecio = string.IsNullOrWhiteSpace(entry.OrigenPrecio) ? "Importación de banco" : entry.OrigenPrecio,
    };

    private static string? PromptPriceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar banco de precios",
            Filter = "Archivos Excel o CSV|*.xlsx;*.xlsm;*.csv|Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|CSV (*.csv)|*.csv|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void SelectByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        var match = entries.FirstOrDefault(x => string.Equals(x.Codigo, code, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            SelectedEntry = match;
    }
}
