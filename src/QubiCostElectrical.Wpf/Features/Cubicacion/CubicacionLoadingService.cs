using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public enum CubicacionStatusFilterMode
{
    Todos,
    Listas,
    Pendientes
}

/// <summary>
/// Carga de partidas/filas desde la sesión y filtro del panel izquierdo — extraído de
/// CubicacionViewModel.Loading.cs (ver plan en ~/.claude/plans/glittery-stargazing-hippo.md).
/// Comportamiento preservado tal cual, incluyendo el bombeo síncrono de Dispatcher.Invoke con
/// DispatcherPriority.Render para forzar el pintado del overlay "Cargando partida…" antes del
/// recálculo (estructuralmente distinto al patrón async-delay de CubicacionTotalsCalculator).
/// </summary>
internal sealed class CubicacionLoadingService
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly ICubicacionTakeoffQueryService querySvc;
    private readonly ICubicacionService cubSvc;

    public CubicacionLoadingService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        ICubicacionTakeoffQueryService querySvc,
        ICubicacionService cubSvc)
    {
        this.parent = parent;
        this.session = session;
        this.querySvc = querySvc;
        this.cubSvc = cubSvc;
    }

    public void LoadPartidas()
    {
        parent.UpdateEmptyState();
        var prevFormulas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parent.Partidas)
        {
            if (!string.IsNullOrWhiteSpace(p.CantidadFormula) && !string.IsNullOrWhiteSpace(p.Id))
                prevFormulas[p.Id] = p.CantidadFormula;
        }
        if (parent.SelectedPartida != null && !string.IsNullOrWhiteSpace(parent.SelectedPartida.CantidadFormula) && !string.IsNullOrWhiteSpace(parent.SelectedPartida.Id))
            prevFormulas[parent.SelectedPartida.Id] = parent.SelectedPartida.CantidadFormula;

        parent.Partidas.Clear();
        parent.FilteredPartidas.Clear();

        // Precalcular conteos e issues O(n+m) en vez de O(n*m) para evitar congelamiento en
        // proyectos grandes (mismo patrón: un solo recorrido de session.Cubicacion, con un
        // diccionario de materiales por código para no reescanear el catálogo por fila).
        var materialesPorCodigo = new Dictionary<string, MaterialRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in session.Materiales)
            if (!string.IsNullOrWhiteSpace(m.Codigo))
                materialesPorCodigo[m.Codigo] = m;

        var countByItemId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var issuesByItemId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in session.Cubicacion)
        {
            if (string.IsNullOrWhiteSpace(r.ItemId)) continue;
            bool hasContent = !string.IsNullOrWhiteSpace(r.Codigo) || r.Cantidad != 0m;
            if (!hasContent) continue;

            countByItemId.TryGetValue(r.ItemId, out int current);
            countByItemId[r.ItemId] = current + 1;

            if (issuesByItemId.Contains(r.ItemId)) continue;
            MaterialRow? material = string.IsNullOrWhiteSpace(r.Codigo)
                ? null
                : materialesPorCodigo.GetValueOrDefault(r.Codigo);
            bool codigoInvalido = !string.IsNullOrWhiteSpace(r.Codigo) && material == null;
            bool precioCero = material != null
                && r.MatClpConFallback == 0m
                && r.MoClpConFallback == 0m;
            if (codigoInvalido || precioCero)
                issuesByItemId.Add(r.ItemId);
        }

        var titleIdsWithChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in session.Itemizado)
        {
            if (!item.EsTitulo)
            {
                if (!string.IsNullOrWhiteSpace(item.ParentId))
                    titleIdsWithChildren.Add(item.ParentId);

                string calcParent = ItemizadoHierarchyRules.ParentIdOf(item.Id, session.Itemizado);
                if (!string.IsNullOrWhiteSpace(calcParent))
                    titleIdsWithChildren.Add(calcParent);
            }
        }

        foreach (var item in session.Itemizado)
        {
            int count = countByItemId.GetValueOrDefault(item.Id ?? "", 0);
            bool hasIssues = issuesByItemId.Contains(item.Id ?? "");

            bool isPureSectionTitle = item.EsTitulo
                && titleIdsWithChildren.Contains(item.Id ?? "")
                && count == 0;

            var pvm = new PartidaListItemVm(item, count, hasIssues, isPureSectionTitle);
            if (!string.IsNullOrWhiteSpace(item.Id) && prevFormulas.TryGetValue(item.Id, out var formula))
                pvm.CantidadFormula = formula;
            parent.Partidas.Add(pvm);
        }
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        parent.UpdateFilterCounts();
        parent.FilteredPartidas.Clear();
        string f = (parent.PartidaFilter ?? "").Trim().ToLowerInvariant();
        var statusFilter = parent.PartidaStatusFilter;

        var matchingPartidas = new HashSet<PartidaListItemVm>();
        foreach (var p in parent.Partidas)
        {
            if (!p.EsPartida) continue;

            bool searchMatch = string.IsNullOrEmpty(f)
                || p.Descripcion.Contains(f, StringComparison.OrdinalIgnoreCase)
                || p.Codigo.Contains(f, StringComparison.OrdinalIgnoreCase);
            if (!searchMatch) continue;

            bool isLista = p.ExcluidoCubicacion || (p.TieneCubicacion && !p.HasIssues);
            bool statusMatch = statusFilter switch
            {
                CubicacionStatusFilterMode.Todos => true,
                CubicacionStatusFilterMode.Listas => isLista,
                CubicacionStatusFilterMode.Pendientes => !isLista,
                _ => true
            };

            if (statusMatch)
                matchingPartidas.Add(p);
        }

        for (int i = 0; i < parent.Partidas.Count; i++)
        {
            var item = parent.Partidas[i];
            if (item.EsPartida)
            {
                if (matchingPartidas.Contains(item))
                    parent.FilteredPartidas.Add(item);
            }
            else
            {
                if (statusFilter == CubicacionStatusFilterMode.Todos && string.IsNullOrEmpty(f))
                {
                    parent.FilteredPartidas.Add(item);
                    continue;
                }

                bool titleSearchMatch = !string.IsNullOrEmpty(f) && (
                    item.Descripcion.Contains(f, StringComparison.OrdinalIgnoreCase)
                    || item.Codigo.Contains(f, StringComparison.OrdinalIgnoreCase));

                int itemLevel = GetWbsLevel(item.Codigo);
                bool hasMatchingChild = false;
                for (int j = i + 1; j < parent.Partidas.Count; j++)
                {
                    var next = parent.Partidas[j];
                    if (!next.EsPartida)
                    {
                        int nextLevel = GetWbsLevel(next.Codigo);
                        if (nextLevel <= itemLevel)
                            break;
                    }
                    else if (matchingPartidas.Contains(next))
                    {
                        hasMatchingChild = true;
                        break;
                    }
                }

                if (titleSearchMatch || hasMatchingChild)
                    parent.FilteredPartidas.Add(item);
            }
        }

        if (parent.SelectedPartida == null || !parent.FilteredPartidas.Contains(parent.SelectedPartida))
        {
            var first = parent.FilteredPartidas.FirstOrDefault(p => p.EsPartida && !p.ExcluidoCubicacion)
                ?? parent.FilteredPartidas.FirstOrDefault(p => p.EsPartida);
            if (first != null)
                parent.SelectPartida(first);
        }
    }

    public void UpdateFilterCounts()
    {
        int total = parent.Partidas.Count(p => p.EsPartida);
        int listas = parent.Partidas.Count(p => p.EsPartida && (p.ExcluidoCubicacion || (p.TieneCubicacion && !p.HasIssues)));
        int pendientes = Math.Max(0, total - listas);
        parent.TotalPartidasCount = total;
        parent.ListasPartidasCount = listas;
        parent.PendientesPartidasCount = pendientes;
        parent.NotifyFilterCountsChanged();
    }

    private static int GetWbsLevel(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return 1;
        return codigo.Count(c => c == '.') + 1;
    }

    public void SelectFirstPartida()
    {
        var first = parent.Partidas.FirstOrDefault(p => p.EsPartida && !p.ExcluidoCubicacion)
            ?? parent.Partidas.FirstOrDefault(p => p.EsPartida);
        if (first != null) parent.SelectPartida(first);
    }

    public void SelectPartida(PartidaListItemVm partida)
    {
        parent.SelectedPartida = partida;
        if (session.PendingTakeoffNavigation != null
            && string.Equals(session.PendingTakeoffNavigation.ItemId, partida.Id, StringComparison.OrdinalIgnoreCase))
        {
            session.PendingTakeoffNavigation = null;
        }
    }

    public void LoadCubicacionRows()
    {
        parent.Rows.Clear();
        parent.SelectedRow = null;
        parent.NotifyCubicacionSummaryChanged();

        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida == null || !selectedPartida.EsPartida)
        {
            parent.NotifyIsEmptyChanged();
            parent.ScheduleRefreshTotals();
            return;
        }

        parent.HasPendingUndoForEdit = false;
        var byItem = CubicacionItemIndex.Build(session.Cubicacion);
        string itemId = selectedPartida.Id ?? "";
        int sourceCount = byItem.TryGetValue(itemId, out var indexed)
            ? indexed.Count
            : 0;
        bool showLoading = sourceCount > 100;

        if (showLoading)
        {
            parent.IsLoading = true;
            // Forzar un pase de render antes del recálculo síncrono (puede tomar segundos con
            // miles de filas): sin esto, WPF nunca pinta el overlay "Cargando partida…" porque
            // el hilo de UI queda bloqueado en el mismo tick en que se puso IsLoading=true.
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        // Recalcular fórmulas solo de esta partida antes de mostrar (RecalculateAll recorría
        // todo el proyecto y podía congelar la UI en proyectos grandes).
        querySvc.RecalculateForItem(session, itemId, byItem);

        var sourceRows = cubSvc.GetRowsForItem(byItem, itemId);
        foreach (var r in sourceRows)
        {
            var vm = parent.CreateRowVm(r);
            parent.InitializeSectorRow(vm);
            parent.Rows.Add(vm);
        }

        // Casillas visuales estilo Excel — no persistir en sesión hasta que el usuario edite.
        while (parent.Rows.Count < CubicacionViewModel.MinVisibleRows)
        {
            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId,
                Tipo = "MAT",
                Area = "General"
            };

            var vm = parent.CreateRowVm(row, isSessionBacked: false);
            parent.InitializeSectorRow(vm);
            parent.Rows.Add(vm);
        }

        parent.ValidateRows();
        parent.IsLoading = false;
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.ScheduleRefreshTotals();
        parent.UpdatePartidaCount(selectedPartida);
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        parent.RefreshSectorWindow();
        parent.RefreshRowNumbers();

        // Sin esto, el panel Detalle queda en blanco ("Seleccione un recurso…") cada vez que
        // se cambia de partida, aunque la partida tenga recursos reales — obliga a un clic
        // extra innecesario. Se preselecciona el primer recurso con datos (no una casilla
        // visual vacía) para que el panel muestre contenido útil de entrada.
        parent.SelectedRow = parent.Rows.FirstOrDefault(r => r.IsSessionBacked);
    }
}
