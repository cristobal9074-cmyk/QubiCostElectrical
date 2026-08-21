using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class FloorSummaryMaterialItemDto
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitClp { get; set; }
    public decimal SubtotalClp => Cantidad * PrecioUnitClp;
    public string Fuente { get; set; } = "Cubicación"; // "Cubicación" o "Cub. SC"
    public string Categoria { get; set; } = "Otros";
}

public sealed class FloorSummaryDto
{
    public string ZonaId { get; set; } = "";
    public string ZonaNombre { get; set; } = "Sin zona";
    public int Orden { get; set; }
    public decimal CostoTotalClp => Materiales.Sum(m => m.SubtotalClp);
    public int MaterialesUnicosCount => Materiales.Select(m => m.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public decimal CantidadTotal => Materiales.Sum(m => m.Cantidad);
    public List<FloorSummaryMaterialItemDto> Materiales { get; set; } = new();
}

public sealed class FloorSummaryService
{
    private static string DeterminarCategoria(string codigo, string descripcion, string tipo)
    {
        if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(descripcion))
            return "Otros";

        var descUpper = (descripcion ?? "").ToUpperInvariant();
        var codUpper = (codigo ?? "").ToUpperInvariant();

        if (codUpper.StartsWith("CON") || descUpper.Contains("CABLE") || descUpper.Contains("ALAMBRE") || descUpper.Contains("CONDUCTOR") || descUpper.Contains("THHN") || descUpper.Contains("NYA"))
            return "Conductores";

        if (codUpper.StartsWith("BAN") || codUpper.StartsWith("TUB") || descUpper.Contains("TUBERÍA") || descUpper.Contains("TUBERIA") || descUpper.Contains("BANDEJA") || descUpper.Contains("EMT") || descUpper.Contains("PVC") || descUpper.Contains("CANAL"))
            return "Cañerías y Bandejas";

        if (codUpper.StartsWith("ACC") || descUpper.Contains("CAJA") || descUpper.Contains("CONECTOR") || descUpper.Contains("CURVA") || descUpper.Contains("TERMINAL") || descUpper.Contains("SOPORTE") || descUpper.Contains("ABRAZADERA"))
            return "Accesorios";

        if (codUpper.StartsWith("LUM") || codUpper.StartsWith("ENC") || codUpper.StartsWith("INT") || descUpper.Contains("LUMINARIA") || descUpper.Contains("ENCHUFE") || descUpper.Contains("INTERRUPTOR") || descUpper.Contains("FOCO") || descUpper.Contains("SENSOR"))
            return "Artefactos SC";

        return "Otros";
    }

    public List<FloorSummaryDto> GetSummary(BudgetVersionData data, string consolidacionMode = "both")
    {
        var result = new List<FloorSummaryDto>();
        if (data == null) return result;

        // 1. Obtener todas las zonas activas del proyecto ordenadas por Orden
        var zonas = data.Zonas.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        var zonaMap = zonas.ToDictionary(z => z.Id, z => z, StringComparer.OrdinalIgnoreCase);

        // Inicializar el diccionario de grupos de resultados por ZonaId
        var summariesByZona = new Dictionary<string, FloorSummaryDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in zonas)
        {
            summariesByZona[z.Id] = new FloorSummaryDto
            {
                ZonaId = z.Id,
                ZonaNombre = z.Nombre,
                Orden = z.Orden
            };
        }

        // Grupo comodín para elementos sin zona
        var sinZonaSummary = new FloorSummaryDto
        {
            ZonaId = "",
            ZonaNombre = "Sin zona / General",
            Orden = 9999
        };

        // Indice de precios de materiales del catálogo para backup
        var catalogPrices = data.Materiales
            .GroupBy(m => m.CodigoNormalizado, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 2. Consolidar materiales de Cubicación Regular
        var recursosMap = data.CubicacionRecursos.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);
        var valorizacionesMap = data.CubicacionValorizaciones.ToDictionary(v => v.RecursoId, v => v, StringComparer.OrdinalIgnoreCase);

        foreach (var cantRow in data.CubicacionZonaCantidades)
        {
            if (cantRow.Cantidad <= 0m) continue;
            if (!recursosMap.TryGetValue(cantRow.RecursoId, out var recurso)) continue;
            if (string.IsNullOrWhiteSpace(recurso.Codigo)) continue;



            FloorSummaryDto targetSummary;
            if (string.IsNullOrWhiteSpace(cantRow.ZonaId) || !zonaMap.ContainsKey(cantRow.ZonaId))
            {
                targetSummary = sinZonaSummary;
            }
            else
            {
                targetSummary = summariesByZona[cantRow.ZonaId];
            }

            decimal precioClp = 0m;
            if (valorizacionesMap.TryGetValue(recurso.Id, out var val))
            {
                if (val.PrecioSnapshot != null)
                {
                    precioClp = (val.PrecioSnapshot.MatUf + val.PrecioSnapshot.MoUf) * data.Proyecto.ValorUfClp;
                }
                else
                {
                    precioClp = (val.MatUf + val.MoUf) * data.Proyecto.ValorUfClp;
                }
            }

            if (precioClp == 0m && catalogPrices.TryGetValue(CodeHelper.Normalize(recurso.Codigo), out var catMat))
            {
                precioClp = catMat.MatClpConFallback + catMat.MoClpConFallback;
            }

            var existing = targetSummary.Materiales.FirstOrDefault(m => string.Equals(m.Codigo, recurso.Codigo, StringComparison.OrdinalIgnoreCase) && m.Fuente == "Cubicación");
            if (existing != null)
            {
                existing.Cantidad += cantRow.Cantidad;
            }
            else
            {
                targetSummary.Materiales.Add(new FloorSummaryMaterialItemDto
                {
                    Codigo = recurso.Codigo,
                    Descripcion = recurso.Descripcion,
                    Unidad = recurso.Unidad,
                    Cantidad = cantRow.Cantidad,
                    PrecioUnitClp = precioClp,
                    Fuente = "Cubicación",
                    Categoria = DeterminarCategoria(recurso.Codigo, recurso.Descripcion, recurso.Tipo)
                });
            }
        }

        // 3. Consolidar materiales de Cubicación Servicios Comunes (Cub. SC)
        if (!string.Equals(consolidacionMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            var scGrupos = CubicacionSCEngine.AggregateMaterialesPorPiso(data.CubicacionSC, data.CubicacionScConfig, zonas);
            foreach (var grupo in scGrupos)
            {
                FloorSummaryDto targetSummary = sinZonaSummary;
                if (string.Equals(grupo.Nombre, "Sin piso", StringComparison.OrdinalIgnoreCase) || string.Equals(grupo.Nombre, "Sin zona", StringComparison.OrdinalIgnoreCase))
                {
                    targetSummary = sinZonaSummary;
                }
                else
                {
                    var matchingZona = zonas.FirstOrDefault(z => string.Equals(z.Nombre, grupo.Nombre, StringComparison.OrdinalIgnoreCase));
                    if (matchingZona != null)
                    {
                        targetSummary = summariesByZona[matchingZona.Id];
                    }
                }

                foreach (var scMat in grupo.Materiales)
                {
                    if (scMat.Cantidad <= 0m) continue;

                    string cleanName = scMat.Nombre.Trim();
                    string matchingCode = "";
                    decimal precioClp = 0m;
                    string desc = cleanName;

                    var matchedCatalogMat = data.Materiales.FirstOrDefault(m => string.Equals(m.Descripcion, cleanName, StringComparison.OrdinalIgnoreCase));
                    if (matchedCatalogMat != null)
                    {
                        matchingCode = matchedCatalogMat.Codigo;
                        precioClp = matchedCatalogMat.MatClpConFallback + matchedCatalogMat.MoClpConFallback;
                        desc = matchedCatalogMat.Descripcion;
                    }

                    var existing = targetSummary.Materiales.FirstOrDefault(m => string.Equals(m.Descripcion, desc, StringComparison.OrdinalIgnoreCase) && m.Fuente == "Cub. SC");
                    if (existing != null)
                    {
                        existing.Cantidad += scMat.Cantidad;
                    }
                    else
                    {
                        targetSummary.Materiales.Add(new FloorSummaryMaterialItemDto
                        {
                            Codigo = matchingCode,
                            Descripcion = desc,
                            Unidad = scMat.Unidad,
                            Cantidad = scMat.Cantidad,
                            PrecioUnitClp = precioClp,
                            Fuente = "Cub. SC",
                            Categoria = DeterminarCategoria(matchingCode, desc, "MAT")
                        });
                    }
                }
            }
        }

        foreach (var z in zonas)
        {
            var summary = summariesByZona[z.Id];
            if (summary.Materiales.Count > 0)
            {
                summary.Materiales = summary.Materiales
                    .OrderBy(m => m.Categoria)
                    .ThenBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result.Add(summary);
            }
        }

        if (sinZonaSummary.Materiales.Count > 0)
        {
            sinZonaSummary.Materiales = sinZonaSummary.Materiales
                .OrderBy(m => m.Categoria)
                .ThenBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Add(sinZonaSummary);
        }

        return result;
    }
}
