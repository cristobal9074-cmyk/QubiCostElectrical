using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public VersionsViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(
            shell,
            host.Session,
            host.ProjectWorkspace,
            host.Dashboard,
            host.Services.VersionComparison,
            host.Services.RevisionDiffPdfExporter,
            host.Services.RevisionDiffExcelExporter,
            host.FileDialog,
            host.FileOpener,
            host.Services.ItemizadoSpreadsheet,
            host.Services.ImportItemizado)
    { }

    public VersionsViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        DashboardPresenter dashboard,
        IVersionComparisonService versionComparison,
        IBudgetRevisionDiffPdfExporter? pdfExporter = null,
        IBudgetRevisionDiffExcelExporter? excelExporter = null,
        IFileDialogService? fileDialog = null,
        IFileOpenService? fileOpener = null,
        IItemizadoSpreadsheetReader? spreadsheetReader = null,
        ImportarItemizadoUseCase? importItemizadoUseCase = null) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.dashboard = dashboard;
        this.versionComparison = versionComparison;
        this.pdfExporter = pdfExporter;
        this.excelExporter = excelExporter;
        this.fileDialog = fileDialog;
        this.fileOpener = fileOpener;
        this.spreadsheetReader = spreadsheetReader;
        this.importItemizadoUseCase = importItemizadoUseCase;

        refreshCommand = new RelayCommand(Refresh);
        newCommand = new RelayCommand(CreateVersion, () => HasProject && !session.IsReadOnly);
        activateCommand = new RelayCommand(ActivateVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        renameCommand = new RelayCommand(RenameVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        deleteCommand = new RelayCommand(DeleteVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        refreshComparisonCommand = new RelayCommand(RefreshComparison);
        goToItemCommand = new RelayCommand(GoToItem, () => selectedComparisonRow != null);
        exportPdfCommand = new RelayCommand(ExportPdf, () => HasProject && currentSummary != null && comparisonRows.Count > 0);
        exportExcelCommand = new RelayCommand(ExportExcel, () => HasProject && currentSummary != null && comparisonRows.Count > 0);
        createChangeOrderFromDiffCommand = new RelayCommand(CreateChangeOrderFromDiff, () => HasProject && currentSummary != null && comparisonRows.Count > 0 && !session.IsReadOnly);
        compareWithExcelCommand = new RelayCommand(CompareWithExcel, () => HasProject);
        applyAddendaQuantitiesCommand = new RelayCommand(ApplyAddendaQuantities, () => HasProject && !session.IsReadOnly && isAddendaComparison && lastIncomingAddendaItems != null);
        saveAddendaAsVersionCommand = new RelayCommand(SaveAddendaAsVersion, () => HasProject && !session.IsReadOnly && isAddendaComparison && currentSummary != null);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        versionsView = CollectionViewSource.GetDefaultView(versions);
        versionsView.Filter = FilterVersionRow;
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.FechaCreacion), ListSortDirection.Descending));
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.Codigo), ListSortDirection.Ascending));

        comparisonRowsView = CollectionViewSource.GetDefaultView(comparisonRows);
        comparisonRowsView.Filter = FilterComparisonRow;
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.DeltaUf), ListSortDirection.Descending));
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.Item), ListSortDirection.Ascending));

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            Refresh();
            NotifyMonedaChanged();
        });

    public bool HasProject => workspace.HasOpenProject;

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (columnas Venta /
    /// P.U. / Total / Δ UF y la mini tabla de recursos se muestran en pesos).
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, Moneda.CLP, StringComparison.OrdinalIgnoreCase);

    private decimal UfRate => session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;

    /// <summary>Formateador de Versiones según la moneda del proyecto.</summary>
    internal VersionsCurrencyFormat CurrencyFormat => new(MuestraUf, UfRate);

    private void NotifyMonedaChanged() => OnPropertyChanged(nameof(MuestraUf));

    public bool IsReadOnly => session.IsReadOnly;

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

    public string EmptyStateTitle => HasProject ? "Sin versiones" : "Sin proyecto abierto";

    public string EmptyStateDescription => HasProject
        ? "Cree una versión para congelar un hito del presupuesto y comparar cambios."
        : "Abra un proyecto para gestionar versiones y comparar hitos del presupuesto.";

    public ObservableCollection<VersionRowVm> Versions => versions;
    public ICollectionView FilteredVersions => versionsView;
    public ObservableCollection<VersionComparisonRowVm> ComparisonRows => comparisonRows;
    public ICollectionView FilteredComparisonRows => comparisonRowsView;

    public VersionRowVm? SelectedVersion
    {
        get => selectedVersion;
        set
        {
            if (!SetProperty(ref selectedVersion, value))
                return;

            UpdateComparisonVersionOptions();
            RefreshComparison();
            activateCommand.RaiseCanExecuteChanged();
            renameCommand.RaiseCanExecuteChanged();
            deleteCommand.RaiseCanExecuteChanged();
            goToItemCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CompareAgainstCurrent
    {
        get => compareAgainstCurrent;
        set
        {
            if (!SetProperty(ref compareAgainstCurrent, value))
                return;

            OnPropertyChanged(nameof(ShowComparisonVersionSelector));
            UpdateComparisonVersionOptions();
            RefreshComparison();
        }
    }

    public bool ShowComparisonVersionSelector => !CompareAgainstCurrent;

    public ObservableCollection<VersionRowVm> ComparisonVersionOptions { get; } = new();

    public VersionRowVm? SelectedComparisonVersion
    {
        get => selectedComparisonVersion;
        set
        {
            if (!SetProperty(ref selectedComparisonVersion, value))
                return;

            RefreshComparison();
        }
    }

    public VersionComparisonRowVm? SelectedComparisonRow
    {
        get => selectedComparisonRow;
        set
        {
            if (!SetProperty(ref selectedComparisonRow, value))
                return;
            goToItemCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedRowResourceDiffs));
            OnPropertyChanged(nameof(HasSelectedRowResourceDiffs));
        }
    }

    public IReadOnlyList<VersionsResourceDiffVm> SelectedRowResourceDiffs =>
        SelectedComparisonRow is null
            ? Array.Empty<VersionsResourceDiffVm>()
            : SelectedComparisonRow.ResourceDiffs
                .Select(r => new VersionsResourceDiffVm(r, CurrencyFormat))
                .ToList();

    public bool HasSelectedRowResourceDiffs =>
        selectedComparisonRow?.HasResourceDiffs == true;

    public BudgetVersionComparisonSummary? CurrentSummary => currentSummary;

    public RevisionDiffFilterMode ActiveFilterMode
    {
        get => activeFilterMode;
        set
        {
            if (!SetProperty(ref activeFilterMode, value))
                return;
            comparisonRowsView.Refresh();
            OnPropertyChanged(nameof(IsFilterTodos));
            OnPropertyChanged(nameof(IsFilterAdicionales));
            OnPropertyChanged(nameof(IsFilterModificados));
            OnPropertyChanged(nameof(IsFilterDeductivos));
        }
    }

    public bool IsFilterTodos
    {
        get => activeFilterMode == RevisionDiffFilterMode.Todos;
        set { if (value) ActiveFilterMode = RevisionDiffFilterMode.Todos; }
    }

    public bool IsFilterAdicionales
    {
        get => activeFilterMode == RevisionDiffFilterMode.SoloAdicionales;
        set { if (value) ActiveFilterMode = RevisionDiffFilterMode.SoloAdicionales; }
    }

    public bool IsFilterModificados
    {
        get => activeFilterMode == RevisionDiffFilterMode.SoloModificados;
        set { if (value) ActiveFilterMode = RevisionDiffFilterMode.SoloModificados; }
    }

    public bool IsFilterDeductivos
    {
        get => activeFilterMode == RevisionDiffFilterMode.SoloDeductivos;
        set { if (value) ActiveFilterMode = RevisionDiffFilterMode.SoloDeductivos; }
    }

    // Búsqueda compartida: filtra el historial de versiones y las filas del comparador.
    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value ?? string.Empty))
                return;

            versionsView.Refresh();
            comparisonRowsView.Refresh();
        }
    }

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string ActiveVersionText
    {
        get => activeVersionText;
        private set => SetProperty(ref activeVersionText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ComparisonSummaryText
    {
        get => comparisonSummaryText;
        private set => SetProperty(ref comparisonSummaryText, value);
    }

    public string ComparisonStatusText
    {
        get => comparisonStatusText;
        private set => SetProperty(ref comparisonStatusText, value);
    }

    public string ComparisonTotalsText
    {
        get => comparisonTotalsText;
        private set => SetProperty(ref comparisonTotalsText, value);
    }

    public string ComparisonAddedText
    {
        get => comparisonAddedText;
        private set => SetProperty(ref comparisonAddedText, value);
    }

    public string ComparisonChangedText
    {
        get => comparisonChangedText;
        private set => SetProperty(ref comparisonChangedText, value);
    }

    public string ComparisonRemovedText
    {
        get => comparisonRemovedText;
        private set => SetProperty(ref comparisonRemovedText, value);
    }

    public string ComparisonUnchangedText
    {
        get => comparisonUnchangedText;
        private set => SetProperty(ref comparisonUnchangedText, value);
    }

    public string ComparisonAdicionalesText
    {
        get => comparisonAdicionalesText;
        private set => SetProperty(ref comparisonAdicionalesText, value);
    }

    public string ComparisonDeductivosText
    {
        get => comparisonDeductivosText;
        private set => SetProperty(ref comparisonDeductivosText, value);
    }

    public string ComparisonNetVariationText
    {
        get => comparisonNetVariationText;
        private set => SetProperty(ref comparisonNetVariationText, value);
    }

    public decimal ComparisonDeltaUf
    {
        get => comparisonDeltaUf;
        private set
        {
            if (!SetProperty(ref comparisonDeltaUf, value))
                return;

            OnPropertyChanged(nameof(ComparisonDeltaKind));
        }
    }

    public decimal ComparisonDeltaPct
    {
        get => comparisonDeltaPct;
        private set => SetProperty(ref comparisonDeltaPct, value);
    }

    // ComparisonDeltaUf llega en UF reales desde el comparador de revisiones
    // (BudgetVersionComparisonSummary.DeltaUf, derivado de CubicacionRow.UfTotal).
    public string ComparisonDeltaText => ComparisonDeltaUf == 0m
        ? "—"
        : CurrencyFormat.FromUfSigned(ComparisonDeltaUf);
    public string ComparisonDeltaPctText => ComparisonDeltaPct == 0m ? "—"
        : $"{(ComparisonDeltaPct > 0m ? "+" : string.Empty)}{ComparisonDeltaPct:N2}%";

    // Signo de la variación para colorear la tira KPI (sube/baja/neutro).
    public string ComparisonDeltaKind => ComparisonDeltaUf > 0m ? "Aumenta"
        : ComparisonDeltaUf < 0m ? "Reduce" : "Neutro";

    public string ComparisonModeHint
    {
        get => comparisonModeHint;
        private set => SetProperty(ref comparisonModeHint, value);
    }

    public bool IsAddendaComparison
    {
        get => isAddendaComparison;
        private set => SetProperty(ref isAddendaComparison, value);
    }

    public string AddendaFileName
    {
        get => addendaFileName;
        private set => SetProperty(ref addendaFileName, value);
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand NewCommand => newCommand;
    public ICommand ActivateCommand => activateCommand;
    public ICommand RenameCommand => renameCommand;
    public ICommand DeleteCommand => deleteCommand;
    public ICommand RefreshComparisonCommand => refreshComparisonCommand;
    public ICommand GoToItemCommand => goToItemCommand;
    public ICommand ExportPdfCommand => exportPdfCommand;
    public ICommand ExportExcelCommand => exportExcelCommand;
    public ICommand CreateChangeOrderFromDiffCommand => createChangeOrderFromDiffCommand;
    public ICommand CompareWithExcelCommand => compareWithExcelCommand;
    public ICommand ApplyAddendaQuantitiesCommand => applyAddendaQuantitiesCommand;
    public ICommand SaveAddendaAsVersionCommand => saveAddendaAsVersionCommand;
    public ICommand OpenProjectCommand { get; }

    public void Refresh()
    {
        IsLoading = true;
        try
        {
            if (!HasProject)
            {
                versions.Clear();
                SelectedVersion = null;
                comparisonRows.Clear();
                SelectedComparisonRow = null;
                ShowEmptyState = true;
                OnPropertyChanged(nameof(EmptyStateTitle));
                OnPropertyChanged(nameof(EmptyStateDescription));
                OnPropertyChanged(nameof(IsReadOnly));
                SummaryText = "Sin proyecto abierto";
                ActiveVersionText = "Sin versión activa";
                StatusMessage = "Abre o crea un proyecto para gestionar versiones.";
                ComparisonSummaryText = "Sin comparación";
                ComparisonStatusText = "Abre un proyecto para habilitar la comparación de versiones.";
                ComparisonTotalsText = "—";
                ComparisonAddedText = "0 nuevas";
                ComparisonChangedText = "0 modificadas";
                ComparisonRemovedText = "0 eliminadas";
                ComparisonDeltaUf = 0m;
                ComparisonDeltaPct = 0m;
                ComparisonModeHint = "Abre un proyecto para comparar versiones.";
                OnPropertyChanged(nameof(ComparisonDeltaText));
                OnPropertyChanged(nameof(ComparisonDeltaPctText));
                newCommand.RaiseCanExecuteChanged();
                activateCommand.RaiseCanExecuteChanged();
                renameCommand.RaiseCanExecuteChanged();
                deleteCommand.RaiseCanExecuteChanged();
                return;
            }

            string? selectedVersionId = SelectedVersion?.Source.Id;
            var sorted = session.Versiones
                .OrderByDescending(x => x.FechaCreacion)
                .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            versions.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                var current = sorted[i];
                bool isActive = string.Equals(current.Id, session.ActiveVersionId, StringComparison.OrdinalIgnoreCase);
                decimal delta = i + 1 < sorted.Count
                    ? current.MontoVentaUf - sorted[i + 1].MontoVentaUf
                    : 0m;

                versions.Add(new VersionRowVm(current, isActive, delta, CurrencyFormat));
            }

            if (versions.Count == 0)
            {
                SelectedVersion = null;
            }
            else
            {
                var match = versions.FirstOrDefault(v => string.Equals(v.Source.Id, selectedVersionId, StringComparison.OrdinalIgnoreCase));
                SelectedVersion = match ?? versions.FirstOrDefault(v => v.IsActive) ?? versions[0];
            }

            ShowEmptyState = versions.Count == 0;
            OnPropertyChanged(nameof(EmptyStateTitle));
            OnPropertyChanged(nameof(EmptyStateDescription));
            OnPropertyChanged(nameof(IsReadOnly));
            UpdateSummary();
            UpdateComparisonVersionOptions();
            RefreshComparison();
            newCommand.RaiseCanExecuteChanged();
            activateCommand.RaiseCanExecuteChanged();
            renameCommand.RaiseCanExecuteChanged();
            deleteCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateSummary()
    {
        SummaryText = versions.Count == 0
            ? "Sin versiones guardadas"
            : $"{versions.Count:N0} versiones · {versions.Count(v => v.IsActive):N0} activa(s)";

        var active = versions.FirstOrDefault(v => v.IsActive);
        ActiveVersionText = active != null
            ? $"Activa: {active.Codigo} · {active.Nombre}"
            : "Sin versión activa";

        StatusMessage = versions.Count == 0
            ? "Este proyecto todavía no tiene versiones guardadas."
            : "Usa una versión para comparar, congelar o restaurar estados del proyecto.";
    }

    private void UpdateComparisonVersionOptions()
    {
        var previousId = SelectedComparisonVersion?.Source.Id;
        ComparisonVersionOptions.Clear();

        foreach (var version in versions.Where(v => v.Source.Id != SelectedVersion?.Source.Id))
            ComparisonVersionOptions.Add(version);

        SelectedComparisonVersion = ComparisonVersionOptions.FirstOrDefault(v =>
                string.Equals(v.Source.Id, previousId, StringComparison.OrdinalIgnoreCase))
            ?? ComparisonVersionOptions.FirstOrDefault();
    }

    /// <summary>Línea de totales de la comparación en la moneda del proyecto.</summary>
    private string BuildTotalsText(BudgetVersionComparisonSummary summary, string currentVerb) => MuestraUf
        ? $"Base {summary.BaselineUf:N3} UF · {currentVerb} {summary.CurrentUf:N3} UF · Delta {summary.DeltaUf:N3} UF ({summary.DeltaPct:N2}%)"
        : $"Base $ {summary.BaselineClp:N0} · {currentVerb} $ {summary.CurrentClp:N0} · Delta $ {summary.DeltaClp:N0} ({summary.DeltaPct:N2}%)";

    /// <summary>
    /// Chips de variación (adicionales / deductivos / neto) en la moneda del proyecto. Con CLP
    /// se usan las columnas en pesos del resumen y nunca se muestra UF.
    /// </summary>
    private (string Adicionales, string Deductivos, string Neto) BuildVariationTexts(
        BudgetVersionComparisonSummary summary)
    {
        if (!MuestraUf)
        {
            return (
                $"+$ {Math.Abs(summary.TotalAdicionalesClp):N0}",
                $"- $ {Math.Abs(summary.TotalDeductivosClp):N0}",
                $"{(summary.NetVariationClp >= 0m ? "+" : "-")}$ {Math.Abs(summary.NetVariationClp):N0}");
        }

        return (
            $"+{summary.TotalAdicionalesUf:N3} UF · +$ {summary.TotalAdicionalesClp:N0}",
            $"{summary.TotalDeductivosUf:N3} UF · $ {summary.TotalDeductivosClp:N0}",
            $"{(summary.NetVariationUf >= 0m ? "+" : "")}{summary.NetVariationUf:N3} UF · {(summary.NetVariationClp >= 0m ? "+$ " : "$ ")}{summary.NetVariationClp:N0}");
    }

    private void RefreshComparison()
    {        comparisonRows.Clear();
        IsAddendaComparison = false;
        AddendaFileName = string.Empty;
        lastIncomingAddendaItems = null;
        applyAddendaQuantitiesCommand.RaiseCanExecuteChanged();
        saveAddendaAsVersionCommand.RaiseCanExecuteChanged();

        var selected = SelectedVersion?.Source;
        if (selected == null)
        {
            currentSummary = null;
            ComparisonSummaryText = "Sin comparación";
            ComparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
            ComparisonTotalsText = "—";
            ComparisonAddedText = "0 nuevas";
            ComparisonChangedText = "0 modificadas";
            ComparisonRemovedText = "0 eliminadas";
            ComparisonUnchangedText = "0 sin cambio";
            ComparisonAdicionalesText = MuestraUf ? "+0.000 UF" : "+$ 0";
            ComparisonDeductivosText = MuestraUf ? "0.000 UF" : "- $ 0";
            ComparisonNetVariationText = MuestraUf ? "0.000 UF" : "$ 0";
            ComparisonDeltaUf = 0m;
            ComparisonDeltaPct = 0m;
            ComparisonModeHint = "Selecciona una versión para comparar.";
            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            SelectedComparisonRow = null;
            exportPdfCommand.RaiseCanExecuteChanged();
            exportExcelCommand.RaiseCanExecuteChanged();
            createChangeOrderFromDiffCommand.RaiseCanExecuteChanged();
            return;
        }

        string baselineLabel = $"{selected.Codigo} · {selected.Nombre}";
        BudgetVersionComparisonSummary summary;

        if (CompareAgainstCurrent)
        {
            summary = versionComparison.Compare(
                selected.Snapshot,
                session.CreateSnapshot(),
                baselineLabel,
                "Estado actual");
        }
        else
        {
            var comparison = SelectedComparisonVersion?.Source;
            if (comparison == null)
            {
                currentSummary = null;
                ComparisonSummaryText = "Sin comparación";
                ComparisonStatusText = "Selecciona una segunda versión para comparar.";
                ComparisonTotalsText = "—";
                ComparisonAddedText = "0 nuevas";
                ComparisonChangedText = "0 modificadas";
                ComparisonRemovedText = "0 eliminadas";
                ComparisonUnchangedText = "0 sin cambio";
                ComparisonAdicionalesText = MuestraUf ? "+0.000 UF" : "+$ 0";
                ComparisonDeductivosText = MuestraUf ? "0.000 UF" : "- $ 0";
                ComparisonNetVariationText = MuestraUf ? "0.000 UF" : "$ 0";
                ComparisonDeltaUf = 0m;
                ComparisonDeltaPct = 0m;
                ComparisonModeHint = "Selecciona la versión B para comparar A vs B.";
                OnPropertyChanged(nameof(ComparisonDeltaText));
                OnPropertyChanged(nameof(ComparisonDeltaPctText));
                SelectedComparisonRow = null;
                exportPdfCommand.RaiseCanExecuteChanged();
                exportExcelCommand.RaiseCanExecuteChanged();
                createChangeOrderFromDiffCommand.RaiseCanExecuteChanged();
                return;
            }

            summary = versionComparison.Compare(
                selected.Snapshot,
                comparison.Snapshot,
                baselineLabel,
                $"{comparison.Codigo} · {comparison.Nombre}");
        }

        currentSummary = summary;
        foreach (var row in summary.Rows)
            comparisonRows.Add(new VersionComparisonRowVm(row, CurrencyFormat));

        SelectedComparisonRow = comparisonRows.FirstOrDefault();

        ComparisonSummaryText = $"{summary.Rows.Count:N0} partidas comparadas";
        ComparisonStatusText = summary.Rows.Count == 0
            ? "No hay diferencias entre la versión seleccionada y el estado actual."
            : $"Comparando {summary.BaselineLabel} contra {summary.CurrentLabel}.";
        // BaselineUf/CurrentUf/DeltaUf del comparador llegan en UF reales (CubicacionRow.UfTotal);
        // en CLP se usan las columnas en pesos del resumen.
        ComparisonTotalsText = BuildTotalsText(summary, "Actual");
        ComparisonAddedText = $"{summary.AddedCount:N0} nuevas";
        ComparisonChangedText = $"{summary.ChangedCount:N0} modificadas";
        ComparisonRemovedText = $"{summary.RemovedCount:N0} eliminadas";
        ComparisonUnchangedText = $"{summary.UnchangedCount:N0} sin cambio";
        (ComparisonAdicionalesText, ComparisonDeductivosText, ComparisonNetVariationText) = BuildVariationTexts(summary);
        ComparisonDeltaUf = summary.DeltaUf;
        ComparisonDeltaPct = summary.DeltaPct;
        ComparisonModeHint = CompareAgainstCurrent
            ? $"Versión A = {summary.BaselineLabel} · Versión B = estado actual"
            : $"Versión A = {summary.BaselineLabel} · Versión B = {summary.CurrentLabel}";
        OnPropertyChanged(nameof(ComparisonDeltaText));
        OnPropertyChanged(nameof(ComparisonDeltaPctText));
        exportPdfCommand.RaiseCanExecuteChanged();
        exportExcelCommand.RaiseCanExecuteChanged();
        createChangeOrderFromDiffCommand.RaiseCanExecuteChanged();
        comparisonRowsView.Refresh();
    }

    private void CreateVersion()
    {
        int nextNum = session.Versiones.Count + 1;
        string codigo = $"V{nextNum}";
        string nombre = $"Versión {nextNum}";

        if (!PromptVersionDialog("Nueva versión", ref codigo, ref nombre))
            return;

        var snapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
        decimal ventaUf = dashboard.BuildPageState().Cost.TotalVentaUf;
        var version = new BudgetVersionRow
        {
            Codigo = string.IsNullOrWhiteSpace(codigo) ? $"V{nextNum}" : codigo.Trim(),
            Nombre = string.IsNullOrWhiteSpace(nombre) ? $"Versión {nextNum}" : nombre.Trim(),
            Estado = "Preliminar",
            CreadoPor = session.ProjectInfo.UsuarioNombre,
            FechaCreacion = AppClock.Now,
            FechaModificacion = AppClock.Now,
            MontoVentaUf = ventaUf,
            Snapshot = snapshot
        };

        session.Versiones.Add(version);
        session.ActiveVersionId = version.Id;
        session.MarkDirty(ProjectDirtyScope.Versiones);
        Refresh();
    }

    private void ActivateVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        session.ActiveVersionId = version.Id;
        session.MarkDirty(ProjectDirtyScope.Versiones);
        Refresh();
    }

    private void RenameVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        string codigo = version.Codigo;
        string nombre = version.Nombre;
        if (!PromptVersionDialog("Renombrar versión", ref codigo, ref nombre))
            return;

        version.Codigo = string.IsNullOrWhiteSpace(codigo) ? version.Codigo : codigo.Trim();
        version.Nombre = string.IsNullOrWhiteSpace(nombre) ? version.Nombre : nombre.Trim();
        version.FechaModificacion = AppClock.Now;
        session.MarkDirty(ProjectDirtyScope.Versiones);
        Refresh();
    }

    private void DeleteVersion()
    {
        var version = SelectedVersion?.Source;
        if (version == null)
            return;

        if (!WpfDialogService.Confirm(
                "Eliminar versión",
                $"¿Eliminar la versión «{version.Codigo} — {version.Nombre}»?",
                isWarning: true))
            return;

        session.Versiones.Remove(version);
        if (string.Equals(session.ActiveVersionId, version.Id, StringComparison.OrdinalIgnoreCase))
            session.ActiveVersionId = session.Versiones.LastOrDefault()?.Id ?? string.Empty;

        session.MarkDirty(ProjectDirtyScope.Versiones);
        Refresh();
    }

    private void GoToItem()
    {
        var row = selectedComparisonRow;
        if (row == null)
            return;

        shell.RequestNavigate(
            ShellRoute.Takeoff,
            new TakeoffNavigationContext(row.ItemId, row.Item, row.Descripcion));
    }

    private static bool PromptVersionDialog(string title, ref string codigo, ref string nombre)
    {
        string? responseCodigo = WpfDialogService.PromptText(title, "Código de versión", codigo);
        if (responseCodigo == null)
            return false;

        string? responseNombre = WpfDialogService.PromptText(title, "Nombre de versión", nombre);
        if (responseNombre == null)
            return false;

        codigo = responseCodigo;
        nombre = responseNombre;
        return true;
    }

    private bool FilterVersionRow(object obj)
    {
        if (obj is not VersionRowVm row) return false;
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        string q = searchText.Trim();
        return Contains(row.Codigo, q)
            || Contains(row.Nombre, q)
            || Contains(row.CreadoPor, q)
            || Contains(row.Estado, q);
    }

    private bool FilterComparisonRow(object obj)
    {
        if (obj is not VersionComparisonRowVm row) return false;

        bool passMode = activeFilterMode switch
        {
            RevisionDiffFilterMode.SoloAdicionales => row.DiffKind == RevisionDiffKind.Adicional,
            RevisionDiffFilterMode.SoloModificados => row.DiffKind == RevisionDiffKind.Modificado,
            RevisionDiffFilterMode.SoloDeductivos => row.DiffKind == RevisionDiffKind.Deductivo,
            _ => true
        };

        if (!passMode) return false;
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        string q = searchText.Trim();
        return Contains(row.ItemId, q)
            || Contains(row.Item, q)
            || Contains(row.Descripcion, q)
            || Contains(row.DetalleCambio, q);
    }

    private static bool Contains(string? source, string query) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private void ExportPdf()
    {
        if (currentSummary == null)
        {
            WpfDialogService.InfoModal("Exportar Informe", "No hay datos de comparación disponibles para exportar.");
            return;
        }

        string defaultName = $"Informe_Adicionales_{Sanitize(currentSummary.ProjectTitle)}_{Sanitize(currentSummary.BaselineLabel)}_vs_{Sanitize(currentSummary.CurrentLabel)}.pdf";
        string? path = fileDialog?.PromptSaveFile("Exportar Informe de Adicionales para Constructora (PDF)", defaultName, "Documento PDF (*.pdf)|*.pdf")
            ?? WpfDialogService.PromptSaveFile(defaultName);

        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (pdfExporter != null)
            {
                pdfExporter.Export(currentSummary, path);
            }
            else
            {
                new BudgetRevisionDiffPdfExporter().Export(currentSummary, path);
            }

            if (WpfDialogService.Confirm("Informe Exportado", $"El informe PDF fue generado exitosamente:\n{path}\n\n¿Desea abrir el archivo ahora?"))
            {
                fileOpener?.OpenFile(path);
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.InfoModal("Error al exportar", $"No se pudo generar el informe PDF:\n{ex.Message}");
        }
    }

    private void ExportExcel()
    {
        if (currentSummary == null)
        {
            WpfDialogService.InfoModal("Exportar Excel", "No hay datos de comparación disponibles para exportar.");
            return;
        }

        string defaultName = $"Comparativo_Revisiones_{Sanitize(currentSummary.ProjectTitle)}_{Sanitize(currentSummary.BaselineLabel)}_vs_{Sanitize(currentSummary.CurrentLabel)}.xlsx";
        string? path = fileDialog?.PromptSaveFile("Exportar Comparativo de Revisiones (Excel)", defaultName, "Libro de Excel (*.xlsx)|*.xlsx")
            ?? WpfDialogService.PromptSaveFile(defaultName);

        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (excelExporter != null)
            {
                excelExporter.Export(currentSummary, path);
            }
            else
            {
                new BudgetRevisionDiffExcelExporter().Export(currentSummary, path);
            }

            if (WpfDialogService.Confirm("Excel Exportado", $"El libro Excel con fórmulas vivas fue generado exitosamente:\n{path}\n\n¿Desea abrir el archivo ahora?"))
            {
                fileOpener?.OpenFile(path);
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.InfoModal("Error al exportar", $"No se pudo generar el libro Excel:\n{ex.Message}");
        }
    }

    private void CreateChangeOrderFromDiff()
    {
        if (!HasProject || currentSummary == null || session.IsReadOnly)
            return;

        var changeOrder = ChangeOrderDiffGenerator.CreateFromDiff(
            currentSummary,
            session.OrdenesCambio,
            session.ProjectInfo.GastosGeneralesPct,
            session.ProjectInfo.UtilidadPct,
            session.ProjectInfo.UsuarioNombre);

        if (changeOrder == null)
        {
            WpfDialogService.Info("Sin variaciones", "No se detectaron partidas con variaciones (adicionales, deductivos o modificaciones) en esta comparación.");
            return;
        }

        session.OrdenesCambio.Add(changeOrder);
        session.MarkDirty(ProjectDirtyScope.OrdenesCambio);

        WpfDialogService.Info(
            "Orden de Cambio Generada",
            $"Se ha creado exitosamente la orden «{changeOrder.Numero} — {changeOrder.Titulo}» con {changeOrder.Lineas.Count} partidas afectadas y un impacto de venta estimado de {CurrencyFormat.FromUfSigned(changeOrder.DeltaVentaUf)}.\n\nA continuación se abrirá el módulo de Órdenes de Cambio.");

        shell.RequestNavigate(ShellRoute.ChangeOrders);
    }

    private static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Proyecto";
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        var clean = new string(text.Where(c => !invalid.Contains(c) && c != ' ' && c != '·').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Revision" : clean;
    }

    private void CompareWithExcel()
    {
        if (!HasProject) return;

        string? filePath = fileDialog?.PromptOpenFile("Libros de Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Todos los archivos (*.*)|*.*")
            ?? PromptOpenFileDirect();

        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            return;

        try
        {
            var reader = spreadsheetReader ?? new ItemizadoSpreadsheetReader();
            var parser = importItemizadoUseCase ?? new ImportarItemizadoUseCase();

            var table = reader.ReadFirstWorksheet(filePath);
            if (table == null || table.Count == 0)
            {
                WpfDialogService.InfoModal("Archivo sin datos", "El archivo seleccionado no contiene filas o datos legibles.");
                return;
            }

            var result = parser.Execute(table.Select(r => r.ToList()).ToList());
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                WpfDialogService.InfoModal("Error al importar Addenda", $"No se pudo leer la estructura del itemizado:\n{result.Error}");
                return;
            }

            if (result.Rows.Count == 0)
            {
                WpfDialogService.InfoModal("Sin partidas", "No se encontraron partidas válidas en el archivo de la Addenda.");
                return;
            }

            string addendaName = System.IO.Path.GetFileName(filePath);
            var snapshot = session.CreateSnapshot();
            var summary = versionComparison.CompareWithIncomingItemizado(
                result.Rows,
                snapshot,
                incomingLabel: $"Addenda ({addendaName})",
                currentLabel: "Presupuesto Base");

            lastIncomingAddendaItems = result.Rows;
            AddendaFileName = addendaName;
            IsAddendaComparison = true;
            currentSummary = summary;

            comparisonRows.Clear();
            foreach (var row in summary.Rows)
                comparisonRows.Add(new VersionComparisonRowVm(row, CurrencyFormat));

            SelectedComparisonRow = comparisonRows.FirstOrDefault();

            ComparisonSummaryText = $"{summary.Rows.Count:N0} partidas analizadas";
            ComparisonStatusText = $"Semáforo de Aclaración / Addenda: {summary.BaselineLabel} vs {summary.CurrentLabel}";
            ComparisonTotalsText = BuildTotalsText(summary, "Addenda");
            ComparisonAddedText = $"{summary.AddedCount:N0} adicionales";
            ComparisonChangedText = $"{summary.ChangedCount:N0} con cambio de metraje";
            ComparisonRemovedText = $"{summary.RemovedCount:N0} deductivos";
            ComparisonUnchangedText = $"{summary.UnchangedCount:N0} sin cambio";
            (ComparisonAdicionalesText, ComparisonDeductivosText, ComparisonNetVariationText) = BuildVariationTexts(summary);
            ComparisonDeltaUf = summary.DeltaUf;
            ComparisonDeltaPct = summary.DeltaPct;
            ComparisonModeHint = $"Addenda entrante: «{addendaName}» · Semáforo activado";

            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            exportPdfCommand.RaiseCanExecuteChanged();
            exportExcelCommand.RaiseCanExecuteChanged();
            createChangeOrderFromDiffCommand.RaiseCanExecuteChanged();
            applyAddendaQuantitiesCommand.RaiseCanExecuteChanged();
            saveAddendaAsVersionCommand.RaiseCanExecuteChanged();
            comparisonRowsView.Refresh();

            int mayoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad > 0);
            int menoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad < 0);

            WpfDialogService.Info(
                "Addenda Analizada con Éxito",
                $"Se ha cargado y contrastado «{addendaName}» contra el presupuesto activo:\n\n" +
                $"• 🟢 Partidas Nuevas / Adicionales: {summary.AddedCount} partidas\n" +
                $"• 🟢 Aumentos de Metraje: {mayoresMetrajes} partidas\n" +
                $"• 🔴 Deductivos / Omitidas: {summary.RemovedCount} partidas\n" +
                $"• 🔴 Reducciones de Metraje: {menoresMetrajes} partidas\n" +
                $"• ⚡ Saldo Neto Variación: {CurrencyFormat.FromUfSigned(summary.NetVariationUf)} ({(summary.NetVariationClp >= 0 ? "+$ " : "$ ")}{summary.NetVariationClp:N0})\n\n" +
                $"Use los filtros del semáforo para auditar los cambios o presione «⚡ Aplicar Cantidades de Addenda» para incorporar las modificaciones al proyecto.");
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al procesar Addenda", $"Ocurrió una excepción al leer o comparar la Addenda:\n{ex.Message}");
        }
    }

    private void ApplyAddendaQuantities()
    {
        if (!HasProject || session.IsReadOnly || lastIncomingAddendaItems == null || currentSummary == null)
            return;

        if (!WpfDialogService.Confirm(
            "Aplicar Cantidades de Addenda",
            $"¿Desea aplicar las cantidades de la Addenda «{AddendaFileName}» al presupuesto activo?\n\n" +
            $"• Se actualizarán los metrajes de las partidas coincidentes.\n" +
            $"• Se añadirán al itemizado las partidas nuevas.\n" +
            $"• Se creará automáticamente una versión de respaldo previa.",
            isWarning: true))
            return;

        try
        {
            // 1. Guardar versión de respaldo previa
            int nextNum = session.Versiones.Count + 1;
            var backupSnapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
            decimal ventaUf = dashboard.BuildPageState().Cost.TotalVentaUf;
            var backupVersion = new BudgetVersionRow
            {
                Codigo = $"V{nextNum}",
                Nombre = $"Pre-Addenda ({AddendaFileName})",
                Estado = "Congelada",
                CreadoPor = session.ProjectInfo.UsuarioNombre,
                FechaCreacion = AppClock.Now,
                FechaModificacion = AppClock.Now,
                MontoVentaUf = ventaUf,
                Snapshot = backupSnapshot
            };
            session.Versiones.Add(backupVersion);

            // 2. Aplicar cantidades a session.Itemizado
            var itemByCode = session.Itemizado
                .Where(x => !string.IsNullOrWhiteSpace(x.Item))
                .ToDictionary(x => x.Item.Trim(), x => x, StringComparer.OrdinalIgnoreCase);

            var itemByDesc = new Dictionary<string, ItemizadoRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in session.Itemizado)
            {
                string norm = NormalizeText(it.Descripcion);
                if (!string.IsNullOrWhiteSpace(norm) && !itemByDesc.ContainsKey(norm))
                    itemByDesc[norm] = it;
            }

            int updatedCount = 0;
            int insertedCount = 0;

            foreach (var inc in lastIncomingAddendaItems)
            {
                if (inc.EsTitulo) continue;

                string code = inc.Item?.Trim() ?? string.Empty;
                string desc = inc.Descripcion?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(desc)) continue;

                decimal newQty = inc.Cantidad > 0m ? inc.Cantidad : (inc.CantidadOriginal > 0m ? inc.CantidadOriginal : 1m);

                ItemizadoRow? target = null;
                if (!string.IsNullOrWhiteSpace(code) && itemByCode.TryGetValue(code, out var byCode))
                {
                    target = byCode;
                }
                else
                {
                    string norm = NormalizeText(desc);
                    if (!string.IsNullOrWhiteSpace(norm) && itemByDesc.TryGetValue(norm, out var byDesc))
                        target = byDesc;
                }

                if (target != null)
                {
                    target.CantidadOriginal = newQty;
                    target.Cantidad = newQty;
                    updatedCount++;
                }
                else
                {
                    // Nueva partida en la addenda
                    var newRow = new ItemizadoRow
                    {
                        Id = Guid.NewGuid().ToString(),
                        PartidaId = Guid.NewGuid(),
                        Item = !string.IsNullOrWhiteSpace(code) ? code : $"{session.Itemizado.Count + 1}",
                        Descripcion = !string.IsNullOrWhiteSpace(desc) ? desc : "Partida nueva Addenda",
                        Unidad = !string.IsNullOrWhiteSpace(inc.Unidad) ? inc.Unidad : "GL",
                        CantidadOriginal = newQty,
                        Cantidad = newQty,
                        Observaciones = $"Incorporada desde Addenda {AddendaFileName}"
                    };
                    session.Itemizado.Add(newRow);
                    insertedCount++;
                }
            }

            session.MarkDirty(ProjectDirtyScope.Itemizado, ProjectDirtyScope.Versiones);

            WpfDialogService.Info(
                "Cantidades de Addenda Aplicadas",
                $"Se han aplicado exitosamente los cambios de la Addenda:\n\n" +
                $"• Partidas actualizadas: {updatedCount}\n" +
                $"• Partidas nuevas incorporadas: {insertedCount}\n" +
                $"• Respaldo creado: «{backupVersion.Codigo} — {backupVersion.Nombre}»");

            Refresh();
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al aplicar Addenda", $"No se pudieron aplicar las cantidades de la Addenda:\n{ex.Message}");
        }
    }

    private void SaveAddendaAsVersion()
    {
        if (!HasProject || session.IsReadOnly || currentSummary == null)
            return;

        int nextNum = session.Versiones.Count + 1;
        string codigo = $"V{nextNum}_ADD";
        string nombre = $"Addenda {AddendaFileName}";

        if (!PromptVersionDialog("Guardar Addenda como Versión", ref codigo, ref nombre))
            return;

        var snapshot = BudgetVersionData.FromProjectFileData(session.CreateSnapshot());
        decimal ventaUf = dashboard.BuildPageState().Cost.TotalVentaUf;
        var version = new BudgetVersionRow
        {
            Codigo = string.IsNullOrWhiteSpace(codigo) ? $"V{nextNum}_ADD" : codigo.Trim(),
            Nombre = string.IsNullOrWhiteSpace(nombre) ? $"Addenda {AddendaFileName}" : nombre.Trim(),
            Estado = "Addenda",
            CreadoPor = session.ProjectInfo.UsuarioNombre,
            FechaCreacion = AppClock.Now,
            FechaModificacion = AppClock.Now,
            MontoVentaUf = ventaUf,
            Snapshot = snapshot
        };

        session.Versiones.Add(version);
        session.ActiveVersionId = version.Id;
        session.MarkDirty(ProjectDirtyScope.Versiones);
        Refresh();

        WpfDialogService.Info("Versión de Addenda guardada", $"La versión «{version.Codigo} — {version.Nombre}» ha sido registrada en el historial.");
    }

    private static string? PromptOpenFileDirect()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Cargar Aclaración o Addenda de Licitación (Excel)",
            Filter = "Libros de Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Todos los archivos (*.*)|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var chars = text.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }
}
