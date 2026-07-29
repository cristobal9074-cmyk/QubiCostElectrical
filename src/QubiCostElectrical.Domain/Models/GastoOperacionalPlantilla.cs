using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Plantilla estándar de Gastos Operacionales (3.1–3.6).</summary>
public static class GastoOperacionalPlantilla
{
    public static List<GastoOperacionalRow> CreateClean()
    {
        static GastoOperacionalRow Cat(string codigo, string concepto, params (string concepto, string un)[] items)
            => new()
            {
                Codigo = codigo,
                Concepto = concepto,
                MontoClp = 0,
                Uf = 0,
                Items = items.Select(i => new GastoOperacionalItemRow
                {
                    Concepto = i.concepto,
                    Unidad = i.un,
                    Cantidad = 0,
                    PrecioUnitarioClp = 0,
                }).ToList(),
            };

        return
        [
            Cat("3.1", "Gastos de oficina",
                ("Instalación de faena (oficina, bodega, container)", "GL"),
                ("Teléfono e internet", "mes"),
                ("Radios de comunicación (arriendo)", "mes"),
                ("Implementos de oficina", "mes"),
                ("Gastos bancarios y notariales", "mes")),
            Cat("3.2", "Arriendos y mant. herramientas",
                ("Arriendo de grúas", "día"),
                ("Arriendo plataformas elevadoras", "día"),
                ("Arriendo de andamios", "mes"),
                ("Mantención herramientas menores", "mes"),
                ("Fletes internos en obra", "GL")),
            Cat("3.3", "Movilización y viáticos",
                ("Pasajes MO directa", "día"),
                ("Viáticos MO indirecta", "mes"),
                ("Arriendo / traslado en bus", "viaje")),
            Cat("3.4", "Transporte de materiales / fletes",
                ("Vehículo de terreno (arriendo + combustible)", "mes"),
                ("Fletes especiales (conductores, tableros)", "GL")),
            Cat("3.5", "Costos por EPP",
                ("EPP indumentaria MO directa", "GL"),
                ("EPP indumentaria MO indirecta", "GL"),
                ("EPP accesorios opcionales", "GL")),
            Cat("3.6", "Trámites y documentación",
                ("Ensayos eléctricos y protocolos", "GL"),
                ("Boletas de garantía", "un"),
                ("Certificaciones SEC", "GL"),
                ("Trámites municipales y permisos", "GL")),
        ];
    }

    /// <summary>
    /// Si el GO está vacío o le faltan categorías estándar (p. ej. solo quedó 3.5 tras un sync),
    /// inserta las faltantes sin borrar las existentes ni sus montos.
    /// </summary>
    public static bool EnsureComplete(ProjectFileData data)
    {
        if (data == null)
            return false;

        data.GastosOperacionales ??= new List<GastoOperacionalRow>();
        var list = data.GastosOperacionales;
        var plantilla = CreateClean();
        var changed = false;

        if (list.Count == 0)
        {
            list.AddRange(plantilla);
            return true;
        }

        foreach (var std in plantilla)
        {
            var exists = list.Any(c =>
                string.Equals(c.Codigo, std.Codigo, StringComparison.OrdinalIgnoreCase));
            if (exists)
                continue;

            // Insertar en orden de código.
            var idx = list.FindIndex(c =>
                string.Compare(c.Codigo, std.Codigo, StringComparison.OrdinalIgnoreCase) > 0);
            if (idx < 0)
                list.Add(CloneCategory(std));
            else
                list.Insert(idx, CloneCategory(std));
            changed = true;
        }

        return changed;
    }

    private static GastoOperacionalRow CloneCategory(GastoOperacionalRow src) => new()
    {
        Codigo = src.Codigo,
        Concepto = src.Concepto,
        MontoClp = 0,
        Uf = 0,
        Items = src.Items.Select(i => new GastoOperacionalItemRow
        {
            Concepto = i.Concepto,
            Unidad = i.Unidad,
            Cantidad = i.Cantidad,
            PrecioUnitarioClp = i.PrecioUnitarioClp,
        }).ToList(),
    };
}
