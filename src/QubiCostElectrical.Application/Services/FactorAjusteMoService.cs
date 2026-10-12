using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Arma las entradas del cálculo del FA a partir del proyecto y deriva el índice.
/// La M.O. objetivo es la misma que usa el puente de costos (módulo Mano de obra, o dotación ×
/// sueldo × leyes sociales cuando el módulo no dejó total) y la base es el material de las filas
/// en fórmula "Porcentaje" del itemizado.
/// </summary>
public static class FactorAjusteMoService
{
    public static FactorAjusteMoResultado CalcularParaProyecto(ProjectFileData? data, ProjectInfoRow? proyecto)
    {
        data ??= new ProjectFileData();
        var project = proyecto ?? data.Proyecto ?? new ProjectInfoRow();
        decimal moReal = ResolverMoRealClp(data, project);
        return FactorAjusteMoCalculator.Calcular(moReal, ConstruirLineas(data.Cubicacion, data.Materiales, project));
    }

    /// <summary>
    /// M.O. real del proyecto: el total del módulo Mano de obra si existe; si no, la dotación
    /// (directa + indirecta) × sueldo × (1 + leyes sociales), igual que <see cref="ExcelCostosBridge"/>.
    /// </summary>
    public static decimal ResolverMoRealClp(ProjectFileData? data, ProjectInfoRow? proyecto)
    {
        var project = proyecto ?? new ProjectInfoRow();
        if (project.MoTotalClp > 0m)
            return project.MoTotalClp;

        data ??= new ProjectFileData();
        int meses = project.MoDuracionMeses > 0 ? project.MoDuracionMeses : 12;
        return ExcelCostosBridge.SumMoDotacionClp(
            data.MoDirecta, data.MoIndirecta, fallbackMonths: meses, leyesSocialesPct: project.LeyesSocialesMoPct);
    }

    /// <summary>
    /// Días-hombre del plan de M.O. (columna TOTAL DD/HH del Excel). Sirve para que el estimador
    /// lea el FA contra la tarifa D/H y verifique que M.O. del itemizado y plan de obra cuadran.
    /// </summary>
    public static decimal ResolverDiasHombre(ProjectFileData? data, ProjectInfoRow? proyecto)
    {
        var project = proyecto ?? new ProjectInfoRow();
        data ??= new ProjectFileData();
        int meses = project.MoDuracionMeses > 0 ? project.MoDuracionMeses : 12;
        return ExcelCostosBridge.SumDiasHombre(data.MoDirecta, data.MoIndirecta, fallbackMonths: meses);
    }

    /// <summary>
    /// Una línea por fila de cubicación con cantidad: separa la M.O. que ya cubre el catálogo
    /// (directa o por rendimiento) de la base que multiplica el FA (Material × %).
    /// </summary>
    public static IReadOnlyList<FactorAjusteMoLinea> ConstruirLineas(
        IEnumerable<CubicacionRow>? cubicacion,
        IEnumerable<MaterialRow>? materiales,
        ProjectInfoRow? proyecto)
    {
        var catalogo = new Dictionary<string, MaterialRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in materiales ?? Enumerable.Empty<MaterialRow>())
        {
            if (string.IsNullOrWhiteSpace(material.Codigo))
                continue;
            catalogo[material.Codigo.Trim()] = material;
        }

        var lineas = new List<FactorAjusteMoLinea>();
        foreach (var row in cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            if (row.Cantidad <= 0m)
                continue;

            MaterialRow? material = null;
            if (!string.IsNullOrWhiteSpace(row.Codigo))
                catalogo.TryGetValue(row.Codigo.Trim(), out material);

            lineas.Add(ConstruirLinea(material, row, proyecto));
        }

        return lineas;
    }

    private static FactorAjusteMoLinea ConstruirLinea(MaterialRow? material, CubicacionRow row, ProjectInfoRow? proyecto)
    {
        // Fila sin material en el catálogo: su M.O. ya viene resuelta en la fila y no depende del FA.
        if (material == null)
            return new FactorAjusteMoLinea(row.MoClpConFallback, 0m, row.Cantidad);

        // Misma precedencia que el motor: rendimiento primero, luego fórmula de porcentaje.
        if (MaterialMoResolver.DebeUsarRendimiento(material, proyecto))
            return new FactorAjusteMoLinea(FijaConRespaldo(MaterialMoResolver.MoClpEfectiva(material, proyecto), row), 0m, row.Cantidad);

        if (MaterialMoResolver.DebeUsarPorcentaje(material))
        {
            decimal matClp = material.MatClp > 0m ? material.MatClp : row.MatClpConFallback;
            return new FactorAjusteMoLinea(0m, matClp * (material.MoPorcentajeMaterial / 100m), row.Cantidad);
        }

        return new FactorAjusteMoLinea(FijaConRespaldo(material.MoClp, row), 0m, row.Cantidad);
    }

    private static decimal FijaConRespaldo(decimal moCatalogo, CubicacionRow row) =>
        moCatalogo > 0m ? moCatalogo : row.MoClpConFallback;
}
