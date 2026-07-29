using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class TemplateExpansionResult
{
    public List<CubicacionRow> Rows { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class TemplateService : ITemplateService
{
    public TemplateExpansionResult BuildCubicacionRowsFromTemplate(
        TemplateRow template,
        ItemizadoRow partida,
        IEnumerable<TemplateRow> plantillas,
        IEnumerable<MaterialRow> materiales)
    {
        var result = new TemplateExpansionResult();
        if (template == null || partida == null) return result;

        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExpandTemplateToRows(
            template,
            partida.Cantidad,
            partida.Id ?? string.Empty,
            plantillas?.ToList() ?? new List<TemplateRow>(),
            materiales?.ToList() ?? new List<MaterialRow>(),
            result.Rows,
            result.Warnings,
            stack);

        result.Rows = MergeTemplateRows(result.Rows);
        return result;
    }

    public List<CubicacionRow> MergeTemplateRows(IEnumerable<CubicacionRow> rows)
    {
        return (rows ?? Enumerable.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => new
            {
                Codigo = (x.Codigo ?? "").Trim().ToUpperInvariant(),
                Tipo = (x.Tipo ?? "").Trim().ToUpperInvariant(),
                Unidad = (x.Unidad ?? "").Trim().ToUpperInvariant(),
                Area = (x.Area ?? "").Trim().ToUpperInvariant(),
                x.MatUf,
                x.MoUf
            })
            .Select(g =>
            {
                var first = g.First();
                return new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = first.ItemId,
                    Tipo = first.Tipo,
                    Codigo = first.Codigo,
                    Descripcion = first.Descripcion,
                    Unidad = first.Unidad,
                    Cantidad = g.Sum(x => x.Cantidad),
                    MatUf = first.MatUf,
                    MoUf = first.MoUf,
                    Area = first.Area
                };
            })
            .OrderBy(x => x.Tipo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ExpandTemplateToRows(
        TemplateRow template,
        decimal baseQuantity,
        string itemId,
        IReadOnlyList<TemplateRow> plantillas,
        IReadOnlyList<MaterialRow> materiales,
        List<CubicacionRow> rows,
        List<string> warnings,
        HashSet<string> stack)
    {
        if (template == null) return;

        if (!stack.Add(template.Nombre ?? ""))
        {
            warnings.Add($"Ciclo detectado en plantilla: {template.Nombre}");
            return;
        }

        foreach (var item in template.Items ?? new List<TemplateItemRow>())
        {
            decimal factor = item.Factor <= 0m ? 1m : item.Factor;
            decimal qty = baseQuantity * factor;
            if (qty < item.Minimo) qty = item.Minimo;

            if (IsTemplateReference(item))
            {
                string childName = TemplateReferenceName(item);
                var child = plantillas.FirstOrDefault(p =>
                    string.Equals((p.Nombre ?? "").Trim(), childName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (child == null)
                {
                    warnings.Add($"Sub-plantilla no encontrada: {childName}");
                    continue;
                }

                ExpandTemplateToRows(child, qty, itemId ?? string.Empty, plantillas, materiales, rows, warnings, stack);
                continue;
            }

            var material = materiales.FirstOrDefault(m =>
                string.Equals((m.Codigo ?? "").Trim(), (item.Codigo ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

            if (material == null)
            {
                warnings.Add($"Material no encontrado: {item.Codigo}");
                continue;
            }

            rows.Add(new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId ?? string.Empty,
                Tipo = string.IsNullOrWhiteSpace(item.Tipo) ? "MAT" : item.Tipo,
                Codigo = material.Codigo,
                Descripcion = material.Descripcion,
                Unidad = material.Unidad,
                Cantidad = qty,
                MatUf = material.MatUf,
                MoUf = material.MoUf,
                Area = string.IsNullOrWhiteSpace(template.Area) ? "General" : template.Area
            });
        }

        stack.Remove(template.Nombre ?? "");
    }

    private static bool IsTemplateReference(TemplateItemRow item)
    {
        return string.Equals(item.Clase, "PLANTILLA", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(item.SubPlantilla);
    }

    private static string TemplateReferenceName(TemplateItemRow item)
    {
        return string.IsNullOrWhiteSpace(item.SubPlantilla) ? item.Codigo ?? "" : item.SubPlantilla;
    }
}
