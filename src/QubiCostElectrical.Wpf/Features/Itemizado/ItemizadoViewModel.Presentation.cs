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

    // Refresca la vista de la grilla de forma segura ante cualquier operación
    // (edición de celda, agregar, eliminar, duplicar, mover...). El DataGrid puede
    // tener abierta una transacción AddNew/EditItem en OTRA fila en el momento en
    // que se dispara esta u otra mutación (p. ej. una fila recién agregada que entra
    // en modo edición automático mientras el usuario elimina otra fila desde el menú
    // contextual), y CollectionView.Refresh() lanza InvalidOperationException si se
    // llama mientras esa transacción sigue abierta. En vez de intentar prevenir cada
    // combinación posible de esta condición de carrera, se captura y reintenta en el
    // siguiente ciclo del dispatcher, una vez que la transacción se cierra.
    //
    // Salvaguardas contra un bucle infinito (esto llegó a "trabar" la app en un caso
    // real: agregar una partida, borrar la descripción y presionar Enter):
    //  - refreshRetryScheduled evita que varias llamadas superpuestas (una edición de
    //    celda puede disparar SafeRefreshView desde 2-3 lugares distintos en la misma
    //    cadena síncrona) generen cada una su propia cola de reintentos en paralelo;
    //    se coalescen en un solo reintento pendiente.
    //  - MaxRefreshRetries pone un techo: si la transacción del DataGrid nunca llega
    //    a cerrarse (por cualquier causa, conocida o no), se abandona ese refresco
    //    puntual en vez de reintentar para siempre. El próximo cambio de estado real
    //    disparará un refresco nuevo de todas formas.
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

    private void OnSearchDebounceTick(object? sender, EventArgs e)
    {
        searchDebounceTimer?.Stop();
        if (debouncedSearchText == searchText)
            return;
        debouncedSearchText = searchText;
        SafeRefreshView();
        EnsureSelectionVisible();
        UpdateFilterCounts();
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
            RefreshPresentation(includeCosts: false);
    }

    private void Reload(bool syncOnly)
    {
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
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

        var state = workspace.BuildPageState(presenter, host.Session);
        ApplyPresentation(state, includeCosts: true, refreshView: true);
        ShowEmptyItemizado = host.Session.Itemizado.Count == 0;
        UpdateHeader(state);
        UpdateSaveState();
        RestoreSelection();
        hasPendingUndoForEdit = false;
        OnPropertyChanged(nameof(IsReadOnly));
    }

    private void SyncRowModels()
    {
        InvalidateFilterPresentationCache();
        var sessionRows = host.Session.Itemizado;
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
                var vm = new ItemizadoRowVm(row);
                rowsById[id] = vm;
                allRows.Add(vm);
                Rows.Add(vm);
            }
        }

        foreach (var vm in allRows)
            WireRowVm(vm);

        SafeRefreshView();
    }

    private void WireRowVm(ItemizadoRowVm vm)
    {
        vm.PeersProvider = () => host.Session.Itemizado;
        if (!wiredRows.Add(vm))
            return;

        vm.PropertyChanged += OnRowVmPropertyChanged;
        vm.FieldCommitted += OnRowFieldCommitted;
    }

    private void UnwireRowVm(ItemizadoRowVm vm)
    {
        if (!wiredRows.Remove(vm))
            return;

        vm.PropertyChanged -= OnRowVmPropertyChanged;
        vm.FieldCommitted -= OnRowFieldCommitted;
    }

    private void OnRowVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemizadoRowVm.IsSelected))
            OnPropertyChanged(nameof(CubicarButtonLabel));
    }

    private void OnRowFieldCommitted(ItemizadoRowVm row, string field)
    {
        if (host.Session.IsReadOnly)
            return;

        if (!hasPendingUndoForEdit)
        {
            host.UndoService.PushUndoPoint(host.Session, "Editar itemizado");
            hasPendingUndoForEdit = true;
        }

        host.Session.MarkDirty("itemizado");
        UpdateSaveState();

        if (string.Equals(field, "Excl. cubic.", StringComparison.OrdinalIgnoreCase))
        {
            row.ValidateEditableCells(host.Session.Itemizado);
            RefreshPresentation(includeCosts: false);
        }
    }

    private void ReorderRowsFromSession()
    {
        var ordered = new List<ItemizadoRowVm>();
        foreach (var row in host.Session.Itemizado)
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
        if (ShowEmptyState)
            return;

        SyncRowModels();
        ReorderRowsFromSession();
        RefreshPresentation(includeCosts: true);
        ShowEmptyItemizado = host.Session.Itemizado.Count == 0;
        RestoreSelection();
        RaiseCommandStates();
    }

    private void RefreshPresentation(bool includeCosts = true)
    {
        if (ShowEmptyState)
            return;
        var state = workspace.BuildPageState(
            presenter,
            host.Session,
            includeCosts ? null : costTotalsByItem);
        ApplyPresentation(state, includeCosts, refreshView: false);
        UpdateHeader(state);
    }

    private void ApplyPresentation(ItemizadoPageState state, bool includeCosts, bool refreshView = false)
    {
        foreach (var presentation in state.Rows)
        {
            if (rowsById.TryGetValue(presentation.Id, out var vm))
            {
                vm.ApplyPresentation(presentation);
                vm.ValidateEditableCells();
            }
        }

        if (includeCosts)
        {
            costTotalsByItem.Clear();
            foreach (var presentation in state.Rows.Where(x => !x.EsTitulo))
                costTotalsByItem[presentation.Id] = presentation.TotalUf;
        }

        SummaryPartidas = state.Summary.TotalPartidas.ToString("N0");
        SummaryCompletas = state.Summary.Completas.ToString("N0");
        SummaryIncompletas = state.Summary.Incompletas.ToString("N0");
        SummaryCapitulos = state.Rows.Count(x => x.EsTitulo).ToString("N0");
        SummarySinPrecio = state.Summary.SinPrecio.ToString("N0");
        int pct = state.Summary.TotalPartidas > 0 ? (int)Math.Round(state.Summary.CompletedPct) : 0;
        SummaryStripText =
            $"{state.Summary.TotalPartidas:N0} partidas · {state.Summary.Completas:N0} completas · {pct}% listo";
        UpdateHudState((double)state.Summary.CompletedPct);
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
        SaveStateLabel = !host.ProjectWorkspace.HasOpenProject
            ? "Sin proyecto"
            : host.Session.IsDirty ? "Cambios pendientes" : "Guardado";
        shell.RefreshChrome();
    }

    private bool FilterRow(object obj)
    {
        if (obj is not ItemizadoRowVm row)
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
            return;
        }

        int total = allRows.Count(r => !r.EsTitulo);
        int visible = allRows.Count(r => !r.EsTitulo && FilterRow(r));
        bool filtered = FilterMode != ItemizadoFilterMode.Todos || !string.IsNullOrWhiteSpace(debouncedSearchText);
        FilteredCountLabel = filtered
            ? $"{visible:N0} de {total:N0} partidas"
            : $"{total:N0} partidas";
    }

    private void RestoreSelection()
    {
        string? target = pendingSelectionId ?? host.Session.PendingItemizadoSelectionId;
        if (string.IsNullOrWhiteSpace(target))
            return;

        SelectedRow = allRows.FirstOrDefault(x => string.Equals(x.Id, target, StringComparison.OrdinalIgnoreCase));
        pendingSelectionId = null;
        host.Session.PendingItemizadoSelectionId = null;
    }
}
