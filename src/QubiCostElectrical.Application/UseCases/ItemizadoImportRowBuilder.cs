using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Construcción unificada de filas importadas/pega (WBS, ParentId, jerarquía).</summary>
internal static class ItemizadoImportRowBuilder
{
    internal static void AppendRow(
        List<ItemizadoRow> rows,
        string itemCode,
        string desc,
        string unidad,
        decimal cant,
        bool esTitulo,
        ref string currentParentId,
        ref int childSeq,
        ref int fallbackTitle)
    {
        string id;
        if (esTitulo)
        {
            id = ItemizadoImportParser.BuildImportedTitleId(itemCode, fallbackTitle);
            currentParentId = id;
            childSeq = 1;
            fallbackTitle++;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(currentParentId))
            {
                currentParentId = $"{fallbackTitle}.0";
                fallbackTitle++;
            }

            id = $"{currentParentId}.{childSeq:00000}";
            childSeq++;
        }

        rows.Add(new ItemizadoRow
        {
            Id = id,
            Item = itemCode,
            WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(itemCode),
            ParentId = esTitulo ? "" : currentParentId,
            NivelJerarquico = esTitulo ? 1 : 2,
            Descripcion = desc,
            Unidad = unidad,
            CantidadOriginal = esTitulo ? 0m : ItemizadoHierarchyRules.NormalizePartidaQuantity(cant),
            Cantidad = 0m,
            EsTitulo = esTitulo
        });
    }
}
