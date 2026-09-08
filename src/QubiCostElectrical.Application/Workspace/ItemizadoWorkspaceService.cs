using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public interface IItemizadoWorkspaceService
{
    ItemizadoPageState BuildPageState(
        ItemizadoPresenter presenter,
        BudgetProjectSession session,
        IReadOnlyDictionary<string, decimal>? cachedTotalsByItem = null);
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
        IReadOnlyDictionary<string, decimal>? cachedTotalsByItem = null)
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
            totalsByItem = BuildTotalsByItem(cost);
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
                && IsDescendantOf(child, row)
                && MatchesFilter(child, filter)
                && MatchesSearch(child, searchText));
        }

        return MatchesFilter(row, filter) && MatchesSearch(row, searchText);
    }

    private static bool IsDescendantOf(ItemizadoRowPresentation partida, ItemizadoRowPresentation chapter)
    {
        if (string.IsNullOrWhiteSpace(chapter.Id))
            return false;

        if (string.Equals(partida.ParentId, chapter.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        return (partida.Id ?? "").StartsWith(chapter.Id + ".", StringComparison.OrdinalIgnoreCase);
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

    private static Dictionary<string, decimal> BuildTotalsByItem(CostBreakdown cost)
    {
        return (cost.Origenes ?? new List<CostOriginBreakdown>())
            .GroupBy(x => x.ItemId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalUf), StringComparer.OrdinalIgnoreCase);
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
