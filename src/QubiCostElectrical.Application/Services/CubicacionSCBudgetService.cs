using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Resultado del envío de cubicación C.SC al presupuesto.
/// </summary>
public sealed record SendCubicacionSCResult(
    int TotalMateriales,
    int ConPrecio,
    int SinPrecio,
    string PartidaDescripcion,
    bool HayFlexiblesSinConfig);

/// <summary>
/// Construye las filas de cubicación y la partida de itemizado a partir del resumen C.SC.
/// No accede a UI ni a SQLite — puro caso de uso de Application.
/// </summary>
public static class CubicacionSCBudgetService
{
    private const string PartidaDesc = "CUBICACION SERVICIOS COMUNES (C.SC)";
    private const string PartidaItem = "C.SC";

    /// <summary>
    /// Calcula si hay datos para enviar y si hay flexibles sin configurar.
    /// Devuelve null si no hay materiales.
    /// </summary>
    public static SendCubicacionSCResult? BuildResult(
        IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> totals,
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config)
    {
        if (totals.Count == 0) return null;

        bool sinConfig = CubicacionSCEngine.HasFlexiblesWithoutConfig(cuadros, config);
        return new SendCubicacionSCResult(
            TotalMateriales: totals.Count,
            ConPrecio: 0,
            SinPrecio: 0,
            PartidaDescripcion: PartidaDesc,
            HayFlexiblesSinConfig: sinConfig);
    }

    /// <summary>
    /// Aplica el resumen C.SC al itemizado y la cubicación del proyecto.
    /// Crea la partida C.SC si no existe. Reemplaza las filas previas de esa partida.
    /// Devuelve el resultado con conteo de materiales con/sin precio.
    /// </summary>
    public static SendCubicacionSCResult Apply(
        IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> totals,
        IEnumerable<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config,
        List<ItemizadoRow> itemizado,
        List<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentNullException.ThrowIfNull(itemizado);
        ArgumentNullException.ThrowIfNull(cubicacion);
        ArgumentNullException.ThrowIfNull(materiales);

        bool sinConfig = CubicacionSCEngine.HasFlexiblesWithoutConfig(cuadros, config);

        var partida = itemizado.FirstOrDefault(i =>
            string.Equals(i.Descripcion, PartidaDesc, StringComparison.OrdinalIgnoreCase));
        if (partida == null)
        {
            partida = new ItemizadoRow
            {
                Id = Guid.NewGuid().ToString("N"),
                Item = PartidaItem,
                Descripcion = PartidaDesc,
                Unidad = "GL",
                Cantidad = 1
            };
            itemizado.Add(partida);
        }

        cubicacion.RemoveAll(c => c.ItemId == partida.Id);

        var catalogoByDescripcion = materiales
            .GroupBy(m => m.Descripcion, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var catalogoByCodigo = materiales
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        int conPrecio = 0, codigoSeq = 1;

        foreach (var kv in totals.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var mat = ResolveMaterial(kv.Key, catalogoByDescripcion, catalogoByCodigo);
            cubicacion.Add(new CubicacionRow
            {
                ItemId  = partida.Id,
                Tipo    = "MAT",
                Codigo  = mat?.Codigo ?? $"CSC-{codigoSeq:000}",
                Descripcion = kv.Key,
                Unidad  = mat?.Unidad is { Length: > 0 } u ? u : kv.Value.Unidad,
                Cantidad = Math.Round(kv.Value.Cantidad, 2),
                MatUf   = mat?.MatUf ?? 0m,
                MoUf    = mat?.MoUf  ?? 0m,
                Area    = "C.SC"
            });
            if (mat != null) conPrecio++;
            codigoSeq++;
        }

        return new SendCubicacionSCResult(
            TotalMateriales: totals.Count,
            ConPrecio: conPrecio,
            SinPrecio: totals.Count - conPrecio,
            PartidaDescripcion: PartidaDesc,
            HayFlexiblesSinConfig: sinConfig);
    }

    private static MaterialRow? ResolveMaterial(
        string key,
        IReadOnlyDictionary<string, MaterialRow> byDescripcion,
        IReadOnlyDictionary<string, MaterialRow> byCodigo)
    {
        if (byDescripcion.TryGetValue(key, out var mat))
            return mat;
        if (byCodigo.TryGetValue(key, out mat))
            return mat;
        return null;
    }

    /// <summary>
    /// Construye un hash del resumen para detectar cambios pendientes de envío.
    /// </summary>
    public static string BuildResumenHash(IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> totals)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in totals.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sb.Append(kv.Key).Append('=')
              .Append(kv.Value.Cantidad.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
              .Append(';');
        return sb.ToString();
    }
}
