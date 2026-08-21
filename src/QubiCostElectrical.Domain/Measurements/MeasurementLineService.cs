using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Calcula parciales y totales de líneas de medición al estilo Presto / Arquímedes.
/// Reglas de cálculo:
///  - Si hay <see cref="MeasurementLineRow.FormulaParcial"/>, se evalúa con QuantityFormulaService.
///  - Si no, el parcial es el producto de los factores Nº×Longitud×Anchura×Altura,
///    ignorando los factores en 0 (un factor vacío no anula la medición, vale 1).
///  - El signo (+1/-1) permite registrar deducciones (huecos, descuentos).
/// </summary>
public static class MeasurementLineService
{
    /// <summary>Parcial de una sola línea, con su signo aplicado.</summary>
    public static decimal ComputeParcial(
        MeasurementLineRow line,
        IReadOnlyDictionary<string, decimal>? variables = null)
    {
        if (line == null) return 0m;

        int signo = line.Signo >= 0 ? 1 : -1;

        if (!string.IsNullOrWhiteSpace(line.FormulaParcial))
        {
            var result = QuantityFormulaService.Evaluate(line.FormulaParcial, variables);
            return result.Success ? signo * result.Value : 0m;
        }

        decimal[] factores = { line.Unidades, line.Longitud, line.Anchura, line.Altura };
        decimal[] usados = factores.Where(f => f != 0m).ToArray();
        if (usados.Length == 0) return 0m;

        decimal producto = usados.Aggregate(1m, (acc, f) => acc * f);
        return signo * producto;
    }

    /// <summary>Suma de los parciales de todas las líneas (cantidad medida de la partida).</summary>
    public static decimal ComputeTotal(
        IEnumerable<MeasurementLineRow> lines,
        IReadOnlyDictionary<string, decimal>? variables = null)
    {
        if (lines == null) return 0m;
        return lines.Sum(l => ComputeParcial(l, variables));
    }

    /// <summary>
    /// Representación legible tipo Presto de una línea: "Comentario  3×4,50×0,30 = 4,05".
    /// Útil para exportar el detalle de medición a PDF/Excel y para tooltips en la grilla.
    /// </summary>
    public static string FormatPresto(
        MeasurementLineRow line,
        IReadOnlyDictionary<string, decimal>? variables = null,
        CultureInfo? culture = null)
    {
        if (line == null) return string.Empty;
        culture ??= CultureInfo.GetCultureInfo("es-CL");

        string Num(decimal v) => v.ToString("0.###", culture);

        decimal parcial = ComputeParcial(line, variables);
        string detalle;

        if (!string.IsNullOrWhiteSpace(line.FormulaParcial))
        {
            detalle = line.FormulaParcial.Trim();
        }
        else
        {
            var factores = new List<string>();
            if (line.Unidades != 0m) factores.Add(Num(line.Unidades));
            if (line.Longitud != 0m) factores.Add(Num(line.Longitud));
            if (line.Anchura != 0m) factores.Add(Num(line.Anchura));
            if (line.Altura != 0m) factores.Add(Num(line.Altura));
            detalle = factores.Count == 0 ? "0" : string.Join("×", factores);
        }

        string signo = line.Signo < 0 ? "-" : "";
        string comentario = string.IsNullOrWhiteSpace(line.Comentario)
            ? string.Empty
            : line.Comentario.Trim() + "  ";

        return $"{comentario}{signo}{detalle} = {Num(parcial)}";
    }

    /// <summary>Agrupa los parciales por área/zona para reporting (ej. por piso).</summary>
    public static IReadOnlyDictionary<string, decimal> TotalsByArea(
        IEnumerable<MeasurementLineRow> lines,
        IReadOnlyDictionary<string, decimal>? variables = null)
    {
        if (lines == null) return new Dictionary<string, decimal>();
        return lines
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Area) ? "General" : l.Area.Trim())
            .ToDictionary(g => g.Key, g => g.Sum(l => ComputeParcial(l, variables)));
    }
}
