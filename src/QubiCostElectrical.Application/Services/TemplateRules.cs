using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class TemplateRules
{
    public static bool IsTemplateReference(TemplateItemRow item)
    {
        if (item == null) return false;
        return string.Equals((item.Clase ?? "").Trim(), "PLANTILLA", StringComparison.OrdinalIgnoreCase)
            || string.Equals((item.Tipo ?? "").Trim(), "TPL", StringComparison.OrdinalIgnoreCase);
    }

    public static string TemplateReferenceName(TemplateItemRow item)
    {
        if (item == null) return "";
        string name = (item.SubPlantilla ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = (item.Codigo ?? "").Trim();
        return name;
    }

    public static bool WouldCreateCycle(
        string parentTemplateName,
        string childTemplateName,
        IReadOnlyList<TemplateRow> plantillas)
    {
        ArgumentNullException.ThrowIfNull(plantillas);
        string parent = (parentTemplateName ?? "").Trim();
        string child  = (childTemplateName  ?? "").Trim();
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(child)) return false;
        if (string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)) return true;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string name)
        {
            if (string.Equals(name, parent, StringComparison.OrdinalIgnoreCase)) return true;
            if (!visited.Add(name)) return false;

            var tpl = plantillas.FirstOrDefault(p =>
                string.Equals(p.Nombre.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (tpl == null) return false;

            foreach (var item in tpl.Items.Where(IsTemplateReference))
            {
                string next = TemplateReferenceName(item);
                if (!string.IsNullOrWhiteSpace(next) && Visit(next)) return true;
            }
            return false;
        }

        return Visit(child);
    }

    public static string UniqueTemplateName(string baseName, IReadOnlyList<TemplateRow> plantillas)
    {
        ArgumentNullException.ThrowIfNull(plantillas);
        string name = baseName ?? "";
        int i = 2;
        while (plantillas.Any(p =>
            string.Equals(p.Nombre.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {i}";
            i++;
        }
        return name;
    }

    public static List<CubicacionRow> MergeRows(IEnumerable<CubicacionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .GroupBy(x => new
            {
                Codigo  = (x.Codigo  ?? "").Trim().ToUpperInvariant(),
                Tipo    = (x.Tipo    ?? "").Trim().ToUpperInvariant(),
                Unidad  = (x.Unidad  ?? "").Trim().ToUpperInvariant(),
                Area    = (x.Area    ?? "").Trim().ToUpperInvariant(),
                x.MatUf,
                x.MoUf
            })
            .Select(g =>
            {
                var first = g.First();
                return new CubicacionRow
                {
                    ItemId      = first.ItemId,
                    Tipo        = first.Tipo,
                    Codigo      = first.Codigo,
                    Descripcion = first.Descripcion,
                    Unidad      = first.Unidad,
                    Cantidad    = g.Sum(x => x.Cantidad),
                    MatUf       = first.MatUf,
                    MoUf        = first.MoUf,
                    Area        = first.Area
                };
            })
            .OrderBy(x => x.Tipo)
            .ThenBy(x => x.Codigo)
            .ToList();
    }
}
