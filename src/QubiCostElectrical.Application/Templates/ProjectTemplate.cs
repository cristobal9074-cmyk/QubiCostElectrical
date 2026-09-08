using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Representa una fila predefinida de una plantilla de itemizado de obra típica.
/// </summary>
public sealed class ProjectTemplateItemRow
{
    public string Item { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = Unidades.GL;
    public decimal Cantidad { get; init; } = 1m;
    public bool EsTitulo { get; init; }
    public int NivelJerarquico { get; init; } = 1;

    public ProjectTemplateItemRow() { }

    public ProjectTemplateItemRow(string item, string descripcion, bool esTitulo = false, string unidad = Unidades.GL, decimal cantidad = 1m, int nivel = 1)
    {
        Item = item;
        Descripcion = descripcion;
        EsTitulo = esTitulo;
        Unidad = unidad;
        Cantidad = cantidad;
        NivelJerarquico = nivel;
    }
}

/// <summary>
/// Plantilla de obra eléctrica típica chilena basada en normativa SEC / RIC.
/// </summary>
public sealed class ProjectTemplate
{
    public string Id { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Subtitulo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string NormaSec { get; init; } = "";
    public string Icono { get; init; } = "📋";
    public bool EsEnBlanco { get; init; }
    public IReadOnlyList<ProjectTemplateItemRow> Filas { get; init; } = Array.Empty<ProjectTemplateItemRow>();

    /// <summary>
    /// Genera una lista de filas vivas de ItemizadoRow con GUIDs nuevos y códigos normalizados.
    /// </summary>
    public List<ItemizadoRow> CreateInstanceRows()
    {
        var result = new List<ItemizadoRow>();
        if (EsEnBlanco || Filas == null || Filas.Count == 0)
            return result;

        string currentTitleId = "1";
        foreach (var fila in Filas)
        {
            var row = new ItemizadoRow
            {
                PartidaId = Guid.NewGuid(),
                Item = fila.Item,
                Descripcion = fila.Descripcion,
                Unidad = fila.Unidad,
                CantidadOriginal = fila.Cantidad,
                Cantidad = fila.EsTitulo ? 0m : fila.Cantidad,
                EsTitulo = fila.EsTitulo,
                NivelJerarquico = fila.NivelJerarquico,
                WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(fila.Item)
            };

            if (fila.EsTitulo)
            {
                currentTitleId = ItemizadoHierarchyRules.NormalizeTitleId(fila.Item);
                row.Id = currentTitleId;
                row.ParentId = "";
            }
            else
            {
                row.ParentId = currentTitleId;
                row.Id = ItemizadoHierarchyRules.NextChildId(result, currentTitleId);
            }

            result.Add(row);
        }

        return result;
    }

    public override string ToString() => $"{Icono} {Nombre}";
}
