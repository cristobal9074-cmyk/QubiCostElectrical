using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ExportScopeMode
{
    Complete,
    Partida,
    Area
}

public sealed record ExportPartidaOption(string Id, string Label, bool IsTitle);

public static class ExportScopeService
{
    public static IReadOnlyList<ExportPartidaOption> BuildPartidaOptions(ProjectFileData source)
    {
        source ??= new ProjectFileData();

        return source.Itemizado
            .Select(row => new ExportPartidaOption(
                row.Id,
                BuildPartidaLabel(row),
                row.EsTitulo))
            .ToList();
    }

    public static IReadOnlyList<string> BuildAreaOptions(ProjectFileData source)
    {
        source ??= new ProjectFileData();

        return source.Cubicacion
            .Select(row => string.IsNullOrWhiteSpace(row.Area) ? "General" : row.Area.Trim())
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(area => area, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ProjectFileData ApplyScope(ProjectFileData source, ExportScopeMode mode, string? partidaId, string? area)
    {
        source ??= new ProjectFileData();
        if (mode == ExportScopeMode.Complete)
            return Clone(source);

        var selectedIds = mode == ExportScopeMode.Partida
            ? ResolvePartidaScopeIds(source, partidaId)
            : ResolveAreaScopeIds(source, area);

        if (selectedIds.Count == 0)
            return Clone(source);

        return Clone(source, selectedIds);
    }

    private static ProjectFileData Clone(ProjectFileData source)
        => new()
        {
            Proyecto = source.Proyecto,
            Itemizado = source.Itemizado.ToList(),
            Materiales = source.Materiales.ToList(),
            Cubicacion = source.Cubicacion.ToList(),
            Plantillas = source.Plantillas.ToList(),
            ConjuntosElectricos = source.ConjuntosElectricos.ToList(),
            CubicacionRecursos = source.CubicacionRecursos.ToList(),
            CubicacionZonaCantidades = source.CubicacionZonaCantidades.ToList(),
            CubicacionValorizaciones = source.CubicacionValorizaciones.ToList(),
            PartidaApuVinculos = source.PartidaApuVinculos.ToList(),
            QtyHeadersByItem = source.QtyHeadersByItem.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value ?? Array.Empty<string>()).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            Zonas = source.Zonas.ToList(),
            Mediciones = source.Mediciones.ToList(),
            Versiones = source.Versiones.ToList(),
            Apus = source.Apus.ToList(),
            OrdenesCambio = source.OrdenesCambio.ToList(),
            CubicacionSC = source.CubicacionSC.ToList(),
            CubicacionScConfig = source.CubicacionScConfig,
            ActiveVersionId = source.ActiveVersionId
        };

    private static ProjectFileData Clone(ProjectFileData source, HashSet<string> selectedIds)
    {
        var itemizado = FilterItemizado(source.Itemizado, selectedIds);
        var cubicacion = FilterByItemId(source.Cubicacion, selectedIds, row => row.ItemId);
        var apus = FilterByItemId(source.Apus, selectedIds, row => row.ItemId);
        var vinculos = FilterByItemId(source.PartidaApuVinculos, selectedIds, row => row.ItemId);
        var recursos = FilterByItemId(source.CubicacionRecursos, selectedIds, row => row.ItemId);
        var zonas = FilterByItemId(source.CubicacionZonaCantidades, selectedIds, row => row.ItemId);
        var valorizaciones = FilterByItemId(source.CubicacionValorizaciones, selectedIds, row => row.ItemId);
        var mediciones = FilterByItemId(source.Mediciones, selectedIds, row => row.ItemId);
        var qtyHeaders = source.QtyHeadersByItem
            .Where(kvp => selectedIds.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => (kvp.Value ?? Array.Empty<string>()).ToArray(), StringComparer.OrdinalIgnoreCase);
        var usedCodes = CollectUsedMaterialCodes(cubicacion, apus);
        var materiales = usedCodes.Count == 0
            ? source.Materiales.ToList()
            : source.Materiales.Where(m => usedCodes.Contains(Normalize(m.Codigo))).ToList();

        return new ProjectFileData
        {
            Proyecto = source.Proyecto,
            Itemizado = itemizado,
            Materiales = materiales,
            Cubicacion = cubicacion,
            Plantillas = source.Plantillas.ToList(),
            ConjuntosElectricos = source.ConjuntosElectricos.ToList(),
            CubicacionRecursos = recursos,
            CubicacionZonaCantidades = zonas,
            CubicacionValorizaciones = valorizaciones,
            PartidaApuVinculos = vinculos,
            QtyHeadersByItem = qtyHeaders,
            Zonas = source.Zonas.ToList(),
            Mediciones = mediciones,
            Versiones = source.Versiones.ToList(),
            Apus = apus,
            OrdenesCambio = source.OrdenesCambio.ToList(),
            CubicacionSC = source.CubicacionSC.ToList(),
            CubicacionScConfig = source.CubicacionScConfig,
            ActiveVersionId = source.ActiveVersionId
        };
    }

    private static List<ItemizadoRow> FilterItemizado(IReadOnlyList<ItemizadoRow> source, HashSet<string> selectedIds)
        => source.Where(row => selectedIds.Contains(row.Id)).ToList();

    private static HashSet<string> ResolvePartidaScopeIds(ProjectFileData source, string? partidaId)
    {
        var row = source.Itemizado.FirstOrDefault(x => string.Equals(x.Id, partidaId ?? "", StringComparison.OrdinalIgnoreCase));
        if (row == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var byId = source
            .WhereItemizadoById()
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var childrenByParent = source
            .Itemizado
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAncestors(row, byId, ids);
        ids.Add(row.Id);

        if (row.EsTitulo)
            AddDescendants(row.Id, childrenByParent, ids);

        return ids;
    }

    private static HashSet<string> ResolveAreaScopeIds(ProjectFileData source, string? area)
    {
        var normalizedArea = (area ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedArea))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matchedIds = source.Cubicacion
            .Where(row => string.Equals(NormalizeArea(row.Area), NormalizeArea(normalizedArea), StringComparison.OrdinalIgnoreCase))
            .Select(row => row.ItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (matchedIds.Count == 0)
            return matchedIds;

        var byId = source
            .WhereItemizadoById()
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var contextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in matchedIds)
        {
            if (!byId.TryGetValue(id, out var row))
                continue;

            AddAncestors(row, byId, contextIds);
            contextIds.Add(row.Id);
        }

        return contextIds;
    }

    private static List<T> FilterByItemId<T>(IEnumerable<T> source, HashSet<string> selectedIds, Func<T, string> itemIdSelector)
        => source.Where(row => selectedIds.Contains((itemIdSelector(row) ?? "").Trim())).ToList();

    private static HashSet<string> CollectUsedMaterialCodes(IEnumerable<CubicacionRow> cubicacion, IEnumerable<ApuRow> apus)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in cubicacion)
        {
            if (!string.IsNullOrWhiteSpace(row.Codigo))
                codes.Add(Normalize(row.Codigo));
        }

        foreach (var apu in apus)
        {
            foreach (var recurso in apu.Recursos)
            {
                if (!string.IsNullOrWhiteSpace(recurso.CodigoMaterial))
                    codes.Add(Normalize(recurso.CodigoMaterial));
            }
        }

        return codes;
    }

    private static void AddAncestors(ItemizadoRow row, IReadOnlyDictionary<string, ItemizadoRow> byId, HashSet<string> ids)
    {
        var current = row;
        while (!string.IsNullOrWhiteSpace(current.ParentId) && byId.TryGetValue(current.ParentId, out var parent))
        {
            if (!ids.Add(parent.Id))
                break;

            current = parent;
        }
    }

    private static void AddDescendants(string parentId, IReadOnlyDictionary<string, List<ItemizadoRow>> childrenByParent, HashSet<string> ids)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return;

        foreach (var child in children)
        {
            if (!ids.Add(child.Id))
                continue;

            AddDescendants(child.Id, childrenByParent, ids);
        }
    }

    private static string BuildPartidaLabel(ItemizadoRow row)
    {
        var code = string.IsNullOrWhiteSpace(row.Item) ? row.Id : row.Item;
        var description = string.IsNullOrWhiteSpace(row.Descripcion) ? "Sin descripción" : row.Descripcion.Trim();
        var suffix = row.EsTitulo ? " (título)" : "";
        return $"{code} · {description}{suffix}";
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string NormalizeArea(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static IEnumerable<ItemizadoRow> WhereItemizadoById(this ProjectFileData source)
        => source.Itemizado.Where(row => !string.IsNullOrWhiteSpace(row.Id));
}
