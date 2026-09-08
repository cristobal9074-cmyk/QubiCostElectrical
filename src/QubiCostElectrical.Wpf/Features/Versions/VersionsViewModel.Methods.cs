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
        : this(shell, host.Session, host.ProjectWorkspace, host.Dashboard, host.Services.VersionComparison) { }

    public VersionsViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, DashboardPresenter dashboard, IVersionComparisonService versionComparison) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.dashboard = dashboard;
        this.versionComparison = versionComparison;

        refreshCommand = new RelayCommand(Refresh);
        newCommand = new RelayCommand(CreateVersion, () => HasProject && !session.IsReadOnly);
        activateCommand = new RelayCommand(ActivateVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        renameCommand = new RelayCommand(RenameVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        deleteCommand = new RelayCommand(DeleteVersion, () => HasProject && !session.IsReadOnly && selectedVersion != null);
        refreshComparisonCommand = new RelayCommand(RefreshComparison);
        goToItemCommand = new RelayCommand(GoToItem, () => selectedComparisonRow != null);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        versionsView = CollectionViewSource.GetDefaultView(versions);
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.FechaCreacion), ListSortDirection.Descending));
        versionsView.SortDescriptions.Add(new SortDescription(nameof(VersionRowVm.Codigo), ListSortDirection.Ascending));

        comparisonRowsView = CollectionViewSource.GetDefaultView(comparisonRows);
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.DeltaUf), ListSortDirection.Descending));
        comparisonRowsView.SortDescriptions.Add(new SortDescription(nameof(VersionComparisonRowVm.Item), ListSortDirection.Ascending));

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);

    public bool HasProject => workspace.HasOpenProject;

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

    public decimal ComparisonDeltaUf
    {
        get => comparisonDeltaUf;
        private set => SetProperty(ref comparisonDeltaUf, value);
    }

    public decimal ComparisonDeltaPct
    {
        get => comparisonDeltaPct;
        private set => SetProperty(ref comparisonDeltaPct, value);
    }

    // DeltaUf contiene CLP desde la migración (nombre legado)
    public string ComparisonDeltaText => ComparisonDeltaUf == 0m ? "—"
        : $"{(ComparisonDeltaUf > 0m ? "+" : string.Empty)}$ {ComparisonDeltaUf:N0}";
    public string ComparisonDeltaPctText => ComparisonDeltaPct == 0m ? "—"
        : $"{(ComparisonDeltaPct > 0m ? "+" : string.Empty)}{ComparisonDeltaPct:N2}%";

    public string ComparisonModeHint
    {
        get => comparisonModeHint;
        private set => SetProperty(ref comparisonModeHint, value);
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand NewCommand => newCommand;
    public ICommand ActivateCommand => activateCommand;
    public ICommand RenameCommand => renameCommand;
    public ICommand DeleteCommand => deleteCommand;
    public ICommand RefreshComparisonCommand => refreshComparisonCommand;
    public ICommand GoToItemCommand => goToItemCommand;
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

            versions.Add(new VersionRowVm(current, isActive, delta));
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

    private void RefreshComparison()
    {
        comparisonRows.Clear();

        var selected = SelectedVersion?.Source;
        if (selected == null)
        {
            ComparisonSummaryText = "Sin comparación";
            ComparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
            ComparisonTotalsText = "—";
            ComparisonAddedText = "0 nuevas";
            ComparisonChangedText = "0 modificadas";
            ComparisonRemovedText = "0 eliminadas";
            ComparisonDeltaUf = 0m;
            ComparisonDeltaPct = 0m;
            ComparisonModeHint = "Selecciona una versión para comparar.";
            OnPropertyChanged(nameof(ComparisonDeltaText));
            OnPropertyChanged(nameof(ComparisonDeltaPctText));
            SelectedComparisonRow = null;
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
                ComparisonSummaryText = "Sin comparación";
                ComparisonStatusText = "Selecciona una segunda versión para comparar.";
                ComparisonTotalsText = "—";
                ComparisonAddedText = "0 nuevas";
                ComparisonChangedText = "0 modificadas";
                ComparisonRemovedText = "0 eliminadas";
                ComparisonDeltaUf = 0m;
                ComparisonDeltaPct = 0m;
                ComparisonModeHint = "Selecciona la versión B para comparar A vs B.";
                OnPropertyChanged(nameof(ComparisonDeltaText));
                OnPropertyChanged(nameof(ComparisonDeltaPctText));
                SelectedComparisonRow = null;
                return;
            }

            summary = versionComparison.Compare(
                selected.Snapshot,
                comparison.Snapshot,
                baselineLabel,
                $"{comparison.Codigo} · {comparison.Nombre}");
        }

        foreach (var row in summary.Rows)
            comparisonRows.Add(new VersionComparisonRowVm(row));

        SelectedComparisonRow = comparisonRows.FirstOrDefault();

        ComparisonSummaryText = $"{summary.Rows.Count:N0} partidas comparadas";
        ComparisonStatusText = summary.Rows.Count == 0
            ? "No hay diferencias entre la versión seleccionada y el estado actual."
            : $"Comparando {summary.BaselineLabel} contra {summary.CurrentLabel}.";
        // BaselineUf/CurrentUf/DeltaUf contienen CLP desde la migración
        ComparisonTotalsText = $"Base $ {summary.BaselineUf:N0} · Actual $ {summary.CurrentUf:N0} · Delta $ {summary.DeltaUf:N0} ({summary.DeltaPct:N2}%)";
        ComparisonAddedText = $"{summary.AddedCount:N0} nuevas";
        ComparisonChangedText = $"{summary.ChangedCount:N0} modificadas";
        ComparisonRemovedText = $"{summary.RemovedCount:N0} eliminadas";
        ComparisonDeltaUf = summary.DeltaUf;
        ComparisonDeltaPct = summary.DeltaPct;
        ComparisonModeHint = CompareAgainstCurrent
            ? $"Versión A = {summary.BaselineLabel} · Versión B = estado actual"
            : $"Versión A = {summary.BaselineLabel} · Versión B = {summary.CurrentLabel}";
        OnPropertyChanged(nameof(ComparisonDeltaText));
        OnPropertyChanged(nameof(ComparisonDeltaPctText));
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
}
