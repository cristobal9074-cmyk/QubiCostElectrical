using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

/// <summary>
/// Resolución de filas con rol Operación: fórmula libre (QuantityFormulaService: +, -, *, /, ^,
/// paréntesis y variables nombradas) que combina cantidades de otras filas referenciándolas por
/// su CÓDIGO, ej. "PAZ * 2 + TEEG500".
/// Se usa al expandir plantillas a cubicación (ConjuntoElectricoService) y para el recálculo
/// en vivo en la hoja de cubicación. Las referencias se resuelven en orden topológico y las
/// filas en ciclo se excluyen (conservan su valor) con advertencia, para que un ciclo nunca
/// cuelga ni revienta el stack.
/// </summary>
public static class OperacionFilaService
{
    // Identificadores al estilo QuantityFormulaService: empiezan con letra o '_'.
    private static readonly Regex IdentificadorRegex = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    /// <summary>
    /// Normaliza un código de fila a nombre de variable de fórmula: minúsculas, acentos a
    /// ascii, caracteres no alfanuméricos a '_' y sin '_' iniciales/finales. Un código que
    /// empiece por dígito se Prefija con "z_" (las variables deben empezar con letra o '_').
    /// </summary>
    public static string NormalizeVariableKey(string? codigo)
    {
        string raw = (codigo ?? "").Trim().ToLowerInvariant();
        if (raw.Length == 0) return "";

        Span<char> buf = raw.Length <= 256 ? stackalloc char[raw.Length + 2] : new char[raw.Length + 2];
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

        int start = 0, end = len;
        while (start < end && buf[start] == '_') start++;
        while (end > start && buf[end - 1] == '_') end--;
        if (start >= end) return "";

        string key = new string(buf.Slice(start, end - start));
        if (!(char.IsLetter(key[0]) || key[0] == '_')) key = "z_" + key;
        return key;
    }

    /// <summary>
    /// Diccionario código→cantidad para resolver fórmulas. Clave = código normalizado (y el
    /// código crudo si además es nombre de variable válido); comparación ignore-case. En
    /// códigos duplicados gana la primera fila.
    /// </summary>
    public static Dictionary<string, decimal> BuildVariables(IEnumerable<(string? Codigo, decimal Cantidad)> filas)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (codigo, cantidad) in filas ?? Enumerable.Empty<(string?, decimal)>())
        {
            string key = NormalizeVariableKey(codigo);
            if (key.Length > 0 && !variables.ContainsKey(key))
                variables[key] = cantidad;

            string crudo = (codigo ?? "").Trim();
            if (crudo.Length > 0 && QuantityFormulaService.IsValidVariableName(crudo) && !variables.ContainsKey(crudo))
                variables[crudo] = cantidad;
        }
        return variables;
    }

    /// <summary>Variables (identificadores) referenciadas textualmente en una fórmula.</summary>
    public static HashSet<string> ExtraerVariables(string? operacion)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in IdentificadorRegex.Matches(operacion ?? ""))
            variables.Add(m.Value);
        return variables;
    }

    /// <summary>Esta fila tiene fórmulas de zona (QtyExpressions/TotalExpression) que mandan sobre la operación.</summary>
    public static bool TieneZonas(CubicacionRow row)
    {
        if (row == null) return false;
        if (row.QtyExpressions != null)
        {
            for (int i = 0; i < row.QtyExpressions.Length; i++)
                if (!string.IsNullOrWhiteSpace(row.QtyExpressions[i]))
                    return true;
        }
        return !string.IsNullOrWhiteSpace(row.TotalExpression);
    }

    /// <summary>
    /// Evalúa las filas con Operación de <paramref name="rows"/> (las filas de una misma partida)
    /// en orden topológico de sus dependencias y setea <see cref="CubicacionRow.Cantidad"/> en las
    /// que cambien. Filas con zonas (QtyExpressions) se excluyen: ahí manda la fórmula de zona.
    /// Filas en ciclo entre sí o con error de fórmula no se modifican y quedan en
    /// <paramref name="advertencias"/> (si se provee). Retorna las filas cuya cantidad cambió.
    /// </summary>
    public static List<CubicacionRow> Resolver(IReadOnlyList<CubicacionRow> rows, List<string>? advertencias = null)
    {
        var cambiadas = new List<CubicacionRow>();
        if (rows == null || rows.Count == 0) return cambiadas;

        var operaciones = rows
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Operacion) && !TieneZonas(r))
            .ToList();
        if (operaciones.Count == 0) return cambiadas;

        // Variables iniciales: código → cantidad vigente de todas las filas (las operaciones
        // se reevalúan en orden y actualizan su propia entrada para las que dependen de ellas).
        var variables = BuildVariables(rows.Select(r => ((string?)r.Codigo, r.Cantidad)));

        // Dependencias entre operaciones: solo importan las referencias que apuntan a OTRA
        // fila operación (las filas normales ya tienen su valor).
        var opPorKey = new Dictionary<string, CubicacionRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in operaciones)
        {
            string key = NormalizeVariableKey(row.Codigo);
            if (key.Length > 0 && !opPorKey.ContainsKey(key)) opPorKey[key] = row;
        }

        var dependencias = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var gradoEntrada = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in operaciones)
        {
            string key = RowKey(row);
            gradoEntrada[key] = 0;
            dependencias[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        foreach (var row in operaciones)
        {
            string key = RowKey(row);
            foreach (string referencia in ExtraerVariables(row.Operacion))
            {
                if (!opPorKey.TryGetValue(referencia, out var otra)) continue;
                string otraKey = RowKey(otra);
                if (string.Equals(otraKey, key, StringComparison.OrdinalIgnoreCase)) continue; // auto-referencia
                if (dependencias[key].Add(otraKey))
                    gradoEntrada[key]++;
            }
        }

        // Orden topológico (Kahn). Lo que quede fuera del orden está en ciclo: se excluye.
        var cola = new Queue<string>(gradoEntrada.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var orden = new List<string>();
        while (cola.Count > 0)
        {
            string key = cola.Dequeue();
            orden.Add(key);
            foreach (string dependiente in dependencias.Where(kv => kv.Value.Contains(key)).Select(kv => kv.Key))
            {
                gradoEntrada[dependiente]--;
                if (gradoEntrada[dependiente] == 0) cola.Enqueue(dependiente);
            }
        }

        var enCiclo = new HashSet<string>(
            gradoEntrada.Where(kv => kv.Value > 0).Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

        var porKey = operaciones.GroupBy(RowKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (enCiclo.Count > 0 && advertencias != null)
        {
            foreach (string key in enCiclo)
                advertencias.Add(
                    $"Fila '{Label(porKey[key])}' Participa en un ciclo de operaciones; no se recalculó.");
        }

        foreach (string key in orden)
        {
            var row = porKey[key];
            var result = QuantityFormulaService.Evaluate(row.Operacion, variables);
            if (!result.Success)
            {
                advertencias?.Add($"Operación '{row.Operacion}' en fila '{Label(row)}': {result.Error}");
                continue;
            }
            if (row.Cantidad == result.Value) continue;
            row.Cantidad = result.Value;
            cambiadas.Add(row);

            // Publicar el nuevo valor para las operaciones dependientes que sigan en el orden.
            string varKey = NormalizeVariableKey(row.Codigo);
            if (varKey.Length > 0) variables[varKey] = result.Value;
        }

        return cambiadas;

        static string RowKey(CubicacionRow row)
        {
            string key = NormalizeVariableKey(row.Codigo);
            return key.Length > 0 ? key : row.Id ?? "";
        }

        static string Label(CubicacionRow row)
        {
            string desc = (row.Descripcion ?? "").Trim();
            if (desc.Length > 0) return desc;
            string codigo = (row.Codigo ?? "").Trim();
            return codigo.Length > 0 ? codigo : "sin código";
        }
    }
}
