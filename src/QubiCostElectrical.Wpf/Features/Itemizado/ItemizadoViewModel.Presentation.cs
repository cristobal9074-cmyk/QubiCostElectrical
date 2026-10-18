using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    private bool refreshRetryScheduled;
    private int refreshRetryCount;
    private const int MaxRefreshRetries = 25;

    // Refresca la vista de la grilla de forma segura ante transacciones abiertas de edición en DataGrid.
    // Incluye coalescencia de reintentos en dispatcher y límite MaxRefreshRetries contra bucles infinitos.
    private void SafeRefreshView()
    {
        if (rowsView == null)
            return;
        try
        {
            rowsView.Refresh();
            refreshRetryCount = 0;
        }
        catch (InvalidOperationException)
        {
            if (refreshRetryScheduled)
                return;

            if (++refreshRetryCount > MaxRefreshRetries)
            {
                AppLog.Warning("Itemizado",
                    "SafeRefreshView: se abandonó el refresco tras reintentos repetidos " +
                    "(la transacción de edición del DataGrid no llegó a cerrarse).");
                refreshRetryCount = 0;
                return;
            }

            refreshRetryScheduled = true;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                refreshRetryScheduled = false;
                SafeRefreshView();
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
    }

    private void ClearSearch()
    {
        searchText = "";
        debouncedSearchText = "";
        searchDebounceTimer?.Stop();
        UpdateSearchMatchHighlights();
        SafeRefreshView();
        UpdateFilterCounts();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(IsSearchActive));
    }

    private void OnSearchDebounceTick(object? sender, EventArgs e)
    {
        searchDebounceTimer?.Stop();
        if (debouncedSearchText == searchText)
            return;
        debouncedSearchText = searchText;
        UpdateSearchMatchHighlights();
        SafeRefreshView();
        EnsureSelectionVisible();
        UpdateFilterCounts();
        OnPropertyChanged(nameof(IsSearchActive));
    }

    private void UpdateSearchMatchHighlights()
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(debouncedSearchText);
        foreach (var row in allRows)
        {
            row.IsSearchMatch = hasSearch && (
                row.Item.Contains(debouncedSearchText, StringComparison.OrdinalIgnoreCase) ||
                row.Descripcion.Contains(debouncedSearchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        RunOnUiThread(() => OnSessionChangedCore(sender, e));

    private void OnSessionChangedCore(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        if (e.Scope is "role_access" or "lock_access")
            RaiseCommandStates();
        if (e.Kind == ProjectSessionChangeKind.Cubicacion)
            RefreshPresentation(includeCosts: true);
        else if (e.Kind is ProjectSessionChangeKind.Itemizado or ProjectSessionChangeKind.ProjectInfo)
        {
            if (e.Kind == ProjectSessionChangeKind.ProjectInfo)
                SyncCurrencyFromProject();
            RefreshAfterStructureChange();
        }
    }

    private void Reload(bool syncOnly)
    {
        IsLoading = true;
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);

            ShowEmptyState = !projectWorkspace.HasOpenProject;
            if (ShowEmptyState)
            {
                allRows.Clear();
                rowsById.Clear();
                Rows.Clear();
                ShowEmptyItemizado = false;
                return;
            }

            if (!syncOnly)
                SyncRowModels();

            var state = workspace.BuildPageState(presenter, session);
            ApplyPresentation(state, includeCosts: true, refreshView: true);
            ShowEmptyItemizado = session.Itemizado.Count == 0;
            UpdateHeader(state);
            UpdateSaveState();
            RestoreSelection();
            breadcrumbCoordinator.UpdateBreadcrumb(selectedRow, rowsById, allRows);
            OnPropertyChanged(nameof(HasBreadcrumb));
            UpdateSearchMatchHighlights();
            hasPendingUndoForEdit = false;
            OnPropertyChanged(nameof(IsReadOnly));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SyncRowModels()
    {
        InvalidateFilterPresentationCache();
        var sessionRows = session.Itemizado;
        var liveIds = new HashSet<string>(sessionRows.Select(x => x.Id ?? ""), StringComparer.OrdinalIgnoreCase);

        foreach (var id in rowsById.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            rowsById.Remove(id);
            var vm = allRows.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (vm != null)
            {
                UnwireRowVm(vm);
                allRows.Remove(vm);
                Rows.Remove(vm);
            }
        }

        foreach (var row in sessionRows)
        {
            string id = row.Id ?? "";
            if (rowsById.TryGetValue(id, out var existingVm))
            {
                existingVm.UpdateModel(row);
            }
            else
            {
                var vm = new ItemizadoRowVm(row, () => !session.IsReadOnly);
                rowsById[id] = vm;
                allRows.Add(vm);
                Rows.Add(vm);
            }
        }

        var rules = session.ProjectInfo.CommercialPartidaRules;
        var defaultModel = session.ProjectInfo.CommercialModel;
        foreach (var vm in allRows)
        {
            var rule = rules?.FirstOrDefault(r => r.PartidaId == vm.Model.PartidaId);
            vm.CommercialModel = (rule != null && !rule.UseProjectModel) ? rule.Model : defaultModel;
            vm.UsesCommercialOverride = rule != null && !rule.UseProjectModel;
            WireRowVm(vm);
        }

        SafeRefreshView();
    }

    private void WireRowVm(ItemizadoRowVm vm)
    {
        vm.PeersProvider = () => session.Itemizado;
        if (!wiredRows.Add(vm))
            return;

        vm.PropertyChanged += OnRowVmPropertyChanged;
        vm.BeforeFieldChanging += OnRowBeforeFieldChanging;
        vm.FieldCommitted += OnRowFieldCommitted;
    }

    private void UnwireRowVm(ItemizadoRowVm vm)
    {
        if (!wiredRows.Remove(vm))
            return;

        vm.PropertyChanged -= OnRowVmPropertyChanged;
        vm.BeforeFieldChanging -= OnRowBeforeFieldChanging;
        vm.FieldCommitted -= OnRowFieldCommitted;
    }

    private void OnRowVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemizadoRowVm.IsSelected))
            OnPropertyChanged(nameof(CubicarButtonLabel));
    }

    private void OnRowBeforeFieldChanging(ItemizadoRowVm row, string field)
    {
        if (session.IsReadOnly)
            return;

        string detail = !string.IsNullOrWhiteSpace(row.Item) ? $" {field} en Partida {row.Item}" : " itemizado";
        undo.PushUndoPoint(session, $"Modificar{detail}");
        hasPendingUndoForEdit = false;
    }

    private void OnRowFieldCommitted(ItemizadoRowVm row, string field)
    {
        if (session.IsReadOnly)
            return;

        session.MarkDirty(ProjectDirtyScope.Itemizado);
        UpdateSaveState();

        if (string.Equals(field, "Excl. cubic.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "Motivo no cubicar", StringComparison.OrdinalIgnoreCase))
        {
            row.ValidateEditableCells(session.Itemizado);
            RefreshPresentation(includeCosts: false);
        }
    }

    private void ReorderRowsFromSession()
    {
        var ordered = new List<ItemizadoRowVm>();
        foreach (var row in session.Itemizado)
        {
            if (rowsById.TryGetValue(row.Id ?? "", out var vm))
                ordered.Add(vm);
        }

        allRows.Clear();
        Rows.Clear();
        foreach (var vm in ordered)
        {
            allRows.Add(vm);
            Rows.Add(vm);
        }
    }

    private void RefreshAfterStructureChange()
    {
        if (ShowEmptyState) return;
        SyncRowModels();
        ReorderRowsFromSession();
        RefreshPresentation(includeCosts: true);
        SafeRefreshView();
        UpdateFilterCounts();
        ShowEmptyItemizado = session.Itemizado.Count == 0;
        RestoreSelection();
        RaiseCommandStates();
    }

    private void RefreshPresentation(bool includeCosts = true)
    {
        if (ShowEmptyState) return;
        var state = workspace.BuildPageState(presenter, session, includeCosts ? null : costTotalsByItem);
        ApplyPresentation(state, includeCosts, refreshView: false);
        UpdateHeader(state);
    }

    private void ApplyPresentation(ItemizadoPageState state, bool includeCosts, bool refreshView = false)
    {
        var rules = session.ProjectInfo.CommercialPartidaRules;
        var defaultModel = session.ProjectInfo.CommercialModel;
        var currencyCtx = ItemizadoCurrencyCoordinator.CreateContext(
            selectedDeliveryCurrency,
            session.ValorUfClp,
            session.ProjectInfo.TipoCambioUsdClp,
            session.ProjectInfo.TipoCambioEurClp);

        foreach (var presentation in state.Rows)
        {
            if (rowsById.TryGetValue(presentation.Id, out var vm))
            {
                var rule = rules?.FirstOrDefault(r => r.PartidaId == vm.Model.PartidaId);
                vm.CommercialModel = (rule != null && !rule.UseProjectModel) ? rule.Model : defaultModel;
                vm.UsesCommercialOverride = rule != null && !rule.UseProjectModel;
                vm.ApplyPresentation(presentation);
                vm.ApplyCurrencyContext(currencyCtx);
                vm.ValidateEditableCells();
            }
        }

        if (includeCosts)
        {
            costTotalsByItem.Clear();
            foreach (var presentation in state.Rows.Where(x => !x.EsTitulo))
                costTotalsByItem[presentation.Id] = presentation.TotalUf;
        }

        lastSummary = state.Summary;
        SummaryPartidas = state.Summary.TotalPartidas.ToString("N0");
        SummaryCompletas = state.Summary.Completas.ToString("N0");
        SummaryIncompletas = state.Summary.Incompletas.ToString("N0");
        SummaryCapitulos = state.Rows.Count(x => x.EsTitulo).ToString("N0");
        SummarySinPrecio = state.Summary.SinPrecio.ToString("N0");
        int pct = state.Summary.TotalPartidas > 0 ? (int)Math.Round(state.Summary.CompletedPct) : 0;
        SummaryStripText =
            $"{state.Summary.TotalPartidas:N0} partidas · {state.Summary.Completas:N0} completas · {pct}% listo";
        UpdateHudState((double)state.Summary.CompletedPct);
        ItemizadoCollapseCoordinator.UpdateHierarchy(allRows);
        InvalidateFilterPresentationCache();
        if (refreshView)
            SafeRefreshView();
        EnsureSelectionVisible();
        UpdateFilterCounts();
    }

    private void EnsureSelectionVisible()
    {
        if (SelectedRow == null || PassesCurrentFilter(SelectedRow))
            return;
        SelectedRow = null;
    }

    private bool PassesCurrentFilter(ItemizadoRowVm row) => FilterRow(row);

    private void UpdateHeader(ItemizadoPageState state)
    {
        HeaderSubtitle = state.Summary.TotalPartidas > 0
            ? $"{state.Summary.TotalPartidas:N0} partidas · {state.Summary.Completas:N0} completas"
            : "Sin partidas cargadas";
    }

    private void UpdateSaveState()
    {
        SaveStateLabel = !projectWorkspace.HasOpenProject
            ? "Sin proyecto"
            : session.IsDirty ? "Cambios pendientes" : "Guardado";
        shell.RefreshChrome();
    }

    public void ToggleChapterCollapse(ItemizadoRowVm? chapterRow)
    {
        if (chapterRow == null || !chapterRow.EsTitulo)
            return;

        ItemizadoCollapseCoordinator.ToggleChapter(chapterRow, allRows);
        SafeRefreshView();
        UpdateFilterCounts();
    }

    public void CollapseAllChapters()
    {
        ItemizadoCollapseCoordinator.CollapseAll(allRows);
        SafeRefreshView();
        UpdateFilterCounts();
    }

    public void ExpandAllChapters()
    {
        ItemizadoCollapseCoordinator.ExpandAll(allRows);
        SafeRefreshView();
        UpdateFilterCounts();
    }

    private bool FilterRow(object obj)
    {
        if (obj is not ItemizadoRowVm row)
            return false;

        if (row.IsHiddenByParentCollapse)
            return false;

        var presentation = row.FilterPresentation;
        if (presentation == null)
            return false;

        return workspace.ShouldDisplayInGrid(presentation, GetFilterPresentations(), FilterMode, debouncedSearchText);
    }

    private IReadOnlyList<ItemizadoRowPresentation> GetFilterPresentations()
    {
        filterPresentationCache ??= allRows
            .Select(r => r.FilterPresentation)
            .Where(p => p != null)
            .Cast<ItemizadoRowPresentation>()
            .ToList();
        return filterPresentationCache;
    }

    private void InvalidateFilterPresentationCache() => filterPresentationCache = null;

    private void UpdateFilterCounts()
    {
        if (ShowEmptyState || ShowEmptyItemizado)
        {
            FilteredCountLabel = "";
            TotalPartidasCount = 0; ListasPartidasCount = 0; PendientesPartidasCount = 0;
            OnPropertyChanged(nameof(TotalFilterLabel)); OnPropertyChanged(nameof(ListasFilterLabel)); OnPropertyChanged(nameof(PendientesFilterLabel));
            return;
        }

        int total = allRows.Count(r => !r.EsTitulo);
        int listas = allRows.Count(r => !r.EsTitulo && (r.FilterPresentation?.CompletionState == PartidaCompletionState.Completed || r.FilterPresentation?.ExcluidoCubicacion == true));
        int pendientes = Math.Max(0, total - listas);
        TotalPartidasCount = total; ListasPartidasCount = listas; PendientesPartidasCount = pendientes;
        OnPropertyChanged(nameof(TotalFilterLabel)); OnPropertyChanged(nameof(ListasFilterLabel)); OnPropertyChanged(nameof(PendientesFilterLabel));

        int visible = allRows.Count(r => !r.EsTitulo && FilterRow(r));
        bool filtered = FilterMode != ItemizadoFilterMode.Todos || !string.IsNullOrWhiteSpace(debouncedSearchText);
        FilteredCountLabel = filtered
            ? $"{visible:N0} de {total:N0} partidas"
            : $"{total:N0} partidas";

        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var visible = rowsView != null ? rowsView.Cast<ItemizadoRowVm>() : Rows;
        var currencyCtx = ItemizadoCurrencyCoordinator.CreateContext(
            selectedDeliveryCurrency,
            session.ValorUfClp,
            session.ProjectInfo.TipoCambioUsdClp,
            session.ProjectInfo.TipoCambioEurClp);

        totalsSnapshot = ItemizadoTotalsCoordinator.Calculate(
            allRows,
            visible,
            currencyCtx,
            FilterMode != ItemizadoFilterMode.Todos,
            debouncedSearchText);

        heroKpiSnapshot = ItemizadoHeroKpiCoordinator.Compute(
            presenter,
            session,
            lastSummary,
            allRows.Count(x => x.EsTitulo),
            currencyCtx);

        OnPropertyChanged(nameof(TotalPresupuestoUfText));
        OnPropertyChanged(nameof(TotalPresupuestoClpText));
        OnPropertyChanged(nameof(FooterPartidasText));
        OnPropertyChanged(nameof(FooterTotalUfText));
        OnPropertyChanged(nameof(FooterTotalClpText));
        OnPropertyChanged(nameof(UfRateCaption));
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(HeroKpi));
    }

    private void RestoreSelection()
    {
        string? target = pendingSelectionId ?? session.PendingItemizadoSelectionId;
        if (string.IsNullOrWhiteSpace(target))
            return;

        SelectedRow = allRows.FirstOrDefault(x => string.Equals(x.Id, target, StringComparison.OrdinalIgnoreCase));
        pendingSelectionId = null;
        session.PendingItemizadoSelectionId = null;
    }
}
