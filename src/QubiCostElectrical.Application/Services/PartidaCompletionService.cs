using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum PartidaCompletionState
{
    Empty,
    Incomplete,
    Completed
}

public sealed record PartidaCompletionDetail(
    string ItemId,
    bool IsGroup,
    int TotalPartidas,
    int CompletedPartidas,
    int IncompletePartidas,
    int EmptyPartidas)
{
    public int PendingPartidas => IncompletePartidas + EmptyPartidas;

    public decimal CompletedPct => TotalPartidas <= 0 ? 0m : CompletedPartidas * 100m / TotalPartidas;

    public PartidaCompletionState State
    {
        get
        {
            if (TotalPartidas <= 0)
                return PartidaCompletionState.Empty;

            if (CompletedPartidas == TotalPartidas)
                return PartidaCompletionState.Completed;

            if (!IsGroup && EmptyPartidas == TotalPartidas)
                return PartidaCompletionState.Empty;

            // En grupos/títulos, una partida hija vacía también debe encender el semáforo.
            // Así el presupuestista ve capítulos incompletos sin tener que expandirlos.
            return PartidaCompletionState.Incomplete;
        }
    }
}

public sealed record PartidaCompletionSummary(
    int TotalPartidas,
    int CompletedPartidas,
    int IncompletePartidas,
    int EmptyPartidas,
    IReadOnlyDictionary<string, PartidaCompletionState> StatesByItemId,
    IReadOnlyDictionary<string, PartidaCompletionDetail> DetailsByItemId)
{
    public decimal CompletedPct => TotalPartidas <= 0 ? 0m : CompletedPartidas * 100m / TotalPartidas;

    public int PendingPartidas => IncompletePartidas + EmptyPartidas;

    public PartidaCompletionState GetState(string itemId)
    {
        return StatesByItemId.TryGetValue(itemId ?? "", out var state)
            ? state
            : PartidaCompletionState.Empty;
    }

    public PartidaCompletionDetail GetDetail(string itemId)
    {
        if (DetailsByItemId.TryGetValue(itemId ?? "", out var detail))
            return detail;

        return new PartidaCompletionDetail(itemId ?? "", false, 0, 0, 0, 0);
    }

    public int GetPendingCount(string itemId) => GetDetail(itemId).PendingPartidas;
}

public sealed class PartidaCompletionService : IPartidaCompletionService
{
    public PartidaCompletionSummary Build(IEnumerable<ItemizadoRow>? itemizado, IEnumerable<CubicacionRow>? cubicacion)
    {
        var items = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();
        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var states = new Dictionary<string, PartidaCompletionState>(StringComparer.OrdinalIgnoreCase);
        var details = new Dictionary<string, PartidaCompletionDetail>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items.Where(x => !x.EsTitulo))
        {
            // Fuera de alcance / no proyectada: no cuenta como "sin cubicar".
            var state = item.ExcluidoCubicacion
                ? PartidaCompletionState.Completed
                : GetPartidaState(item.Id, rows);
            states[item.Id] = state;
            details[item.Id] = BuildLeafDetail(item.Id, state);
        }

        // Títulos/secciones: semáforo agregado de sus partidas hijas.
        // Regla profesional:
        // - ✓ si todas las partidas hijas están cubicadas.
        // - ⚠ N si existen N partidas hijas pendientes, incompletas o vacías.
        // - — si el título no tiene partidas hijas reales.
        foreach (var title in items.Where(x => x.EsTitulo).OrderByDescending(x => (x.Id ?? "").Length))
        {
            var childDetails = items
                .Where(x => !x.EsTitulo && IsChildOf(x.Id, title.Id))
                .Select(x => details.TryGetValue(x.Id, out var d) ? d : BuildLeafDetail(x.Id, PartidaCompletionState.Empty))
                .ToList();

            var detail = BuildGroupDetail(title.Id, childDetails);
            details[title.Id] = detail;
            states[title.Id] = detail.State;
        }

        int total = items.Count(x => !x.EsTitulo);
        int completed = items.Count(x => !x.EsTitulo && states.TryGetValue(x.Id, out var s) && s == PartidaCompletionState.Completed);
        int incomplete = items.Count(x => !x.EsTitulo && states.TryGetValue(x.Id, out var s) && s == PartidaCompletionState.Incomplete);
        int empty = Math.Max(0, total - completed - incomplete);

        return new PartidaCompletionSummary(total, completed, incomplete, empty, states, details);
    }

    public PartidaCompletionState GetPartidaState(string itemId, IEnumerable<CubicacionRow> cubicacion)
    {
        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => string.Equals(x.ItemId, itemId ?? "", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
            return PartidaCompletionState.Empty;

        bool hasAnyInput = rows.Any(HasAnyInput);
        if (!hasAnyInput)
            return PartidaCompletionState.Empty;

        bool hasCompletedLine = rows.Any(IsCompletedLine);
        bool hasIncompleteLine = rows.Any(IsIncompleteLine);

        if (hasCompletedLine && !hasIncompleteLine)
            return PartidaCompletionState.Completed;

        return PartidaCompletionState.Incomplete;
    }

    public string GetIcon(PartidaCompletionState state) => state switch
    {
        PartidaCompletionState.Completed => "✓",
        PartidaCompletionState.Incomplete => "⚠",
        _ => "—"
    };

    public string GetTooltip(PartidaCompletionState state) => state switch
    {
        PartidaCompletionState.Completed => "Cubicado: tiene materiales con cantidad válida.",
        PartidaCompletionState.Incomplete => "Incompleto: tiene datos, pero falta código o cantidad válida.",
        _ => "Vacío: aún no tiene cubicación."
    };

    public decimal GetLineCompletionPct(string itemId, IEnumerable<CubicacionRow> cubicacion)
    {
        var rows = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => string.Equals(x.ItemId, itemId ?? "", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
            return 0m;

        int completed = rows.Count(IsCompletedLine);
        return Math.Round(completed * 100m / rows.Count, 0);
    }

    private static PartidaCompletionDetail BuildLeafDetail(string itemId, PartidaCompletionState state)
    {
        return state switch
        {
            PartidaCompletionState.Completed => new PartidaCompletionDetail(itemId ?? "", false, 1, 1, 0, 0),
            PartidaCompletionState.Incomplete => new PartidaCompletionDetail(itemId ?? "", false, 1, 0, 1, 0),
            _ => new PartidaCompletionDetail(itemId ?? "", false, 1, 0, 0, 1)
        };
    }

    private static PartidaCompletionDetail BuildGroupDetail(string itemId, IReadOnlyList<PartidaCompletionDetail> children)
    {
        if (children.Count == 0)
            return new PartidaCompletionDetail(itemId ?? "", true, 0, 0, 0, 0);

        int total = children.Sum(x => x.TotalPartidas);
        int completed = children.Sum(x => x.CompletedPartidas);
        int incomplete = children.Sum(x => x.IncompletePartidas);
        int empty = children.Sum(x => x.EmptyPartidas);

        return new PartidaCompletionDetail(itemId ?? "", true, total, completed, incomplete, empty);
    }

    private static bool IsChildOf(string id, string parentId)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(parentId)) return false;
        return id.StartsWith(parentId + ".", StringComparison.Ordinal);
    }

    private static bool HasAnyInput(CubicacionRow row)
    {
        return !string.IsNullOrWhiteSpace(row.Codigo) ||
               !string.IsNullOrWhiteSpace(row.Descripcion) ||
               row.Cantidad != 0m ||
               row.MatUf != 0m ||
               row.MoUf != 0m;
    }

    private static bool IsCompletedLine(CubicacionRow row)
    {
        return !string.IsNullOrWhiteSpace(row.Codigo) && row.Cantidad > 0m;
    }

    private static bool IsIncompleteLine(CubicacionRow row)
    {
        if (!HasAnyInput(row)) return false;
        return string.IsNullOrWhiteSpace(row.Codigo) || row.Cantidad < 0m;
    }
}
