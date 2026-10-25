using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class CubicacionTakeoffQueryService : ICubicacionTakeoffQueryService
{
    public Dictionary<string, decimal> BuildFormulaVariables(BudgetProjectSession session, string itemId)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["m2"] = session.MetrosCuadrados,
            ["metros_cuadrados"] = session.MetrosCuadrados,
            ["valor_uf"] = session.ValorUfClp,
            ["uf_clp"] = session.ValorUfClp,
            ["dolar_clp"] = session.TipoCambioUsdClp,
            ["gastos_generales_pct"] = session.GastosGeneralesPct,
            ["utilidad_pct"] = session.UtilidadPct,
            ["herramientas_pct"] = session.HerramientasPct,
            ["mermas_pct"] = session.MermasPct,
            ["riesgo_pct"] = session.RiesgoPct,
            ["descuento_pct"] = session.DescuentoPct,
            ["iva_pct"] = session.IvaPct
        };

        var item = session.Itemizado.FirstOrDefault(x =>
            string.Equals(x.Id, itemId ?? "", StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            variables["cantidad_item"] = item.Cantidad;
            variables["cantidad_partida"] = item.Cantidad;
        }

        foreach (var kv in session.ProjectVariables)
        {
            if (QuantityFormulaService.IsValidVariableName(kv.Key))
                variables[QuantityFormulaService.NormalizeVariableName(kv.Key)] = kv.Value;
        }

        return variables;
    }

    public decimal EvaluateStoredExpressions(BudgetProjectSession session, CubicacionRow row, int qtyColumnCount = 400)
        => EvaluateStoredExpressionsWithCallback(session, row, qtyColumnCount, null);

    public decimal EvaluateStoredExpressions(BudgetProjectSession session, CubicacionRow row, int qtyColumnCount, Action<int, decimal> onZoneComputed)
        => EvaluateStoredExpressionsWithCallback(session, row, qtyColumnCount, onZoneComputed);

    private decimal EvaluateStoredExpressionsWithCallback(BudgetProjectSession session, CubicacionRow row, int qtyColumnCount, Action<int, decimal>? onZoneComputed)
    {
        var headers = GetHeaders(session, row.ItemId ?? string.Empty, qtyColumnCount);
        return EvaluateStoredExpressionsCore(session, row, qtyColumnCount, null, headers, null, onZoneComputed);
    }

    public void RecalculateAll(BudgetProjectSession session, int qtyColumnCount = 400)
    {
        var baseVariablesCache = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
        var headersCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        // Diccionario de trabajo reutilizable: se llena desde baseVariables + zone vars en cada fila,
        // evitando new Dictionary(..., baseVariables) (copia O(n)) por cada fila con fórmula.
        var workVars = new Dictionary<string, decimal>(64, StringComparer.OrdinalIgnoreCase);

        foreach (var row in session.Cubicacion)
        {
            if (!HasExpressions(row))
                continue;

            string itemKey = row.ItemId ?? string.Empty;
            if (!baseVariablesCache.TryGetValue(itemKey, out var baseVariables))
            {
                baseVariables = BuildFormulaVariables(session, itemKey);
                baseVariablesCache[itemKey] = baseVariables;
            }

            if (!headersCache.TryGetValue(itemKey, out var headers))
            {
                headers = GetHeaders(session, itemKey, qtyColumnCount);
                headersCache[itemKey] = headers;
            }

            row.Cantidad = EvaluateStoredExpressionsCore(session, row, qtyColumnCount, baseVariables, headers, workVars);
        }
    }

    public void RecalculateForItem(BudgetProjectSession session, string itemId, int qtyColumnCount = 400)
    {
        var itemRows = CubicacionItemIndex.FilterRows(session.Cubicacion, itemId);
        RecalculateItemRows(session, itemId, itemRows, qtyColumnCount);
    }

    public void RecalculateForItem(
        BudgetProjectSession session,
        string itemId,
        IReadOnlyDictionary<string, List<CubicacionRow>> index,
        int qtyColumnCount = 400)
    {
        var itemRows = CubicacionItemIndex.GetRows(index, itemId);
        RecalculateItemRows(session, itemId, itemRows, qtyColumnCount);
    }

    private void RecalculateItemRows(
        BudgetProjectSession session,
        string itemId,
        IReadOnlyList<CubicacionRow> itemRows,
        int qtyColumnCount)
    {
        Dictionary<string, decimal>? baseVariables = null;
        string[]? headers = null;
        var workVars = new Dictionary<string, decimal>(64, StringComparer.OrdinalIgnoreCase);

        foreach (var row in itemRows)
        {
            if (!HasExpressions(row))
                continue;

            baseVariables ??= BuildFormulaVariables(session, itemId);
            headers ??= GetHeaders(session, itemId, qtyColumnCount);

            row.Cantidad = EvaluateStoredExpressionsCore(
                session, row, qtyColumnCount, baseVariables, headers, workVars, siblingRows: itemRows);
        }
    }

    public decimal ApplyRounding(CubicacionRow row, decimal value)
    {
        if (row == null || row.RedondeoMultiplo <= 0m) return value;
        decimal multiple = row.RedondeoMultiplo;
        string mode = (row.RedondeoModo ?? "").Trim().ToUpperInvariant();
        decimal ratio = value / multiple;
        return mode switch
        {
            "ARRIBA" or "CEIL" or "SUPERIOR" => Math.Ceiling(ratio) * multiple,
            "ABAJO" or "FLOOR" or "INFERIOR" => Math.Floor(ratio) * multiple,
            "CERCANO" or "ROUND" or "REDONDEAR" => Math.Round(ratio, 0, MidpointRounding.AwayFromZero) * multiple,
            _ => value
        };
    }

    public string[] NormalizeExpressions(string[]? expressions, int count)
    {
        var result = new string[count];
        if (expressions == null) return result;
        for (int i = 0; i < Math.Min(count, expressions.Length); i++)
            result[i] = expressions[i] ?? "";
        return result;
    }

    // Publica el valor de una zona/sector bajo todos sus alias (qN, zonaN, zona_N y el nombre
    // del encabezado normalizado, ej. "TA-P2" → "ta_p2"), para que otras fórmulas de la misma
    // fila puedan referenciarlo por cualquiera de esos nombres.
    private static void PublishZoneVariable(Dictionary<string, decimal> variables, string[]? headers, int index, decimal value)
    {
        string qVar = index < QVarNames.Length ? QVarNames[index] : $"q{index + 1}";
        string zVar = index < ZonaVarNames.Length ? ZonaVarNames[index] : $"zona{index + 1}";
        string zuVar = index < ZonaUnderlineVarNames.Length ? ZonaUnderlineVarNames[index] : $"zona_{index + 1}";
        variables[qVar] = value;
        variables[zVar] = value;
        variables[zuVar] = value;
        string header = headers != null && index < headers.Length ? headers[index] : string.Empty;
        string key = NormalizeVariableName(header);
        if (QuantityFormulaService.IsValidVariableName(key))
            variables[key] = value;
    }

    private void AddZoneVariables(BudgetProjectSession session, string itemId, Dictionary<string, decimal> variables, decimal[] zoneValues, int qtyColumnCount)
    {
        string[] headers = GetHeaders(session, itemId, qtyColumnCount);
        for (int i = 0; i < zoneValues.Length; i++)
            PublishZoneVariable(variables, headers, i, zoneValues[i]);
        variables["zonas_total"] = zoneValues.Sum();
        variables["q_total"] = zoneValues.Sum();
    }

    private static bool HasExpressions(CubicacionRow row)
    {
        if (row == null) return false;

        if (row.QtyExpressions != null)
        {
            for (int i = 0; i < row.QtyExpressions.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(row.QtyExpressions[i]))
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(row.TotalExpression);
    }

    // Sobrecarga interna que acepta un dict reutilizable para evitar allocaciones O(n) por fila.
    private decimal EvaluateStoredExpressionsCore(
        BudgetProjectSession session, CubicacionRow row, int qtyColumnCount,
        Dictionary<string, decimal>? baseVariables, string[]? headers,
        Dictionary<string, decimal>? workVars = null,
        Action<int, decimal>? onZoneComputed = null,
        IReadOnlyList<CubicacionRow>? siblingRows = null)
    {
        if (row == null) return 0m;

        // Buscar si hay alguna expresión real sin crear el array completo de 300 slots.
        string[]? srcExpr = row.QtyExpressions;
        bool hasStoredExpressions = false;
        int exprLen = srcExpr?.Length ?? 0;
        for (int i = 0; i < exprLen; i++)
        {
            if (!string.IsNullOrWhiteSpace(srcExpr![i]))
            { hasStoredExpressions = true; break; }
        }

        if (!hasStoredExpressions && string.IsNullOrWhiteSpace(row.TotalExpression))
            return ApplyRounding(row, 0m);

        // Reutilizar workVars si se proporcionó; si no, crear uno nuevo (llamada puntual).
        Dictionary<string, decimal> variables;
        if (workVars != null)
        {
            workVars.Clear();
            if (baseVariables != null)
                foreach (var kv in baseVariables) workVars[kv.Key] = kv.Value;
            else
                foreach (var kv in BuildFormulaVariables(session, row.ItemId)) workVars[kv.Key] = kv.Value;
            variables = workVars;
        }
        else
        {
            variables = baseVariables != null
                ? new Dictionary<string, decimal>(baseVariables, StringComparer.OrdinalIgnoreCase)
                : BuildFormulaVariables(session, row.ItemId);
        }

        // Usar array pooled de zona en stack si cabe; si no, en heap.
        decimal[]? zoneHeap = null;
        Span<decimal> zoneValues = qtyColumnCount <= 512
            ? stackalloc decimal[qtyColumnCount]
            : (zoneHeap = new decimal[qtyColumnCount]);
        zoneValues.Clear();
        decimal total = 0m;

        // Evaluar y publicar cada sector en el mismo recorrido (en vez de evaluar todos primero
        // y publicar después): así una fórmula puede referenciar un sector ANTERIOR de la misma
        // fila por su nombre (ej. sector "TA-P3" con "=ta_p1+ta_p2"), igual que una fórmula de
        // Excel que referencia una celda a su izquierda. Un sector no puede referenciar uno
        // posterior en la misma fila (aún no fue calculado); en ese caso lee 0.
        if (hasStoredExpressions)
        {
            int limit = Math.Min(exprLen, qtyColumnCount);
            for (int i = 0; i < limit; i++)
            {
                string? expression = srcExpr![i];
                if (string.IsNullOrWhiteSpace(expression))
                    continue;

                var result = EvaluateSafeWithRowReferences(
                    expression, variables, session, row, i, depth: 0, visiting: null, siblingRows);
                if (result.Success)
                {
                    zoneValues[i] = result.Value;
                    total += result.Value;
                    PublishZoneVariable(variables, headers, i, result.Value);
                    onZoneComputed?.Invoke(i, result.Value);
                }
                else
                {
                    onZoneComputed?.Invoke(i, 0m);
                }
            }
        }

        // Publicar el resto de zonas vacías/fuera de rango (para que TotalExpression y las
        // fórmulas posteriores puedan referenciarlas sin error, aunque valgan 0).
        if (headers != null)
        {
            for (int i = 0; i < qtyColumnCount; i++)
            {
                if (i < exprLen && !string.IsNullOrWhiteSpace(srcExpr![i]))
                    continue; // ya publicada arriba
                PublishZoneVariable(variables, headers, i, 0m);
            }
        }
        variables["zonas_total"] = total;
        variables["q_total"] = total;

        string totalFormula = (row.TotalExpression ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(totalFormula))
        {
            var totalResult = EvaluateSafe(totalFormula, variables);
            if (totalResult.Success)
                total = totalResult.Value;
        }

        _ = zoneHeap; // suprimir warning si stackalloc path
        return ApplyRounding(row, total);
    }

    private static string[] GetHeaders(BudgetProjectSession session, string itemId, int count)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && session.QtyHeadersByItem.TryGetValue(itemId, out var stored))
        {
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = stored != null && i < stored.Length && stored[i] != null
                    ? stored[i]
                    : (i < DefaultHeaders.Length ? DefaultHeaders[i] : $"Zona {i + 1}");
            }
            return result;
        }

        if (count == 400)
            return DefaultHeaders;

        var defaults = new string[count];
        for (int i = 0; i < count; i++)
            defaults[i] = i < DefaultHeaders.Length ? DefaultHeaders[i] : $"Zona {i + 1}";
        return defaults;
    }

    private static string NormalizeVariableName(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        string raw = label.Trim().ToLowerInvariant();
        if (raw.Length == 0) return string.Empty;

        // Usar Span<char> en stack para strings cortos (evita List<char>+ToArray()).
        Span<char> buf = raw.Length <= 256 ? stackalloc char[raw.Length + 3] : new char[raw.Length + 3];
        int len = 0;
        bool lastUnderscore = false;
        foreach (char original in raw)
        {
            char ch = original switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                _ => original
            };
            if (char.IsLetterOrDigit(ch) || ch == '_') { buf[len++] = ch; lastUnderscore = false; }
            else if (!lastUnderscore) { buf[len++] = '_'; lastUnderscore = true; }
        }

        // Trim underscores desde los extremos sin crear string intermedio.
        int start = 0, end = len;
        while (start < end && buf[start] == '_') start++;
        while (end > start && buf[end - 1] == '_') end--;
        if (start >= end) return string.Empty;

        string key = new string(buf.Slice(start, end - start));
        if (!(char.IsLetter(key[0]) || key[0] == '_')) key = "z_" + key;
        return key;
    }

    private static QuantityFormulaResult EvaluateSafe(string expression, IReadOnlyDictionary<string, decimal> variables)
    {
        try
        {
            var result = QuantityFormulaService.Evaluate(expression, variables);
            if (!result.Success) return result;
            if (result.Value == decimal.MinValue || result.Value == decimal.MaxValue)
                return new QuantityFormulaResult(0m, false, "Resultado fuera de rango.");
            return result;
        }
        catch (DivideByZeroException)
        {
            return new QuantityFormulaResult(0m, false, "División por cero.");
        }
        catch (OverflowException)
        {
            return new QuantityFormulaResult(0m, false, "Resultado demasiado grande.");
        }
        catch (Exception ex)
        {
            return new QuantityFormulaResult(0m, false, ex.Message);
        }
    }

    private const int MaxRowReferenceDepth = 8;
    private static readonly System.Text.RegularExpressions.Regex CellReferenceRegex =
        new(@"[Ff](?<row>\d+)[Cc](?<col>\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Referencia viva a CUALQUIER celda de la partida (fila + sector) — el usuario arma tokens
    // "F{fila}C{columna}" con las flechas (ver ApplySectorPick en la vista) apuntando a
    // cualquier casilla visible, y esa referencia se recalcula sola porque en cada evaluación se
    // vuelve a leer y evaluar la expresión CRUDA de esa otra celda, no un valor congelado. La
    // fila usa el mismo orden "#" que ve el usuario en pantalla (session.Cubicacion filtrado por
    // partida); la columna es el sector absoluto (1-300).
    //
    // Seguridad: solo se resuelven los tokens que aparecen textualmente en la expresión (evita
    // evaluar celdas que nadie referencia — el caso común), y la recursión tiene límite de
    // profundidad + detección de ciclos (A→B→A) vía "visiting": una celda que ya se está
    // evaluando en la misma cadena de referencias lee 0 en vez de volver a entrar, así una
    // referencia circular nunca cuelga ni revienta el stack.
    private QuantityFormulaResult EvaluateSafeWithRowReferences(
        string expression,
        Dictionary<string, decimal> variables,
        BudgetProjectSession session,
        CubicacionRow row,
        int offset,
        int depth,
        HashSet<(string RowId, int Offset)>? visiting,
        IReadOnlyList<CubicacionRow>? siblingRows = null)
    {
        var matches = CellReferenceRegex.Matches(expression);
        if (matches.Count == 0)
            return EvaluateSafe(expression, variables);

        if (depth >= MaxRowReferenceDepth)
            return new QuantityFormulaResult(0m, false, "Referencia entre casillas demasiado profunda o circular.");

        siblingRows ??= CubicacionItemIndex.FilterRows(session.Cubicacion, row.ItemId);

        visiting ??= new HashSet<(string, int)>();
        bool addedSelf = visiting.Add((row.Id, offset));

        var scoped = new Dictionary<string, decimal>(variables, StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            int targetRowNumber = int.Parse(m.Groups["row"].Value, CultureInfo.InvariantCulture);
            int targetCol = int.Parse(m.Groups["col"].Value, CultureInfo.InvariantCulture);
            string token = m.Value;

            if (targetRowNumber < 1 || targetRowNumber > siblingRows.Count || targetCol < 1)
            {
                scoped[token] = 0m;
                continue;
            }

            var targetRow = siblingRows[targetRowNumber - 1];
            int targetOffset = targetCol - 1;
            var key = (targetRow.Id, targetOffset);

            decimal value = 0m;
            if (!visiting.Contains(key))
            {
                string targetExpr = targetRow.QtyExpressions != null && targetOffset < targetRow.QtyExpressions.Length
                    ? targetRow.QtyExpressions[targetOffset] ?? ""
                    : "";
                if (!string.IsNullOrWhiteSpace(targetExpr))
                {
                    visiting.Add(key);
                    var nested = EvaluateSafeWithRowReferences(
                        targetExpr, variables, session, targetRow, targetOffset, depth + 1, visiting, siblingRows);
                    visiting.Remove(key);
                    value = nested.Success ? nested.Value : 0m;
                }
            }

            scoped[token] = value;
        }

        if (addedSelf)
            visiting.Remove((row.Id, offset));

        return EvaluateSafe(expression, scoped);
    }

    public QuantityFormulaResult EvaluateCandidateExpression(BudgetProjectSession session, CubicacionRow row, int offset, string? expression)
    {
        string text = (expression ?? "").Trim();
        if (string.IsNullOrEmpty(text) || !QuantityFormulaService.IsFormulaLike(text))
            return new QuantityFormulaResult(0m, true, "");

        const int qtyColumnCount = 400;
        var variables = BuildFormulaVariables(session, row.ItemId);
        var headers = GetHeaders(session, row.ItemId ?? string.Empty, qtyColumnCount);
        string[]? stored = row.QtyExpressions;
        if (stored != null && offset > 0)
        {
            int limit = Math.Min(stored.Length, offset);
            for (int i = 0; i < limit; i++)
            {
                if (string.IsNullOrWhiteSpace(stored[i]))
                    continue;
                var prior = EvaluateSafeWithRowReferences(stored[i], variables, session, row, i, depth: 0, visiting: null);
                if (prior.Success)
                    PublishZoneVariable(variables, headers, i, prior.Value);
            }
        }

        return EvaluateSafeWithRowReferences(text, variables, session, row, offset, depth: 0, visiting: null);
    }
}
