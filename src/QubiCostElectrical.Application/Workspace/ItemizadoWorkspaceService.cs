using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public interface IItemizadoWorkspaceService
{
    ItemizadoPageState BuildPageState(
        ItemizadoPresenter presenter,
        BudgetProjectSession session,
        IReadOnlyDictionary<string, decimal>? cachedTotalsByItem = null,
        ItemizadoPerspectiveMode perspective = ItemizadoPerspectiveMode.Venta);
    bool MatchesFilter(ItemizadoRowPresentation row, ItemizadoFilterMode filter);
    bool MatchesSearch(ItemizadoRowPresentation row, string? searchText);
    bool ShouldDisplayInGrid(
        ItemizadoRowPresentation row,
        IReadOnlyList<ItemizadoRowPresentation> allRows,
        ItemizadoFilterMode filter,
        string? searchText);
    int CountDeleteAffectedRows(ItemizadoPresenter presenter, int sourceIndex);
    string BuildDeleteDescription(BudgetProjectSession session, int sourceIndex);
}

public sealed class ItemizadoWorkspaceService : IItemizadoWorkspaceService
{
    private readonly IPartidaCompletionService _partidaCompletion;

    public ItemizadoWorkspaceService() : this(new PartidaCompletionService())
    {
    }

    public ItemizadoWorkspaceService(IPartidaCompletionService partidaCompletion)
    {
        _partidaCompletion = partidaCompletion ?? throw new ArgumentNullException(nameof(partidaCompletion));
    }
    public ItemizadoPageState BuildPageState(
        ItemizadoPresenter presenter,
        BudgetProjectSession session,
        IReadOnlyDictionary<string, decimal>? cachedTotalsByItem = null,
        ItemizadoPerspectiveMode perspective = ItemizadoPerspectiveMode.Venta)
    {
        if (presenter is null)
            throw new ArgumentNullException(nameof(presenter));
        if (session is null)
            throw new ArgumentNullException(nameof(session));
        var grid = presenter.BuildGridState();
        CostBreakdown? cost = null;
        Dictionary<string, decimal> totalsByItem;
        if (cachedTotalsByItem != null)
        {
            totalsByItem = new Dictionary<string, decimal>(cachedTotalsByItem, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            cost = presenter.BuildCostBreakdown();
            totalsByItem = BuildTotalsByItem(cost, session, perspective);
        }
        var rows = new List<ItemizadoRowPresentation>();

        for (int i = 0; i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i];
            var state = row.EsTitulo
                ? PartidaCompletionState.Empty
                : grid.Completion.GetState(row.Id);
            var detail = row.EsTitulo
                ? grid.Completion.GetDetail(row.Id)
                : grid.Completion.GetDetail(row.Id);

            decimal totalUf = totalsByItem.TryGetValue(row.Id, out var total) ? total : 0m;
            bool hasPrice = !row.EsTitulo && totalUf > 0m;
            decimal unitPrice = !row.EsTitulo && row.Cantidad > 0m && totalUf > 0m
                ? totalUf / row.Cantidad
                : 0m;
            decimal completionPct = row.EsTitulo
                ? detail.CompletedPct
                : _partidaCompletion.GetLineCompletionPct(row.Id, session.Cubicacion);

            rows.Add(new ItemizadoRowPresentation
            {
                SourceIndex = i,
                Id = row.Id ?? "",
                ParentId = row.ParentId ?? "",
                Item = row.Item ?? "",
                Descripcion = row.Descripcion ?? "",
                Unidad = row.Unidad ?? "",
                Cantidad = row.Cantidad,
                EsTitulo = row.EsTitulo,
                ExcluidoCubicacion = row.ExcluidoCubicacion,
                CompletionState = state,
                CompletionPct = completionPct,
                UnitPriceUf = unitPrice,
                TotalUf = totalUf,
                HasPrice = hasPrice,
                StatusLabel = BuildStatusLabel(row, state),
                StatusKind = BuildStatusKind(row, state, hasPrice)
            });
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.EsTitulo)
                continue;

            decimal chapterTotalUf = 0m;
            for (int j = i + 1; j < rows.Count; j++)
            {
                var next = rows[j];
                if (next.EsTitulo)
                    break;
                chapterTotalUf += next.TotalUf;
            }

            if (chapterTotalUf == 0m)
            {
                chapterTotalUf = rows
                    .Where(child => !child.EsTitulo && IsDescendantOf(child, row, rows))
                    .Sum(child => child.TotalUf);
            }

            row.TotalUf = chapterTotalUf;
            row.HasPrice = chapterTotalUf > 0m;
        }

        return new ItemizadoPageState(rows, BuildSummary(grid.Completion, totalsByItem, session, grid.Rows), grid.Completion);
    }

    public bool MatchesFilter(ItemizadoRowPresentation row, ItemizadoFilterMode filter)
    {
        if (row.EsTitulo)
            return filter == ItemizadoFilterMode.Todos;

        return filter switch
        {
            ItemizadoFilterMode.Todos => true,
            ItemizadoFilterMode.Incompletos => row.CompletionState != PartidaCompletionState.Completed && !row.ExcluidoCubicacion,
            ItemizadoFilterMode.Completos => row.CompletionState == PartidaCompletionState.Completed && !row.ExcluidoCubicacion,
            ItemizadoFilterMode.SinCubicar => row.CompletionState == PartidaCompletionState.Empty && !row.ExcluidoCubicacion,
            ItemizadoFilterMode.SinPrecio => !row.ExcluidoCubicacion && row.CompletionState != PartidaCompletionState.Empty && !row.HasPrice,
            _ => true
        };
    }

    public bool MatchesSearch(ItemizadoRowPresentation row, string? searchText)
    {
        string q = (searchText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
            return true;

        q = q.ToUpperInvariant();
        return (row.Item ?? "").ToUpperInvariant().Contains(q)
            || (row.Descripcion ?? "").ToUpperInvariant().Contains(q)
            || (row.Unidad ?? "").ToUpperInvariant().Contains(q);
    }

    public bool ShouldDisplayInGrid(
        ItemizadoRowPresentation row,
        IReadOnlyList<ItemizadoRowPresentation> allRows,
        ItemizadoFilterMode filter,
        string? searchText)
    {
        if (row.EsTitulo)
        {
            bool searchActive = !string.IsNullOrWhiteSpace(searchText?.Trim());
            if (filter == ItemizadoFilterMode.Todos && !searchActive)
                return true;

            if (searchActive && MatchesSearch(row, searchText))
                return true;

            return allRows.Any(child =>
                !child.EsTitulo
                && IsDescendantOf(child, row, allRows)
                && MatchesFilter(child, filter)
                && MatchesSearch(child, searchText));
        }

        if (!string.IsNullOrWhiteSpace(searchText?.Trim()) && MatchesFilter(row, filter))
        {
            if (MatchesSearch(row, searchText))
                return true;

            if (allRows.Any(p => p.EsTitulo && IsDescendantOf(row, p, allRows) && MatchesSearch(p, searchText)))
                return true;

            return false;
        }

        return MatchesFilter(row, filter) && MatchesSearch(row, searchText);
    }

    public static bool IsDescendantOf(
        ItemizadoRowPresentation partida,
        ItemizadoRowPresentation chapter,
        IReadOnlyList<ItemizadoRowPresentation>? allRows = null)
    {
        if (chapter == null || partida == null)
            return false;

        if (!string.IsNullOrWhiteSpace(partida.ParentId))
        {
            if (string.Equals(partida.ParentId, chapter.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(partida.ParentId, chapter.Item, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(chapter.Id) && !string.IsNullOrWhiteSpace(partida.Id))
        {
            if (partida.Id.StartsWith(chapter.Id + ".", StringComparison.OrdinalIgnoreCase))
                return true;

            string chapterId = chapter.Id.Trim();
            if (chapterId.EndsWith(".0", StringComparison.Ordinal))
            {
                string prefix = chapterId[..^2];
                string pId = partida.Id.Trim();
                if (pId.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        string cItem = ItemizadoHierarchyRules.NormalizeIdFromItem(chapter.Item);
        string pItem = ItemizadoHierarchyRules.NormalizeIdFromItem(partida.Item);
        if (!string.IsNullOrWhiteSpace(cItem) && !string.IsNullOrWhiteSpace(pItem))
        {
            if (cItem.EndsWith(".0", StringComparison.Ordinal))
                cItem = cItem[..^2];

            if (pItem.StartsWith(cItem + ".", StringComparison.OrdinalIgnoreCase)
                || pItem.StartsWith(cItem + ",", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (allRows != null && chapter.SourceIndex >= 0 && partida.SourceIndex > chapter.SourceIndex)
        {
            int nextBoundaryIndex = allRows.Count;
            string basePrefix = !string.IsNullOrWhiteSpace(cItem) ? cItem : "";

            for (int k = chapter.SourceIndex + 1; k < allRows.Count; k++)
            {
                var candidate = allRows[k];
                if (candidate.EsTitulo)
                {
                    string candItem = ItemizadoHierarchyRules.NormalizeIdFromItem(candidate.Item);
                    if (candItem.EndsWith(".0", StringComparison.Ordinal))
                        candItem = candItem[..^2];

                    bool isSubChapter = !string.IsNullOrWhiteSpace(basePrefix)
                        && candItem.StartsWith(basePrefix + ".", StringComparison.OrdinalIgnoreCase);

                    if (!isSubChapter)
                    {
                        nextBoundaryIndex = k;
                        break;
                    }
                }
            }

            if (partida.SourceIndex < nextBoundaryIndex)
                return true;
        }

        return false;
    }

    public int CountDeleteAffectedRows(ItemizadoPresenter presenter, int sourceIndex)
    {
        if (presenter is null)
            throw new ArgumentNullException(nameof(presenter));
        return presenter.CountAffectedRows(sourceIndex);
    }

    public string BuildDeleteDescription(BudgetProjectSession session, int sourceIndex)
    {
        var row = session.Itemizado.ElementAtOrDefault(sourceIndex);
        if (row == null)
            return "";
        string label = string.IsNullOrWhiteSpace(row.Descripcion) ? row.Item ?? row.Id ?? "" : row.Descripcion;
        return row.EsTitulo
            ? $"Título: {label}"
            : $"Partida {row.Item}: {label}";
    }

    /// <summary>
    /// Costo directo por partida en UF, la unidad de presentación del Itemizado Valorado.
    /// El origen de costos viene en CLP (campos históricos <c>*Uf</c> del motor), así que se
    /// convierte con la UF del proyecto: exponerlo en pesos aquí inflaría por la tasa UF
    /// tanto la grilla como el pie y los estados de pago, que ya asumen UF.
    /// </summary>
    private static Dictionary<string, decimal> BuildTotalsByItem(
        CostBreakdown cost,
        BudgetProjectSession session,
        ItemizadoPerspectiveMode perspective)
    {
        decimal ufRate = session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;
        if (perspective == ItemizadoPerspectiveMode.Venta)
        {
            decimal targetAmount = cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf : cost.TotalVentaUf;
            var itemizadoMap = (session.Itemizado ?? new List<ItemizadoRow>())
                .Where(x => !x.EsTitulo)
                .GroupBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var offerRows = OfferClienteRowBuilder.Build(session.Itemizado, cost, targetAmount, session.ProjectInfo?.Nombre ?? "Proyecto");
            var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in offerRows)
            {
                if (r.EsTitulo) continue;
                decimal uf = PriceConversion.ClpToUf(r.TotalUf, ufRate);
                string key = itemizadoMap.TryGetValue(r.Item, out var id) ? id : r.Item;
                if (!dict.TryAdd(key, uf))
                    dict[key] += uf;
            }
            return dict;
        }

        return (cost.Origenes ?? new List<CostOriginBreakdown>())
            .GroupBy(x => x.ItemId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => PriceConversion.ClpToUf(g.Sum(x => x.TotalUf), ufRate),
                StringComparer.OrdinalIgnoreCase);
    }

    private static ItemizadoSummaryMetrics BuildSummary(
        PartidaCompletionSummary completion,
        IReadOnlyDictionary<string, decimal> totalsByItem,
        BudgetProjectSession session,
        IReadOnlyList<ItemizadoRow> rows)
    {
        int sinPrecio = rows.Count(x =>
            !x.EsTitulo &&
            !x.ExcluidoCubicacion &&
            completion.GetState(x.Id) != PartidaCompletionState.Empty &&
            (!totalsByItem.TryGetValue(x.Id ?? "", out var total) || total <= 0m));

        decimal ufCubicadas = session.Cubicacion.Sum(x => x.UfTotal);
        return new ItemizadoSummaryMetrics(
            completion.TotalPartidas,
            completion.CompletedPartidas,
            completion.IncompletePartidas,
            completion.EmptyPartidas,
            sinPrecio,
            completion.CompletedPct,
            ufCubicadas);
    }

    private static string BuildStatusLabel(ItemizadoRow row, PartidaCompletionState state)
    {
        if (row.EsTitulo)
            return "Capítulo";
        if (row.ExcluidoCubicacion)
        {
            var motivo = (row.MotivoNoCubicar ?? "").Trim();
            return string.IsNullOrEmpty(motivo) ? "Excluida" : "Excluida · " + TruncateMotivo(motivo, 28);
        }
        return state switch
        {
            PartidaCompletionState.Completed => "Completo",
            PartidaCompletionState.Incomplete => "Incompleto",
            _ => "Sin cubicar"
        };
    }

    private static string TruncateMotivo(string motivo, int maxChars)
    {
        if (motivo.Length <= maxChars)
            return motivo;
        return motivo[..(maxChars - 1)].TrimEnd() + "…";
    }

    private static string BuildStatusKind(ItemizadoRow row, PartidaCompletionState state, bool hasPrice)
    {
        if (row.EsTitulo)
            return "neutral";
        if (row.ExcluidoCubicacion)
            return "neutral";
        if (state != PartidaCompletionState.Empty && !hasPrice)
            return "bad";
        return state switch
        {
            PartidaCompletionState.Completed => "ok",
            PartidaCompletionState.Incomplete => "warn",
            _ => "info"
        };
    }
}
