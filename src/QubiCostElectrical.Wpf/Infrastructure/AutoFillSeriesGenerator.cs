using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Motor de generación de series y secuencias para auto-relleno estilo Excel (Fill Handle).
/// Soporta:
/// - Secuencias numéricas simples (201, 202 -> 203, 204, 205...)
/// - Texto con sufijos numéricos (Depto 201, Depto 202 -> Depto 203, Depto 204...)
/// - Pasos aritméticos variables (10, 20 -> 30, 40... o 100, 95 -> 90, 85...)
/// - Fórmulas con desplazamiento de referencias (=C1*2 -> =D1*2 o =C2*2)
/// - Relleno cíclico o replicación para textos puros sin números
/// </summary>
public static class AutoFillSeriesGenerator
{
    private static readonly Regex TrailingNumberRegex = new(@"^(.*?)(\d+)(\D*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CellRefRegex = new(@"\b([A-Za-z]+)(\d+)\b", RegexOptions.Compiled);

    public record ParsedNumber(bool HasNumber, string Prefix, decimal Number, string RawNumberStr, string Suffix);

    public static ParsedNumber ParseTrailingNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedNumber(false, text ?? "", 0m, "", "");

        string trimmed = text.Trim();

        // Si es decimal directo (ej. 201 o 12.5 o 12,5)
        if (decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pureDecimal))
        {
            return new ParsedNumber(true, "", pureDecimal, trimmed, "");
        }

        var m = TrailingNumberRegex.Match(trimmed);
        if (m.Success && decimal.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
        {
            return new ParsedNumber(true, m.Groups[1].Value, val, m.Groups[2].Value, m.Groups[3].Value);
        }

        return new ParsedNumber(false, text, 0m, "", "");
    }

    public static List<string> GenerateNextValues(IReadOnlyList<string> source, int count, bool isHorizontal, bool isBackward = false)
    {
        var result = new List<string>(count);
        if (source.Count == 0 || count <= 0) return result;

        // Fórmulas de Excel: desplazar referencias de celda
        if (source.All(s => s.StartsWith("=", StringComparison.Ordinal) || s.StartsWith("+", StringComparison.Ordinal)))
        {
            for (int i = 0; i < count; i++)
            {
                int offset = (i + 1) * (isBackward ? -1 : 1);
                string baseFormula = source[i % source.Count];
                result.Add(ShiftFormula(baseFormula, isHorizontal ? offset : 0, isHorizontal ? 0 : offset));
            }
            return result;
        }

        // Números y textos con números
        var parsed = source.Select(ParseTrailingNumber).ToList();
        bool allHaveNumbers = parsed.All(p => p.HasNumber);

        if (allHaveNumbers)
        {
            string prefix = parsed[0].Prefix;
            string suffix = parsed[0].Suffix;
            bool sameAffixes = parsed.All(p => string.Equals(p.Prefix, prefix, StringComparison.OrdinalIgnoreCase)
                                            && string.Equals(p.Suffix, suffix, StringComparison.OrdinalIgnoreCase));

            if (sameAffixes)
            {
                decimal step = 1m;
                if (parsed.Count >= 2)
                {
                    decimal diff = parsed[^1].Number - parsed[0].Number;
                    step = diff / (parsed.Count - 1);
                    if (step == 0m) step = 1m;
                }
                else
                {
                    step = 1m;
                }

                if (isBackward)
                    step = -step;

                decimal lastNum = isBackward ? parsed[0].Number : parsed[^1].Number;
                int padLength = parsed[^1].RawNumberStr.Length;
                bool padWithZeros = padLength > 1 && parsed[^1].RawNumberStr.StartsWith("0");

                for (int i = 0; i < count; i++)
                {
                    decimal nextNum = lastNum + step * (i + 1);
                    string numStr;
                    if (nextNum % 1 == 0)
                    {
                        long intVal = (long)nextNum;
                        numStr = padWithZeros && intVal >= 0 ? intVal.ToString($"D{padLength}") : intVal.ToString();
                    }
                    else
                    {
                        numStr = nextNum.ToString("0.##", CultureInfo.InvariantCulture);
                    }

                    result.Add(prefix + numStr + suffix);
                }
                return result;
            }
        }

        // Caso por defecto: repetición cíclica
        for (int i = 0; i < count; i++)
        {
            int index = isBackward
                ? (source.Count - 1 - (i % source.Count))
                : (i % source.Count);
            result.Add(source[index]);
        }
        return result;
    }

    private static string ShiftFormula(string formula, int colOffset, int rowOffset)
    {
        if (colOffset == 0 && rowOffset == 0) return formula;

        return CellRefRegex.Replace(formula, m =>
        {
            string colLetter = m.Groups[1].Value.ToUpperInvariant();
            if (!int.TryParse(m.Groups[2].Value, out int rowNum))
                return m.Value;

            int colIdx = ColumnLetterToIndex(colLetter);
            if (colIdx < 0) return m.Value;

            int newColIdx = Math.Max(0, colIdx + colOffset);
            int newRowNum = Math.Max(1, rowNum + rowOffset);

            return IndexToColumnLetter(newColIdx) + newRowNum;
        });
    }

    private static int ColumnLetterToIndex(string name)
    {
        int sum = 0;
        foreach (char c in name)
        {
            if (c < 'A' || c > 'Z') return -1;
            sum = sum * 26 + (c - 'A' + 1);
        }
        return sum - 1;
    }

    private static string IndexToColumnLetter(int zeroBasedIndex)
    {
        int dividend = zeroBasedIndex + 1;
        string name = "";
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            name = (char)('A' + modulo) + name;
            dividend = (dividend - modulo - 1) / 26;
        }
        return name;
    }
}
