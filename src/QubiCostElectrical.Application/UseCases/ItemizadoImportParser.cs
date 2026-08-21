using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

/// <summary>
/// Helpers puros de parseo de tablas de itemizado.
/// Sin dependencias de UI, sin acceso a archivos — sólo texto → estructura.
/// Usado por PegarItemizadoUseCase (portapapeles) e ImportarItemizadoUseCase (Excel).
/// </summary>
public static class ItemizadoImportParser
{
    // ─── Detección de encabezados ─────────────────────────────────────────

    /// <summary>
    /// Busca en las primeras 40 filas la fila que contiene encabezados reconocibles.
    /// kind = "itemizado" requiere DESCRIPCION + ITEM.
    /// kind = cualquier otro: DESCRIPCION + (CODIGO o PRECIO).
    /// Devuelve el índice de la fila encabezado principal, o -1 si no se encuentra.
    /// Para obtener la fila de encabezado secundaria (cuando UNIDAD/CANT están en la siguiente
    /// fila), usar FindHeaderRowRange.
    /// </summary>
    public static int FindHeaderRow(List<List<string>> rows, string kind)
        => FindHeaderRowRange(rows, kind).MainRow;

    /// <summary>
    /// Detecta filas de encabezado, incluyendo el patrón con 2 filas contiguas donde
    /// ITEM/DESC están en una fila y UNIDAD/CANTIDAD en la siguiente.
    /// Devuelve (MainRow=-1, ExtraRow=-1) si no se encuentran encabezados.
    /// ExtraRow >= 0 indica que hay una segunda fila de encabezado.
    /// </summary>
    public static (int MainRow, int ExtraRow) FindHeaderRowRange(List<List<string>> rows, string kind)
    {
        int maxRows = Math.Min(rows.Count, 40);

        for (int r = 0; r < maxRows; r++)
        {
            var row = rows[r];
            if (kind == "itemizado")
            {
                bool hasDesc = FindColumn(row, "DESCRIPCION", "DESCRIPCIÓN", "DETALLE", "NOMBRE", "DESCRIPTION", "NOMBREY") >= 0;
                bool hasItem = FindColumn(row,
                    "ITEM", "ÍTEM", "PARTIDA", "NITEM", "NUMEROITEM",
                    // aliases extra para encabezados reales de Chile
                    "N", "N.", "N°", "Nº", "ORD", "ORDEN", "NUM", "NUMERO", "NUMITEM",
                    "NRO", "NRO.", "#", "COD", "CODIGO", "CODIGOÍTEM",
                    "ITEMPARTIDA", "NPARTIDA", "NUMEROPARTIDA") >= 0;

                if (hasDesc)
                {
                    // Verificar si UNIDAD/CANTIDAD están en la misma fila o en la siguiente
                    bool hasUnidadSameRow = FindColumn(row, "UNIDAD", "UD", "UN", "UND", "UNID", "UNIDADES") >= 0;
                    bool hasCantSameRow   = FindColumn(row, "CANTIDAD", "CANT", "QTY", "CANTIDADITEM", "CANTIDADES", "NUEVASCANTIDADES") >= 0;
                    if (!hasUnidadSameRow || !hasCantSameRow)
                    {
                        // Intentar fila siguiente como encabezado complementario
                        int nextR = r + 1;
                        if (nextR < rows.Count)
                        {
                            var nextRow = rows[nextR];
                            bool nextHasUnidad = FindColumn(nextRow, "UNIDAD", "UD", "UN", "UND", "UNID", "UNIDADES") >= 0;
                            bool nextHasCant   = FindColumn(nextRow, "CANTIDAD", "CANT", "QTY", "CANTIDADITEM", "CANTIDADES", "NUEVASCANTIDADES") >= 0;
                            if (nextHasUnidad || nextHasCant)
                                return (r, nextR);  // 2-row header
                        }
                    }

                    // ITEM es opcional: si la fila tiene DESCRIPCION, es suficiente para importar
                    // (cItem quedará -1 y BuildRows usará string.Empty como itemCode)
                    if (hasItem || hasDesc)
                        return (r, -1);  // Single-row header
                }
            }
            else
            {
                bool hasDesc = FindColumn(row, "DESCRIPCION", "DESCRIPCIÓN", "DETALLE", "NOMBRE", "MATERIAL") >= 0;
                bool hasCode = FindColumn(row, "CODIGO", "CÓDIGO", "COD", "CODE") >= 0;
                if (hasDesc && (hasCode || FindColumn(row, "PRECIO", "MATUF", "VALOR") >= 0))
                    return (r, -1);
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// Busca la primera columna cuyo HeaderKey coincide exactamente o mediante alias compuestos conocidos.
    /// No usa Contains para evitar falsos positivos (ej. CANTIDADITEM → ITEM).
    /// </summary>
    /// <summary>
    /// Busca la primera columna cuyo HeaderKey coincide con alguno de los names dados.
    /// También busca en un segundo conjunto de encabezados (extraHeaders) para soporte
    /// de tablas con dos filas de encabezado.
    /// </summary>
    public static int FindColumn(List<string> headers, List<string>? extraHeaders, params string[] names)
    {
        int col = FindColumn(headers, names);
        if (col >= 0) return col;
        if (extraHeaders != null && extraHeaders.Count > 0)
            return FindColumn(extraHeaders, names);
        return -1;
    }

    public static int FindColumn(List<string> headers, params string[] names)
    {
        if (headers == null || headers.Count == 0)
            return -1;

        var wanted = names
            .Select(HeaderKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        for (int i = 0; i < headers.Count; i++)
        {
            string h = HeaderKey(headers[i]);
            if (string.IsNullOrWhiteSpace(h))
                continue;
            if (wanted.Contains(h))
                return i;
        }

        for (int i = 0; i < headers.Count; i++)
        {
            string h = HeaderKey(headers[i]);
            if (string.IsNullOrWhiteSpace(h))
                continue;
            if (CompoundHeaderAliases.TryGetValue(h, out var canonical) && wanted.Contains(canonical))
                return i;
        }

        // Pase 3: símbolos especiales que no tienen letras (e.g. "#" → ITEM)
        if (wanted.Contains("ITEM"))
        {
            for (int i = 0; i < headers.Count; i++)
            {
                string raw = (headers[i] ?? "").Trim();
                if (raw == "#" || raw == "N°" || raw == "Nº" || raw == "N.°")
                    return i;
            }
        }

        return -1;
    }

    private static readonly Dictionary<string, string> CompoundHeaderAliases = new(StringComparer.Ordinal)
    {
        ["NITEM"] = "ITEM",
        ["NUMEROITEM"] = "ITEM",
        ["NUMITEM"] = "ITEM",
        ["NRO"] = "ITEM",
        ["NUM"] = "ITEM",
        ["NUMERO"] = "ITEM",
        ["ORD"] = "ITEM",
        ["ORDEN"] = "ITEM",
        ["NPARTIDA"] = "ITEM",
        ["ITEMPARTIDA"] = "ITEM",
        ["PARTIDADESCRIPCION"] = "DESCRIPCION",
        ["CANTIDADITEM"] = "CANTIDAD",
        ["MATUF"] = "PRECIO",
        ["VALORUF"] = "PRECIO",
        // Aliases adicionales para encabezados reales de proyectos
        ["UNID"] = "UNIDAD",
        ["UNIDADES"] = "UNIDAD",
        ["CANTIDADES"] = "CANTIDAD",
        ["NUEVASCANTIDADES"] = "CANTIDAD",
    };


    /// <summary>
    /// Normaliza un encabezado para comparación fuzzy:
    /// quita tildes, pasa a mayúsculas, deja sólo alfanuméricos.
    /// "Descripción" → "DESCRIPCION", "N° Ítem" → "NITEM".
    /// </summary>
    public static string HeaderKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    public static int FindDataStartRow(List<List<string>> table, int headerRow, int cItem, int cDesc)
    {
        for (int i = headerRow + 1; i < table.Count; i++)
        {
            var row = table[i];
            string item = GetCell(row, cItem).Trim();
            string desc = GetCell(row, cDesc).Trim();
            if (string.IsNullOrWhiteSpace(item) && string.IsNullOrWhiteSpace(desc))
                continue;

            string descKey = HeaderKey(desc);
            if (descKey is "DESCRIPCION" or "DETALLE" or "NOMBRE" or "PARTIDADESCRIPCION")
                continue;

            return i;
        }

        return headerRow + 1;
    }

    public static (int UnidadCol, int CantCol) InferOptionalColumns(
        List<List<string>> table,
        int dataStartRow,
        int cItem,
        int cDesc,
        int cUnidad,
        int cCant)
    {
        if (cUnidad < 0)
        {
            int candidate = cDesc + 1;
            if (candidate != cItem && ColumnHasData(table, dataStartRow, candidate))
                cUnidad = candidate;
        }

        if (cCant < 0)
        {
            int candidate = cUnidad >= 0 ? cUnidad + 1 : cDesc + 2;
            if (candidate != cItem && candidate != cDesc && ColumnHasData(table, dataStartRow, candidate))
                cCant = candidate;
        }

        return (cUnidad, cCant);
    }

    private static bool ColumnHasData(List<List<string>> table, int dataStartRow, int column)
    {
        if (column < 0)
            return false;

        int limit = Math.Min(table.Count, dataStartRow + 20);
        for (int i = dataStartRow; i < limit; i++)
        {
            if (!string.IsNullOrWhiteSpace(GetCell(table[i], column)))
                return true;
        }

        return false;
    }

    // ─── Acceso seguro a celdas ───────────────────────────────────────────

    public static string GetCell(List<string> row, int column)
    {
        if (row == null || column < 0 || column >= row.Count) return "";
        return row[column] ?? "";
    }

    // ─── Detección de títulos ─────────────────────────────────────────────

    /// <summary>
    /// Un ítem es un título de sección si su código es un número entero o
    /// termina en ".0" (ej. "1", "2", "1.0", "3.0").
    /// </summary>
    public static bool IsImportedTitleCode(string itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode)) return false;
        string raw = itemCode.Trim().Replace(",", ".");
        // Número entero o terminado en .0 → título de sección (ej. "1", "2", "1.0")
        if (Regex.IsMatch(raw, @"^\d+(\.0)?$")) return true;
        // Asterisco (*) → marcador de subsección/título especial
        if (raw == "*") return true;
        return false;
    }

    /// <summary>
    /// Construye el ID canónico de un título (ej. "1.0", "2.0").
    /// Si el código no reconocible, usa fallback.
    /// </summary>
    public static string BuildImportedTitleId(string itemCode, int fallback)
    {
        string raw = (itemCode ?? "").Trim().Replace(",", ".");

        if (string.IsNullOrWhiteSpace(raw))
            return $"{fallback}.0";

        if (Regex.IsMatch(raw, @"^\d+$"))
            return raw + ".0";

        if (Regex.IsMatch(raw, @"^\d+\.0$"))
            return raw;

        return $"{fallback}.0";
    }

    // ─── Parser de valores numéricos ─────────────────────────────────────

    /// <summary>
    /// Parsea un número en formato chileno/español o inglés.
    /// Ejemplos válidos: "0.035", "0,035", "1.250,50", "1,250.50", "$ 12.500".
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        string text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        if (text.StartsWith("=", StringComparison.Ordinal)
            || text.Contains('+')
            || text.Contains('*')
            || text.Contains('/'))
            return 0m;

        text = text.Replace(" ", " ");
        bool negative = text.Contains('(') && text.Contains(')');
        text = Regex.Replace(text, @"[^0-9,\.\-]", "");
        if (string.IsNullOrWhiteSpace(text) || text == "-" || text == "." || text == ",") return 0m;

        if (text.Count(ch => ch == '-') > 0)
        {
            negative = true;
            text = text.Replace("-", "");
        }

        int lastComma = text.LastIndexOf(',');
        int lastDot   = text.LastIndexOf('.');
        string normalized;

        if (lastComma >= 0 && lastDot >= 0)
        {
            normalized = lastComma > lastDot
                ? text.Replace(".", "").Replace(",", ".")
                : text.Replace(",", "");
        }
        else if (lastComma >= 0)
        {
            normalized = NormalizeSingleSeparator(text, ',');
        }
        else if (lastDot >= 0)
        {
            normalized = NormalizeSingleSeparator(text, '.');
        }
        else
        {
            normalized = text;
        }

        if (negative && !normalized.StartsWith("-", StringComparison.Ordinal))
            normalized = "-" + normalized;

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            return result;

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
            return result;

        return 0m;
    }

    private static string NormalizeSingleSeparator(string text, char separator)
    {
        int count = text.Count(ch => ch == separator);
        string otherRemoved = separator == ',' ? text.Replace(".", "") : text.Replace(",", "");

        if (count > 1)
            return otherRemoved.Replace(separator.ToString(), "");

        int pos = otherRemoved.LastIndexOf(separator);
        if (pos < 0) return otherRemoved;

        int decimals = otherRemoved.Length - pos - 1;
        int integers = pos;

        // X.000 / X,000 (exactamente 3 dígitos decimales) → separador de miles en formato chileno/español
        // Ejemplo: 2.520 = 2520, 1.250 = 1250, 1.530 = 1530
        // EXCEPCIÓN: 0.xxx es decimal (empieza con "0.")
        bool looksLikeThousands = decimals == 3 && !(otherRemoved.StartsWith("0" + separator));
        if (looksLikeThousands)
            return otherRemoved.Replace(separator.ToString(), "");

        return separator == ',' ? otherRemoved.Replace(",", ".") : otherRemoved;
    }
}

