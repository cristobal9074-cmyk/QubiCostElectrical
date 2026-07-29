using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Reglas de jerarquía WBS para itemizado eléctrico chileno.
/// Id interno estable (puntos): capítulo N.0, partida N.0.MMMMM.
/// Item visible (comas): N,0 / N,M.
/// PartidaId (Guid) nunca cambia — es la clave de persistencia y vínculos.
/// </summary>
public static class ItemizadoHierarchyRules
{
    public static string NormalizeIdFromItem(string? item) =>
        (item ?? "").Trim().Replace(',', '.');

    public static string FormatItemFromId(string id) =>
        (id ?? "").Replace('.', ',');

    public static bool IsTitleId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[^1] == "0";
    }

    public static string NormalizeTitleId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "1.0";
        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out var n))
            return $"{n}.0";
        if (parts.Length >= 2 && parts[1] == "0")
            return $"{parts[0]}.0";
        return id.Trim();
    }

  /// <summary>Padre jerárquico del Id interno (no del Item visible).</summary>
    public static string ParentIdOf(string? id, IReadOnlyList<ItemizadoRow>? rows = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";

        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return "";

        if (parts.Length == 2)
        {
            if (parts[1] == "0")
                return "";
            string chapter = parts[0];
            string preferred = $"{chapter}.0";
            if (rows != null && rows.Any(r => string.Equals(r.Id, preferred, StringComparison.OrdinalIgnoreCase)))
                return preferred;
            if (rows != null && rows.Any(r => string.Equals(r.Id, chapter, StringComparison.OrdinalIgnoreCase)))
                return chapter;
            return preferred;
        }

        return string.Join(".", parts.Take(parts.Length - 1));
    }

    public static bool MatchesRemovedId(string? itemId, string removedId)
    {
        if (string.IsNullOrWhiteSpace(removedId) || string.IsNullOrWhiteSpace(itemId))
            return false;
        return string.Equals(itemId, removedId, StringComparison.OrdinalIgnoreCase)
            || itemId.StartsWith(removedId + ".", StringComparison.Ordinal);
    }

    public static string NextChildId(IEnumerable<ItemizadoRow> itemizado, string parentId)
    {
        parentId = NormalizeTitleId(parentId);
        string chapter = parentId.Split('.')[0];
        int max = 0;
        foreach (var row in itemizado.Where(i => !i.EsTitulo))
        {
            string id = row.Id ?? "";
            if (id.StartsWith(parentId + ".", StringComparison.Ordinal))
            {
                var last = id.Split('.').LastOrDefault();
                if (int.TryParse(last, out var n))
                    max = Math.Max(max, n);
                continue;
            }

            var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && string.Equals(parts[0], chapter, StringComparison.Ordinal)
                && int.TryParse(parts[1], out var legacy)
                && legacy != 0)
            {
                max = Math.Max(max, legacy);
            }
        }

        return $"{parentId}.{max + 1:00000}";
    }

    public static string NextDisplayItemCode(IEnumerable<ItemizadoRow> itemizado, string parentTitleId)
    {
        parentTitleId = NormalizeTitleId(parentTitleId);
        string chapter = parentTitleId.Split('.')[0];
        int maxSub = 0;

        foreach (var row in itemizado.Where(x => !x.EsTitulo))
        {
            string normalized = NormalizeIdFromItem(row.Item);
            var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], chapter, StringComparison.Ordinal))
                continue;
            if (int.TryParse(parts[1], out var sub))
                maxSub = Math.Max(maxSub, sub);
        }

        return $"{chapter},{maxSub + 1}";
    }

    public static string NextUniqueDisplayItemCode(IEnumerable<ItemizadoRow> itemizado, string proposedItem)
    {
        string baseCode = (proposedItem ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseCode))
            return baseCode;

        if (!itemizado.Any(x => string.Equals(x.Item?.Trim(), baseCode, StringComparison.OrdinalIgnoreCase)))
            return baseCode;

        for (int i = 2; i < 10_000; i++)
        {
            string candidate = $"{baseCode} ({i})";
            if (!itemizado.Any(x => string.Equals(x.Item?.Trim(), candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return $"{baseCode}-{Guid.NewGuid():N}"[..Math.Min(baseCode.Length + 9, 40)];
    }

    public static decimal NormalizePartidaQuantity(decimal cantidad) =>
        cantidad <= 0m ? 1m : cantidad;

    public static ItemizadoRow CreateDefaultTitleRow(int chapterNumber = 1) =>
        new()
        {
            Id = $"{chapterNumber}.0",
            Item = $"{chapterNumber},0",
            WBSCode = $"{chapterNumber}.0",
            Descripcion = "CAPÍTULO 1",
            Unidad = Unidades.GL,
            Cantidad = 0,
            EsTitulo = true,
            NivelJerarquico = 1,
            ParentId = ""
        };

    public static ItemizadoRow CreatePartidaRow(
        IEnumerable<ItemizadoRow> itemizado,
        string parentTitleId,
        string descripcion,
        string unidad)
    {
        parentTitleId = NormalizeTitleId(parentTitleId);
        string id = NextChildId(itemizado, parentTitleId);
        string item = NextDisplayItemCode(itemizado, parentTitleId);
        return new ItemizadoRow
        {
            Id = id,
            Item = item,
            WBSCode = NormalizeIdFromItem(item),
            ParentId = parentTitleId,
            NivelJerarquico = 2,
            Descripcion = descripcion,
            Unidad = string.IsNullOrWhiteSpace(unidad) ? Unidades.GL : unidad.Trim(),
            Cantidad = 1,
            EsTitulo = false
        };
    }
}
