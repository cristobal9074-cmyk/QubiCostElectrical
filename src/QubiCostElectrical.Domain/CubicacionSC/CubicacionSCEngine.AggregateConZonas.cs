using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static partial class CubicacionSCEngine
{
    public sealed record MaterialConZonasSC(
        string Nombre, string Unidad, string Categoria, decimal Total,
        IReadOnlyList<(string ZonaNombre, decimal Cantidad)> Zonas);

    public static IReadOnlyList<MaterialConZonasSC> AggregateConZonas(
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config,
        IReadOnlyList<ZonaProyecto>? zonas = null)
    {
        ArgumentNullException.ThrowIfNull(cuadros);
        ArgumentNullException.ThrowIfNull(config);

        var dict = new Dictionary<string, Dictionary<string, (decimal Cantidad, string Unidad)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cuadro in cuadros)
        foreach (var c in cuadro.Circuitos)
        {
            if (!c.EsActivo && !c.EsReservaEquipada) continue;
            foreach (var (piso, mat) in CircuitoMaterialesPorPiso(c, config))
            {
                if (!dict.TryGetValue(mat.Nombre, out var zonasDict))
                {
                    zonasDict = new Dictionary<string, (decimal, string)>(StringComparer.OrdinalIgnoreCase);
                    dict[mat.Nombre] = zonasDict;
                }
                var prev = zonasDict.TryGetValue(piso, out var existing) ? existing.Cantidad : 0m;
                zonasDict[piso] = (prev + mat.Cantidad, mat.Unidad);
            }
        }

        var ordenZonas = (zonas ?? Array.Empty<ZonaProyecto>())
            .Select((z, i) => (z.Nombre, Orden: i))
            .GroupBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Orden, StringComparer.OrdinalIgnoreCase);

        var result = new List<MaterialConZonasSC>();
        foreach (var (nombre, zonasDict) in dict)
        {
            var unidad = zonasDict.Values.First().Unidad;
            var total = zonasDict.Values.Sum(z => z.Cantidad);
            var categoria = GetCategoriaFromNombre(nombre);

            var zonasList = new List<(string, decimal)>();
            foreach (var (zNombre, orden) in ordenZonas.OrderBy(kv => kv.Value))
            {
                var qty = zonasDict.TryGetValue(zNombre, out var q) ? q.Cantidad : 0m;
                zonasList.Add((zNombre, qty));
            }
            foreach (var kv in zonasDict)
            {
                if (!ordenZonas.ContainsKey(kv.Key))
                    zonasList.Add((kv.Key, kv.Value.Cantidad));
            }

            result.Add(new MaterialConZonasSC(nombre, unidad, categoria, total, zonasList));
        }

        return result.OrderBy(m => m.Categoria).ThenBy(m => m.Nombre).ToList();
    }

    private static string GetCategoriaFromNombre(string nombre)
    {
        if (nombre.StartsWith("TUBERIA FLEXIBLE", StringComparison.OrdinalIgnoreCase))
            return "Flexible";
        if (nombre.StartsWith("TUBERIA", StringComparison.OrdinalIgnoreCase))
            return "Tubería";
        if (nombre.StartsWith("CONDUCTOR", StringComparison.OrdinalIgnoreCase))
            return "Conductor";
        if (nombre.StartsWith("CAJA", StringComparison.OrdinalIgnoreCase))
            return "Cajas";
        if (nombre.StartsWith("SENSOR", StringComparison.OrdinalIgnoreCase))
            return "Sensores";
        if (nombre.StartsWith("SEÑALETICA", StringComparison.OrdinalIgnoreCase))
            return "Señalética";
        if (nombre.StartsWith("CINTA", StringComparison.OrdinalIgnoreCase))
            return "Cinta LED";
        if (nombre.StartsWith("MONTAJE", StringComparison.OrdinalIgnoreCase))
            return "Montaje";
        if (nombre.StartsWith("INT", StringComparison.OrdinalIgnoreCase))
            return "Interruptores";
        return "Artefactos";
    }
}
