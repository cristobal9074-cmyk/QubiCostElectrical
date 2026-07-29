using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Borrado atómico de filas de cubicación en capas legacy y normalizadas.
/// </summary>
public sealed class CubicacionMutationService
{
    public void RemoveRowsByIds(ProjectFileData data, IReadOnlyCollection<string> rowIds)
    {
        if (data == null || rowIds == null || rowIds.Count == 0)
            return;

        var idSet = rowIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (idSet.Count == 0)
            return;

        EnsureLists(data);

        data.Cubicacion.RemoveAll(r => idSet.Contains(r.Id));
        data.CubicacionRecursos.RemoveAll(r => idSet.Contains(r.Id));
        data.CubicacionZonaCantidades.RemoveAll(z => idSet.Contains(z.RecursoId));
        data.CubicacionValorizaciones.RemoveAll(v => idSet.Contains(v.RecursoId));
        data.Mediciones.RemoveAll(m =>
            idSet.Contains(m.CubicacionRowId) ||
            idSet.Contains(m.RecursoId));
        data.Proyecto.CubicacionCellComments ??= new List<CubicacionCellCommentRow>();
        data.Proyecto.CubicacionCellComments.RemoveAll(c => idSet.Contains(c.CubicacionRowId));
    }

    public void RemoveForItemIds(ProjectFileData data, IReadOnlyCollection<string> itemIds)
    {
        if (data == null || itemIds == null || itemIds.Count == 0)
            return;

        var itemIdsToRemove = itemIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (itemIdsToRemove.Count == 0)
            return;

        // Ensure normalized/legacy layers are synchronized before selecting row ids to delete.
        new CubicacionCompositionService().EnsureProject(data);
        EnsureLists(data);

        var rowIdsToDelete = data.CubicacionRecursos
            .Where(r => itemIdsToRemove.Contains(r.ItemId))
            .Select(r => r.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rowIdsToDelete.Count > 0)
            RemoveRowsByIds(data, rowIdsToDelete);

        // Limpieza adicional para entidades que solo tienen ItemId y no RecursoId/CubicacionRowId
        data.CubicacionZonaCantidades.RemoveAll(z => itemIdsToRemove.Contains(z.ItemId));
        data.CubicacionValorizaciones.RemoveAll(v => itemIdsToRemove.Contains(v.ItemId));
        data.Mediciones.RemoveAll(m => m.RecursoId == null && itemIdsToRemove.Contains(m.ItemId));
    }

    private static void EnsureLists(ProjectFileData data)
    {
        data.Cubicacion ??= new List<CubicacionRow>();
        data.CubicacionRecursos ??= new List<CubicacionRecursoRow>();
        data.CubicacionZonaCantidades ??= new List<CubicacionZonaCantidadRow>();
        data.CubicacionValorizaciones ??= new List<CubicacionValorizacionRow>();
        data.Mediciones ??= new List<MeasurementLineRow>();
        data.Proyecto ??= new ProjectInfoRow();
        data.Proyecto.CubicacionCellComments ??= new List<CubicacionCellCommentRow>();
    }
}
