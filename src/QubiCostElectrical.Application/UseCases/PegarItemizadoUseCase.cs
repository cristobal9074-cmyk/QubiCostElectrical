using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

/// <summary>
/// Resultado de parsear texto clipboard como itemizado.
/// </summary>
public sealed record PegarItemizadoResult(
    IReadOnlyList<ItemizadoRow> Rows,
    string ColumnMap,
    string Warning);

/// <summary>
/// Caso de uso: convierte texto tab-separado del portapapeles en una lista de ItemizadoRow.
/// Detecta columnas automáticamente (con o sin encabezados) y clasifica títulos y partidas.
/// Sin dependencias de UI ni de archivos — se puede testear directamente.
/// </summary>
public sealed class PegarItemizadoUseCase
{
    public PegarItemizadoResult Execute(string clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
            return new PegarItemizadoResult(Array.Empty<ItemizadoRow>(), "", "Texto vacío");

        var table = ParseTable(clipboardText);
        if (table.Count == 0)
            return new PegarItemizadoResult(Array.Empty<ItemizadoRow>(), "", "Sin datos");

        var (headerRow, extraHeaderRow) = ItemizadoImportParser.FindHeaderRowRange(table, "itemizado");

        int cItem, cDesc, cUnidad, cCant;
        string columnMap;
        int dataStart;

        if (headerRow >= 0)
        {
            var h = table[headerRow];
            // Cuando hay 2 filas de encabezado, las columnas de unidad/cant pueden estar en la siguiente
            var extra = extraHeaderRow >= 0 ? table[extraHeaderRow] : null;
            cItem = ItemizadoImportParser.FindColumn(h, extra, "ITEM", "ÍTEM", "NITEM", "NUMEROITEM", "PARTIDA", "N°", "NUM");
            cDesc = ItemizadoImportParser.FindColumn(h, extra, "DESCRIPCION", "DESCRIPCIÓN", "DETALLE", "NOMBRE", "PARTIDADESCRIPCION", "PARTIDA");
            cUnidad = ItemizadoImportParser.FindColumn(h, extra, "UNIDAD", "UD", "UN", "UND", "UNIT", "UNID", "UNIDADES");
            cCant = ItemizadoImportParser.FindColumn(h, extra, "CANTIDAD", "CANT", "QTY", "CANTIDADITEM", "CANTIDADES", "NUEVASCANTIDADES");
            int lastHeader = extraHeaderRow >= 0 ? extraHeaderRow : headerRow;
            dataStart = lastHeader + 1;
            columnMap = BuildColumnMap(cItem, cDesc, cUnidad, cCant, table[headerRow]);
        }
        else
        {
            (cItem, cDesc, cUnidad, cCant) = DetectColumnsHeuristic(table);
            dataStart = 0;
            columnMap = BuildColumnMap(cItem, cDesc, cUnidad, cCant, null);
        }

        if (cDesc < 0)
            return new PegarItemizadoResult(Array.Empty<ItemizadoRow>(), columnMap, "No se encontró columna de descripción");

        var rows = BuildRows(table, dataStart, cItem, cDesc, cUnidad, cCant);
        string? limitError = ItemizadoImportValidation.ValidateRowCount(rows.Count);
        if (limitError != null)
            return new PegarItemizadoResult(Array.Empty<ItemizadoRow>(), columnMap, limitError);

        return new PegarItemizadoResult(rows, columnMap, "");
    }

    // ─── Parser de tabla ─────────────────────────────────────────────────

    /// <summary>Convierte texto clipboard (tabs y saltos de línea) en tabla de celdas.</summary>
    public static List<List<string>> ParseTable(string text)
    {
        var result = new List<List<string>>();
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        foreach (string line in lines)
        {
            string trimmed = line.TrimEnd();
            if (string.IsNullOrEmpty(trimmed)) continue;
            result.Add(trimmed.Split('\t').Select(c => c.Trim()).ToList());
        }

        return result;
    }

    // ─── Detección posicional de columnas ─────────────────────────────────

    private static (int cItem, int cDesc, int cUnidad, int cCant) DetectColumnsHeuristic(
        List<List<string>> table)
    {
        var sample = table.Take(Math.Min(table.Count, 30)).ToList();
        if (sample.Count == 0) return (-1, 0, -1, -1);

        int colCount = sample.Max(r => r.Count);
        if (colCount == 0) return (-1, 0, -1, -1);

        // Columna única: todo es descripción
        if (colCount == 1) return (-1, 0, -1, -1);

        var avgLen = new double[colCount];
        var unitScore = new int[colCount];
        var numScore = new int[colCount];
        var codeScore = new int[colCount];

        for (int c = 0; c < colCount; c++)
        {
            var vals = sample
                .Select(r => ItemizadoImportParser.GetCell(r, c))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
            if (vals.Count == 0) continue;

            avgLen[c] = vals.Average(v => v.Length);

            foreach (string v in vals)
            {
                if (IsKnownUnit(v)) unitScore[c]++;
                if (IsNumericCell(v)) numScore[c]++;
                if (IsCodeLike(v)) codeScore[c]++;
            }
        }

        // Descripción: mayor longitud promedio, no es unidad
        int cDesc = -1;
        double bestLen = 0;
        for (int c = 0; c < colCount; c++)
        {
            if (avgLen[c] > bestLen && unitScore[c] == 0)
            { bestLen = avgLen[c]; cDesc = c; }
        }
        if (cDesc < 0) cDesc = colCount > 1 ? 1 : 0;

        // Ítem: columna antes de descripción con patrón de código
        int cItem = -1;
        for (int c = 0; c < cDesc; c++)
        {
            if (codeScore[c] > 0 || numScore[c] > 0) { cItem = c; break; }
        }

        // Unidad: mayor score de unidades, no es descripción ni ítem
        int cUnidad = -1;
        int bestUnit = 0;
        for (int c = 0; c < colCount; c++)
        {
            if (c == cDesc || c == cItem) continue;
            if (unitScore[c] > bestUnit) { bestUnit = unitScore[c]; cUnidad = c; }
        }

        // Cantidad: numérica después de descripción, no es unidad
        int cCant = -1;
        for (int c = cDesc + 1; c < colCount; c++)
        {
            if (c == cUnidad) continue;
            if (numScore[c] > 0) { cCant = c; break; }
        }

        return (cItem, cDesc, cUnidad, cCant);
    }

    // ─── Construcción de filas ────────────────────────────────────────────

    private static List<ItemizadoRow> BuildRows(
        List<List<string>> table, int dataStart,
        int cItem, int cDesc, int cUnidad, int cCant)
    {
        var rows = new List<ItemizadoRow>();
        string currentParentId = "";
        int childSeq = 1;
        int fallbackTitle = 1;

        for (int i = dataStart; i < table.Count; i++)
        {
            var row = table[i];
            string itemCode = ItemizadoImportParser.GetCell(row, cItem).Trim();
            string desc = ItemizadoImportParser.GetCell(row, cDesc).Trim();
            string unidad = ItemizadoImportParser.GetCell(row, cUnidad).Trim();
            string cantText = ItemizadoImportParser.GetCell(row, cCant).Trim();

            if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(desc))
                continue;

            // Saltar si la fila repite el encabezado (desc == uno de los alias de descripción)
            string descKey = ItemizadoImportParser.HeaderKey(desc);
            if (descKey is "DESCRIPCION" or "DETALLE" or "NOMBRE" or "PARTIDADESCRIPCION")
                continue;

            if (string.IsNullOrWhiteSpace(desc))
                desc = itemCode;

            // No inventar unidad: respetar el valor del Excel exactamente.
            // Si está vacía, se deja vacía para que el usuario la complete.

            // Cantidad vacía = 0 (no 1m); se respeta el valor exacto del Excel
            decimal cant = string.IsNullOrWhiteSpace(cantText)
                ? 0m
                : ItemizadoImportParser.ParseDecimal(cantText);

            bool isTitleCode = ItemizadoImportParser.IsImportedTitleCode(itemCode);
            bool hasQuantityOrUnit = cant > 0m || !string.IsNullOrWhiteSpace(unidad);
            bool hasSubItems = isTitleCode && ItemizadoImportParser.HasSubItemsInTable(table, i, cItem, cDesc, itemCode);

            bool esTitulo = (isTitleCode && (!hasQuantityOrUnit || hasSubItems))
                || (string.IsNullOrWhiteSpace(itemCode) && !hasQuantityOrUnit && IsLikelyTitle(desc));

            if (!esTitulo)
                cant = ItemizadoHierarchyRules.NormalizePartidaQuantity(cant);

            ItemizadoImportRowBuilder.AppendRow(
                rows,
                itemCode,
                desc,
                unidad,
                cant,
                esTitulo,
                ref currentParentId,
                ref childSeq,
                ref fallbackTitle);
        }

        return rows;
    }

    // ─── Clasificadores de celda ─────────────────────────────────────────

    public static bool IsKnownUnit(string v)
    {
        string u = v.Trim().ToUpperInvariant();
        return u is "M" or "M2" or "M3" or "ML" or "ML." or "GL" or "UN" or "UND" or "UNI"
            or "KG" or "TON" or "HR" or "HH" or "DÍA" or "DIA" or "MES" or "PT"
            or "KVA" or "KW" or "VA" or "A" or "PZA" or "PZ" or "JGO" or "SET"
            or "PTO" or "PTOS" or "PUNTO" or "PUNTOS" or "UNID" or "UNIDADES"
            or "ROLLO" or "ROLLOS" or "TIRA" or "TIRAS" or "BARRA" or "BARRAS"
            or "BOB" or "BOBINA" or "PAR" or "PARES" or "GLOBAL" or "LOTE";
    }

    public static bool IsNumericCell(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        string s = v.Trim();

        // Limpiar símbolos de moneda comunes y sufijos/prefijos de divisa
        string clean = Regex.Replace(s, @"[\$€£]|(?i)\b(CLP|UF|USD)\b", "").Trim();
        if (string.IsNullOrWhiteSpace(clean)) return false;

        // Si aún contiene letras (ej. descripciones o unidades), no es celda numérica
        if (clean.Any(char.IsLetter)) return false;

        // Intentar parseo con el parser robusto de ItemizadoImportParser
        decimal val = ItemizadoImportParser.ParseDecimal(clean);
        if (val != 0m) return true;

        // Si es 0 o formato con dígitos ceros
        return Regex.IsMatch(clean, @"^[0,\.\-\s]+$") && clean.Any(char.IsDigit);
    }

    public static bool IsCodeLike(string v)
    {
        string s = v.Trim();
        return Regex.IsMatch(s, @"^([a-zA-Z]{1,3}\.)?[\d,\.]+$") && s.Length <= 12;
    }

    /// <summary>
    /// Heurística auxiliar cuando no hay código de ítem: mayúsculas sostenidas o numeración romana.
    /// </summary>
    public static bool IsLikelyTitle(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc) || desc.Length > 80) return false;

        bool allCaps = desc.All(c => !char.IsLetter(c) || char.IsUpper(c)) && desc.Any(char.IsLetter);
        if (allCaps && desc.Length > 3) return true;

        if (Regex.IsMatch(desc.TrimStart(), @"^(I{1,3}|IV|V|VI{0,3}|IX|X)[\.\s\-]", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    public static string GuessUnit(string desc)
    {
        string d = (desc ?? "").ToUpperInvariant();
        if (d.Contains("M2") || d.Contains("M²")) return "m2";
        if (d.Contains("M3") || d.Contains("M³")) return "m3";
        if (d.Contains("METRO LINEAL") || d.Contains(" ML ")) return "ml";
        if (d.Contains("METRO")) return "m";
        if (d.Contains("KG") || d.Contains("KILOGRAM")) return "kg";
        if (d.Contains("HH") || d.Contains("HORA-HOMBRE")) return "HH";
        if (d.Contains("HORA")) return "hr";
        return "GL";
    }

    private static string BuildColumnMap(int cItem, int cDesc, int cUnidad, int cCant, List<string>? headers)
    {
        string Label(int c) => c >= 0 && headers != null && c < headers.Count
            ? $"col {c + 1} '{headers[c]}'"
            : (c >= 0 ? $"col {c + 1}" : "-");

        return $"ITEM={Label(cItem)}  DESC={Label(cDesc)}  UNIDAD={Label(cUnidad)}  CANT={Label(cCant)}";
    }
}
