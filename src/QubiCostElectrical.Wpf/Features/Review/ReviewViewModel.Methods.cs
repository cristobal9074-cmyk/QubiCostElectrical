using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Review;

public sealed partial class ReviewViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    // Compat: se elimina cuando se complete la migración en tests.
    public ReviewViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.FinalReview) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ReviewViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IFinalReviewService finalReview) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.finalReview = finalReview;

        refreshCommand = new RelayCommand(Refresh);
        fixEquipmentClassificationCommand = new RelayCommand(FixEquipmentClassification, CanFixEquipmentClassification);
        goToIssueCommand = new RelayCommand(GoToSelectedIssue, CanGoToSelectedIssue);
        resetFilterCommand = new RelayCommand(ResetFilters, () => HasActiveFilters);
        clearSearchCommand = new RelayCommand(ClearSearch, () => HasSearchText);
        nextWarRoomIssueCommand = new RelayCommand(NextWarRoomIssue, () => GetWarRoomIssues().Count > 0);
        previousWarRoomIssueCommand = new RelayCommand(PreviousWarRoomIssue, () => GetWarRoomIssues().Count > 0);
        markResolvedLocalCommand = new RelayCommand(MarkResolvedLocal, () => FocusIssue != null);
        GoToExportCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.OfferDocuments));
        GoToBudgetCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized));
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        issuesView = CollectionViewSource.GetDefaultView(issues);
        issuesView.Filter = FilterIssue;
        issuesView.SortDescriptions.Add(new SortDescription(nameof(ReviewIssueVm.Severity), ListSortDirection.Ascending));
        issuesView.SortDescriptions.Add(new SortDescription(nameof(ReviewIssueVm.ItemId), ListSortDirection.Ascending));
        issuesView.SortDescriptions.Add(new SortDescription(nameof(ReviewIssueVm.Codigo), ListSortDirection.Ascending));

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        ScheduleSessionRefresh();

    private void ScheduleSessionRefresh()
    {
        sessionRefreshDebounceTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        sessionRefreshDebounceTimer.Stop();
        sessionRefreshDebounceTimer.Tick -= OnSessionRefreshDebounceTick;
        sessionRefreshDebounceTimer.Tick += OnSessionRefreshDebounceTick;
        sessionRefreshDebounceTimer.Start();
    }

    private void OnSessionRefreshDebounceTick(object? sender, EventArgs e)
    {
        sessionRefreshDebounceTimer?.Stop();
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);
    }

    protected override void DisposeCore()
    {
        if (sessionRefreshDebounceTimer != null)
        {
            sessionRefreshDebounceTimer.Stop();
            sessionRefreshDebounceTimer.Tick -= OnSessionRefreshDebounceTick;
            sessionRefreshDebounceTimer = null;
        }
    }

    public ObservableCollection<ReviewIssueVm> Issues => issues;
    public ICollectionView FilteredIssues => issuesView;
    public ObservableCollection<ChecklistItemVm> Checklist => checklist;
    public ObservableCollection<string> Blockers => blockers;
    public ObservableCollection<string> Criticals => criticals;
    public ObservableCollection<string> Warnings => warnings;
    public ObservableCollection<string> Infos => infos;
    public ReviewIssueVm? SelectedIssue
    {
        get => selectedIssue;
        set
        {
            if (!SetProperty(ref selectedIssue, value))
                return;
            goToIssueCommand.RaiseCanExecuteChanged();
            if (value != null && !locallyResolvedIds.Contains(value.Source.Id))
                SetFocusIssue(value);
        }
    }

    public ReviewIssueVm? FocusIssue
    {
        get => focusIssue;
        private set
        {
            if (!SetProperty(ref focusIssue, value))
                return;
            OnPropertyChanged(nameof(FocusIssueSummary));
            OnPropertyChanged(nameof(WarRoomProgressText));
            OnPropertyChanged(nameof(IsFocusBlocker));
            OnPropertyChanged(nameof(IsFocusCritical));
            OnPropertyChanged(nameof(IsFocusWarning));
            OnPropertyChanged(nameof(IsFocusInfo));
            markResolvedLocalCommand.RaiseCanExecuteChanged();
            nextWarRoomIssueCommand.RaiseCanExecuteChanged();
            previousWarRoomIssueCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsFocusBlocker => FocusIssue?.IsBlocker ?? false;
    public bool IsFocusCritical => FocusIssue?.IsCritical ?? false;
    public bool IsFocusWarning => FocusIssue?.IsWarning ?? false;
    public bool IsFocusInfo => FocusIssue?.IsInfo ?? false;

    public bool IsWarRoomMode
    {
        get => isWarRoomMode;
        set
        {
            if (!SetProperty(ref isWarRoomMode, value))
                return;
            SyncFocusIssue();
        }
    }

    public string FocusIssueSummary =>
        FocusIssue == null
            ? "Sin alertas pendientes en esta sesi\u00f3n."
            : $"{FocusIssue.SeverityText} · {FocusIssue.CategoryText} — {FocusIssue.Problem}";

    public string WarRoomProgressText
    {
        get
        {
            var list = GetWarRoomIssues();
            if (list.Count == 0)
                return "0/0";
            int index = FocusIssue == null ? 0 : list.FindIndex(x => ReferenceEquals(x, FocusIssue)) + 1;
            if (index <= 0)
                index = 1;
            return $"{index}/{list.Count}";
        }
    }

    public IReadOnlyList<string> SeverityOptions => SeverityOptionsInternal;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(HasActiveFilters));
            clearSearchCommand.RaiseCanExecuteChanged();
            resetFilterCommand.RaiseCanExecuteChanged();
            issuesView.Refresh();
            UpdateSummary();
        }
    }

    public string SelectedSeverity
    {
        get => selectedSeverity;
        set
        {
            if (!SetProperty(ref selectedSeverity, value))
                return;
            OnPropertyChanged(nameof(HasActiveFilters));
            resetFilterCommand.RaiseCanExecuteChanged();
            issuesView.Refresh();
            UpdateSummary();
        }
    }

    public string FilterLabel
    {
        get => filterLabel;
        private set => SetProperty(ref filterLabel, value);
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(searchText);
    public bool HasActiveFilter => activeCategoryFilter.HasValue;
    public bool HasActiveFilters => HasSearchText || !string.Equals(selectedSeverity, "Todas", StringComparison.OrdinalIgnoreCase) || activeCategoryFilter.HasValue;
    public int BlockerCount => Math.Max(0, issues.Count(x => x.Severity == FinalReviewSeverity.Blocker && !locallyResolvedIds.Contains(x.Source.Id)));
    public bool HasZeroIssues => issues.Count == 0;

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

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ReadinessMessage
    {
        get => readinessMessage;
        private set => SetProperty(ref readinessMessage, value);
    }

    public string VisibleIssuesLabel
    {
        get => visibleIssuesLabel;
        private set => SetProperty(ref visibleIssuesLabel, value);
    }

    public bool ReadyToExport
    {
        get => readyToExport;
        private set => SetProperty(ref readyToExport, value);
    }

    public string ExportStateText => ReadyToExport ? "Listo" : "Bloqueado";
    public string ConfidenceLabel => ConfidencePct >= 85 ? "Alta confianza" : ConfidencePct >= 60 ? "Confianza media" : "Baja confianza";

    public decimal ConfidencePct
    {
        get => confidencePct;
        private set => SetProperty(ref confidencePct, value);
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand FixEquipmentClassificationCommand => fixEquipmentClassificationCommand;
    public ICommand GoToIssueCommand => goToIssueCommand;
    public ICommand ResetFilterCommand => resetFilterCommand;
    public ICommand ClearSearchCommand => clearSearchCommand;
    public ICommand GoToExportCommand { get; }
    public ICommand GoToBudgetCommand { get; }
    public ICommand OpenProjectCommand { get; }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public ICommand NextWarRoomIssueCommand => nextWarRoomIssueCommand;
    public ICommand PreviousWarRoomIssueCommand => previousWarRoomIssueCommand;
    public ICommand MarkResolvedLocalCommand => markResolvedLocalCommand;

    private bool CanFixEquipmentClassification()
        => workspace.HasOpenProject
            && !session.IsReadOnly
            && new ClasificacionEquiposService().Detectar(session.Cubicacion).Count > 0;

    private void FixEquipmentClassification()
    {
        var servicio = new ClasificacionEquiposService();
        var detectadas = servicio.Detectar(session.Cubicacion);
        if (detectadas.Count == 0)
            return;

        var costo = servicio.CostoDetectado(session.Cubicacion);
        var confirmado = WpfDialogService.Confirm(
            "Corregir clasificación de equipos",
            $"Se marcarán como EQUIPO {detectadas.Count} línea(s) de familia Tableros/Equipos que siguen como material.\n\n" +
            $"Afecta {costo:N0} CLP de costo: la ferretería se recalcula solo sobre materiales, la base de gastos generales y utilidad baja y se aplica el valor agregado sobre equipos.\n\n" +
            "¿Continuar?",
            isWarning: true);
        if (!confirmado)
            return;

        var corregidas = servicio.Corregir(session.Cubicacion);
        session.MarkDirty(ProjectDirtyScope.Cubicacion);
        Refresh();
        StatusMessage = $"{corregidas} línea(s) marcadas como equipo.";
    }

    private void SetCategoryFilter(FinalReviewCategory? category)
    {
        activeCategoryFilter = category;
        FilterLabel = category.HasValue
            ? FinalReviewIssueExtensions.CategoryTextFor(category.Value)
            : "Todas las categor\u00edas";
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(HasActiveFilters));
        resetFilterCommand.RaiseCanExecuteChanged();
        issuesView.Refresh();
        UpdateSummary();
    }

    private void ClearCategoryFilter()
    {
        SetCategoryFilter(null);
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void ResetFilters()
    {
        searchText = string.Empty;
        selectedSeverity = "Todas";
        activeCategoryFilter = null;
        filterLabel = "Todas las categor\u00edas";
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedSeverity));
        OnPropertyChanged(nameof(FilterLabel));
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(HasActiveFilters));
        clearSearchCommand.RaiseCanExecuteChanged();
        resetFilterCommand.RaiseCanExecuteChanged();
        issuesView.Refresh();
        UpdateSummary();
    }

    public void Refresh()
    {
        IsLoading = true;
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);

            ShowEmptyState = !workspace.HasOpenProject;

            string? selectedIssueId = SelectedIssue?.Source.Id;
            string? focusIssueId = FocusIssue?.Source.Id;
            var summary = finalReview.Build(
                session.Itemizado,
                session.Materiales,
                session.Cubicacion,
                session.Apus,
                session.ProjectInfo,
                ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(session.ProjectInfo), session.Data),
                session.OrdenesCambio,
                session.AntecedentesTecnicos,
                session.Mediciones);

            issues.Clear();
            foreach (var issue in summary.Issues)
                issues.Add(new ReviewIssueVm(issue));

            // Mantener resolución local solo para IDs que siguen existiendo.
            locallyResolvedIds.RemoveWhere(id => issues.All(x => !string.Equals(x.Source.Id, id, StringComparison.OrdinalIgnoreCase)));

            issuesView.Refresh();

            SelectedIssue = issues.FirstOrDefault(x => string.Equals(x.Source.Id, selectedIssueId, StringComparison.OrdinalIgnoreCase))
                ?? issues.FirstOrDefault(x => !locallyResolvedIds.Contains(x.Source.Id));

            IsWarRoomMode = summary.BlockingCount > 0 || isWarRoomMode;
            SyncFocusIssue(focusIssueId);

            BuildCollections(summary);
            UpdateProjectSummary();
            UpdateSummary();
            fixEquipmentClassificationCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildCollections(FinalReviewSummary summary)
    {
        bool hasProject = workspace.HasOpenProject;

        checklist.Clear();
        checklist.Add(new ChecklistItemVm(
            summary.BlockingCount == 0 ? "Sin bloqueos" : $"{summary.BlockingCount:N0} bloqueantes",
            summary.BlockingCount == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            summary.BlockingCount, null,
            _ => { }, () => hasProject));
        checklist.Add(new ChecklistItemVm(
            summary.FormulaErrors == 0 ? "F\u00f3rmulas \u00edntegras" : $"{summary.FormulaErrors:N0} f\u00f3rmula(s) inv\u00e1lida(s)",
            summary.FormulaErrors == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            summary.FormulaErrors, FinalReviewCategory.FormulaSyntaxError,
            SetCategoryFilter, () => hasProject));
        checklist.Add(new ChecklistItemVm(
            summary.EmptyPartidas == 0 ? "Partidas completas" : $"{summary.EmptyPartidas:N0} partidas aún sin cubicación",
            summary.EmptyPartidas == 0 ? ChecklistStatus.Pass : ChecklistStatus.Warning,
            summary.EmptyPartidas, FinalReviewCategory.EmptyPartida,
            SetCategoryFilter, () => hasProject));
        int zeroQuantities = summary.Issues.Count(x => x.Category == FinalReviewCategory.ZeroQuantity);
        checklist.Add(new ChecklistItemVm(
            zeroQuantities == 0 ? "Cubicaciones con metraje válido" : $"{zeroQuantities:N0} partida(s) con metraje cero",
            zeroQuantities == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            zeroQuantities, FinalReviewCategory.ZeroQuantity,
            SetCategoryFilter, () => hasProject));
        int sinValorizacionGlobal = summary.Issues.Count(x => x.Category == FinalReviewCategory.PartidaSinValorizacionGlobal);
        checklist.Add(new ChecklistItemVm(
            sinValorizacionGlobal == 0 ? "Partidas valorizadas globalmente" : $"{sinValorizacionGlobal:N0} partida(s) sin unidad o cant. global",
            sinValorizacionGlobal == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            sinValorizacionGlobal, FinalReviewCategory.PartidaSinValorizacionGlobal,
            SetCategoryFilter, () => hasProject));
        checklist.Add(new ChecklistItemVm(
            summary.MaterialsWithoutPrice == 0 ? "Precios completos" : $"{summary.MaterialsWithoutPrice:N0} materiales sin precio",
            summary.MaterialsWithoutPrice == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            summary.MaterialsWithoutPrice, FinalReviewCategory.MaterialWithoutPrice,
            SetCategoryFilter, () => hasProject));
        var issueFamilias = summary.Issues.FirstOrDefault(x => x.Category == FinalReviewCategory.FamiliaSinFactorPresentacion);
        int familiasSinFactor = issueFamilias?.Count ?? 0;
        checklist.Add(new ChecklistItemVm(
            familiasSinFactor == 0 ? "Factores de presentación definidos" : $"{familiasSinFactor:N0} familia(s) sin factor de presentación",
            familiasSinFactor == 0 ? ChecklistStatus.Pass : ChecklistStatus.Warning,
            familiasSinFactor, FinalReviewCategory.FamiliaSinFactorPresentacion,
            SetCategoryFilter, () => hasProject));
        var issueEquipos = summary.Issues.FirstOrDefault(x => x.Category == FinalReviewCategory.EquipoSinClasificar);
        int equiposSinClasificar = issueEquipos?.Count ?? 0;
        checklist.Add(new ChecklistItemVm(
            equiposSinClasificar == 0 ? "Equipos clasificados" : $"{equiposSinClasificar:N0} línea(s) de equipo como material",
            equiposSinClasificar == 0 ? ChecklistStatus.Pass : ChecklistStatus.Warning,
            equiposSinClasificar, FinalReviewCategory.EquipoSinClasificar,
            SetCategoryFilter, () => hasProject));
        int incompleteCommercialModels = summary.Issues.Count(x => x.Category == FinalReviewCategory.CommercialModelIncomplete);
        checklist.Add(new ChecklistItemVm(
            incompleteCommercialModels == 0 ? "Modelos comerciales completos" : $"{incompleteCommercialModels:N0} modelo(s) comercial(es) incompleto(s)",
            incompleteCommercialModels == 0 ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            incompleteCommercialModels, FinalReviewCategory.CommercialModelIncomplete,
            SetCategoryFilter, () => hasProject));
        int apuPorRevisar = summary.Issues.Count(x => x.Category == FinalReviewCategory.ApuIncomplete);
        checklist.Add(new ChecklistItemVm(
            apuPorRevisar == 0
                ? "APU opcional (herramienta o lista de precios)"
                : $"{apuPorRevisar:N0} APU con precio por revisar",
            apuPorRevisar == 0 ? ChecklistStatus.Pass : ChecklistStatus.Warning,
            apuPorRevisar, FinalReviewCategory.ApuIncomplete,
            SetCategoryFilter, () => hasProject));
        checklist.Add(new ChecklistItemVm(
            summary.AreasNotAssigned == 0 ? "\u00c1reas asignadas" : $"{summary.AreasNotAssigned:N0} l\u00edneas sin \u00e1rea",
            summary.AreasNotAssigned == 0 ? ChecklistStatus.Pass : ChecklistStatus.Warning,
            summary.AreasNotAssigned, FinalReviewCategory.AreaNotAssigned,
            SetCategoryFilter, () => hasProject));
        checklist.Add(new ChecklistItemVm(
            summary.ReadyToExport ? "Listo para exportar" : "Bloqueado para exportar",
            summary.ReadyToExport ? ChecklistStatus.Pass : ChecklistStatus.Fail,
            summary.BlockingCount, null,
            _ => { }, () => hasProject));

        blockers.Clear();
        criticals.Clear();
        warnings.Clear();
        infos.Clear();

        foreach (var issue in summary.Issues.Where(x => x.Severity == FinalReviewSeverity.Blocker).Take(8))
            blockers.Add($"\u2022 {issue.CategoryText} \u2014 {issue.Problem}");
        foreach (var issue in summary.Issues.Where(x => x.Severity == FinalReviewSeverity.Critical).Take(8))
            criticals.Add($"\u2022 {issue.CategoryText} \u2014 {issue.Problem}");
        foreach (var issue in summary.Issues.Where(x => x.Severity == FinalReviewSeverity.Warning).Take(8))
            warnings.Add($"\u2022 {issue.CategoryText} \u2014 {issue.Problem}");
        foreach (var issue in summary.Issues.Where(x => x.Severity == FinalReviewSeverity.Info).Take(8))
            infos.Add($"\u2022 {issue.CategoryText} \u2014 {issue.Problem}");

        ReadyToExport = summary.ReadyToExport;
        ConfidencePct = summary.ConfidencePct;
        OnPropertyChanged(nameof(ExportStateText));
        OnPropertyChanged(nameof(ConfidenceLabel));
    }

    private void UpdateProjectSummary()
    {
        ProjectSummary = workspace.HasOpenProject
            ? $"{session.ProjectInfo.Nombre} \u2014 {session.Itemizado.Count(x => !x.EsTitulo):N0} partidas \u2014 {session.ProjectInfo.CommercialModel.DisplayName()}"
            : "Sin proyecto abierto";
    }

    private void UpdateSummary()
    {
        int visible = issuesView.Cast<object>().Count();
        int blockersCount = Math.Max(0, issues.Count(x => x.Severity == FinalReviewSeverity.Blocker && !locallyResolvedIds.Contains(x.Source.Id)));

        VisibleIssuesLabel = $"{visible:N0} visibles";
        SummaryText = $"{visible:N0} visibles \u2014 {issues.Count:N0} total \u2014 {blockersCount:N0} bloqueantes";
        StatusMessage = ReadyToExport
            ? "La revisi\u00f3n est\u00e1 lista para exportar."
            : "Hay bloqueos que deben corregirse antes de exportar.";
        ReadinessMessage = ReadyToExport
            ? "El presupuesto ya tiene una revisi\u00f3n operativa aceptable para exportar."
            : "A\u00fan hay puntos cr\u00edticos que conviene corregir antes de exportar.";
        OnPropertyChanged(nameof(WarRoomProgressText));
        OnPropertyChanged(nameof(FocusIssueSummary));
        OnPropertyChanged(nameof(BlockerCount));
        OnPropertyChanged(nameof(HasZeroIssues));
        OnPropertyChanged(nameof(HasActiveFilters));
        resetFilterCommand.RaiseCanExecuteChanged();
    }

    private bool FilterIssue(object obj)
    {
        if (obj is not ReviewIssueVm issue)
            return false;

        if (locallyResolvedIds.Contains(issue.Source.Id))
            return false;

        if (!string.Equals(SelectedSeverity, "Todas", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(issue.SeverityText, SelectedSeverity, StringComparison.OrdinalIgnoreCase))
            return false;

        if (activeCategoryFilter.HasValue && issue.Source.Category != activeCategoryFilter.Value)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        string query = Normalize(SearchText);
        string searchable = Normalize(string.Join(' ', new[]
        {
            issue.SeverityText,
            issue.CategoryText,
            issue.ItemId,
            issue.Item,
            issue.Partida,
            issue.Codigo,
            issue.Descripcion,
            issue.Problem,
            issue.Recommendation
        }));

        return searchable.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();

    private bool CanGoToSelectedIssue() => SelectedIssue != null || FocusIssue != null;

    private void GoToSelectedIssue()
    {
        var issue = (SelectedIssue ?? FocusIssue)?.Source;
        if (issue == null)
            return;

        var target = FinalReviewRouteResolver.Resolve(issue.Category);
        var route = ShellRouteMapper.FromReviewTarget(target);

        if (route == ShellRoute.Takeoff)
        {
            shell.RequestNavigate(route, new TakeoffNavigationContext(issue.ItemId, issue.Codigo, issue.Descripcion));
            return;
        }

        if (route == ShellRoute.Apu)
        {
            shell.RequestNavigate(route, new ApuNavContext(issue.ItemId, issue.Codigo, issue.Descripcion));
            return;
        }

        if (route == ShellRoute.Prices)
        {
            shell.RequestNavigate(route, new PriceBankNavigationContext(issue.Codigo));
            return;
        }

        shell.RequestNavigate(route);
    }

    private List<ReviewIssueVm> GetWarRoomIssues()
    {
        return issues
            .Where(x => !locallyResolvedIds.Contains(x.Source.Id))
            .OrderBy(x => x.Severity)
            .ThenBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SyncFocusIssue(string? preferredId = null)
    {
        var list = GetWarRoomIssues();
        if (list.Count == 0)
        {
            FocusIssue = null;
            return;
        }

        ReviewIssueVm? next = null;
        if (!string.IsNullOrWhiteSpace(preferredId))
            next = list.FirstOrDefault(x => string.Equals(x.Source.Id, preferredId, StringComparison.OrdinalIgnoreCase));

        next ??= list.FirstOrDefault(x => x.Severity == FinalReviewSeverity.Blocker)
                 ?? SelectedIssue
                 ?? list[0];

        if (next != null && !list.Contains(next))
            next = list[0];

        SetFocusIssue(next);
    }

    private void SetFocusIssue(ReviewIssueVm? issue)
    {
        FocusIssue = issue;
        if (issue != null && !ReferenceEquals(SelectedIssue, issue))
            selectedIssue = issue;
        OnPropertyChanged(nameof(SelectedIssue));
        goToIssueCommand.RaiseCanExecuteChanged();
    }

    private void NextWarRoomIssue()
    {
        var list = GetWarRoomIssues();
        if (list.Count == 0)
            return;

        int index = FocusIssue == null ? -1 : list.FindIndex(x => ReferenceEquals(x, FocusIssue));
        SetFocusIssue(list[(index + 1) % list.Count]);
    }

    private void PreviousWarRoomIssue()
    {
        var list = GetWarRoomIssues();
        if (list.Count == 0)
            return;

        int index = FocusIssue == null ? 0 : list.FindIndex(x => ReferenceEquals(x, FocusIssue));
        if (index < 0)
            index = 0;
        SetFocusIssue(list[(index - 1 + list.Count) % list.Count]);
    }

    private void MarkResolvedLocal()
    {
        if (FocusIssue == null)
            return;

        locallyResolvedIds.Add(FocusIssue.Source.Id);
        issuesView.Refresh();
        SyncFocusIssue();
        UpdateSummary();
    }
}


