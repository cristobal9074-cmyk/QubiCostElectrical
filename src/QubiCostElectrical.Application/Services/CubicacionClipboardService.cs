using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed class CubicacionClipboardRow
{
    public string? Codigo { get; init; }
    public string? Descripcion { get; init; }
    public string? Unidad { get; init; }
    public string? CantidadRaw { get; init; }
    /// <summary>Valores de casillas/sectores pegados a partir de la 5.ª columna (índice 0 = primer sector).</summary>
    public IReadOnlyList<string> SectorValues { get; init; } = Array.Empty<string>();
    public bool IsRejected { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class CubicacionClipboardParseResult
{
    public List<CubicacionClipboardRow> Rows { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public int RejectedCount { get; init; }
}

public static class CubicacionClipboardService
{
    private static readonly char[] ColumnSeparators = ['\t'];

    public static CubicacionClipboardParseResult ParseExcelText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new CubicacionClipboardParseResult();

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        var rows = new List<CubicacionClipboardRow>();
        var warnings = new List<string>();
        int rejectedCount = 0;
        int sectorColumnsSeen = 0;

        foreach (var line in lines)
        {
            var cells = line.Split(ColumnSeparators);
            string codigo = cells.Length > 0 ? cells[0].Trim() : "";
            string descripcion = cells.Length > 1 ? cells[1].Trim() : "";
            string unidad = cells.Length > 2 ? cells[2].Trim() : "";
            string cantidadRaw = cells.Length > 3 ? cells[3].Trim() : "";
            var sectorValues = cells.Length > 4
                ? cells.Skip(4).Select(c => c.Trim()).ToArray()
                : Array.Empty<string>();
            if (sectorValues.Length > sectorColumnsSeen)
                sectorColumnsSeen = sectorValues.Length;

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(descripcion) && string.IsNullOrWhiteSpace(cantidadRaw)
                && sectorValues.All(string.IsNullOrWhiteSpace))
                continue;

            bool rejected = false;
            string rejection = "";
            if (!TryAcceptPastedExpression(cantidadRaw, out string cantidadError))
            {
                rejected = true;
                rejection = cantidadError;
            }
            else
            {
                for (int s = 0; s < sectorValues.Length; s++)
                {
                    if (TryAcceptPastedExpression(sectorValues[s], out string sectorError))
                        continue;
                    rejected = true;
                    rejection = $"Casilla {s + 1}: {sectorError}";
                    break;
                }
            }

            if (rejected)
            {
                rows.Add(new CubicacionClipboardRow
                {
                    Codigo = codigo,
                    Descripcion = descripcion,
                    Unidad = unidad,
                    CantidadRaw = cantidadRaw,
                    SectorValues = sectorValues,
                    IsRejected = true,
                    RejectionReason = rejection
                });
                rejectedCount++;
                warnings.Add($"Fila rechazada — {rejection}");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(codigo))
                    warnings.Add($"Fila sin código: '{descripcion}'");
                rows.Add(new CubicacionClipboardRow
                {
                    Codigo = codigo,
                    Descripcion = descripcion,
                    Unidad = unidad,
                    CantidadRaw = cantidadRaw,
                    SectorValues = sectorValues
                });
            }
        }

        if (sectorColumnsSeen > 0)
            warnings.Add($"{sectorColumnsSeen} columna(s) de casilla/sector detectada(s) y se aplicarán al pegar.");

        return new CubicacionClipboardParseResult
        {
            Rows = rows,
            Warnings = warnings,
            RejectedCount = rejectedCount
        };
    }

    public static decimal ParseCantidad(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        if (TryAcceptPastedExpression(raw, out _) && !QuantityFormulaService.IsFormulaLike(raw))
        {
            string normalized = raw.Trim().Replace(",", ".");
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                return parsed;
        }

        var result = QuantityFormulaService.Evaluate(raw);
        return result.Success ? result.Value : 0m;
    }

    /// <summary>
    /// Número o fórmula del dialecto. Si la función no está en la lista, devuelve el error y no un cero.
    /// </summary>
    public static bool TryAcceptPastedExpression(string? raw, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        string text = raw.Trim();
        if (!QuantityFormulaService.IsFormulaLike(text))
        {
            string normalized = text.Replace(",", ".");
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                return true;
        }

        var result = QuantityFormulaService.ValidateDialect(text);
        if (result.Success)
            return true;

        error = string.IsNullOrWhiteSpace(result.Error)
            ? "fx · La fórmula no se puede calcular."
            : result.Error.StartsWith("fx", StringComparison.OrdinalIgnoreCase) ? result.Error : $"fx · {result.Error}";
        return false;
    }
}
