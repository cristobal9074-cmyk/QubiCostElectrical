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
        bool hasExtraColumns = false;

        foreach (var line in lines)
        {
            var cells = line.Split(ColumnSeparators);
            if (cells.Length > 4)
                hasExtraColumns = true;
            string codigo = cells.Length > 0 ? cells[0].Trim() : "";
            string descripcion = cells.Length > 1 ? cells[1].Trim() : "";
            string unidad = cells.Length > 2 ? cells[2].Trim() : "";
            string cantidadRaw = cells.Length > 3 ? cells[3].Trim() : "";

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(descripcion) && string.IsNullOrWhiteSpace(cantidadRaw))
                continue;

            bool cantidadInvalida = false;
            if (!string.IsNullOrWhiteSpace(cantidadRaw))
            {
                string normalized = cantidadRaw.Replace(",", ".");
                cantidadInvalida = !decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                    && !QuantityFormulaService.IsFormulaLike(cantidadRaw);

                if (cantidadInvalida)
                {
                    var result = QuantityFormulaService.Evaluate(cantidadRaw);
                    cantidadInvalida = !result.Success;
                }
            }

            if (cantidadInvalida)
            {
                rows.Add(new CubicacionClipboardRow
                {
                    Codigo = codigo,
                    Descripcion = descripcion,
                    Unidad = unidad,
                    CantidadRaw = cantidadRaw,
                    IsRejected = true,
                    RejectionReason = $"Cantidad inválida: '{cantidadRaw}'"
                });
                rejectedCount++;
                warnings.Add($"Fila rechazada — cantidad no reconocida: '{cantidadRaw}'");
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
                    CantidadRaw = cantidadRaw
                });
            }
        }

        if (hasExtraColumns)
            warnings.Add("Se pegaron columnas más allá de Código/Descripción/Unidad/Cantidad; el resto se ignoró.");

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
        string normalized = raw.Trim().Replace(",", ".");
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
            return parsed;
        var result = QuantityFormulaService.Evaluate(raw);
        return result.Success ? result.Value : 0m;
    }
}
