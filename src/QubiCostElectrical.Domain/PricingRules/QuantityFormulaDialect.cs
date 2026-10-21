using System;
using System.Collections;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Dialecto cerrado de las cantidades. Lo que no está aquí no se evalúa y no se convierte en cero.
/// </summary>
public static class QuantityFormulaDialect
{
    public const string Summary =
        "Números, + − * / ^, paréntesis, %, comparaciones, R1..Rn, U1..Un, casillas (C1, F1C1) y funciones SUMA, PROMEDIO, MAX, MIN, REDONDEAR, REDONDEAR.MAS, REDONDEAR.MENOS, ENTERO, ABS, RAIZ, POTENCIA, SI.";

    public static readonly string[] Functions =
    [
        "SUMA", "SUM",
        "PROMEDIO", "AVERAGE",
        "MAX", "MAXIMO",
        "MIN", "MINIMO",
        "REDONDEAR", "ROUND",
        "REDONDEAR.MAS", "REDONDEARMAS", "ROUNDUP",
        "REDONDEAR.MENOS", "REDONDEARMENOS", "ROUNDDOWN",
        "ENTERO", "INT",
        "ABS",
        "RAIZ", "SQRT",
        "POTENCIA", "POWER",
        "SI", "IF",
    ];

    private static readonly HashSet<string> FunctionSet = new(Functions, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "m2", "metros_cuadrados", "valor_uf", "uf_clp", "dolar_clp",
        "gastos_generales_pct", "utilidad_pct", "herramientas_pct", "mermas_pct",
        "riesgo_pct", "descuento_pct", "iva_pct", "cantidad_item", "cantidad_partida",
        "zonas_total", "q_total",
    };

    public static bool IsFunction(string? name)
        => !string.IsNullOrWhiteSpace(name) && FunctionSet.Contains(name.Trim());

    /// <summary>R1, U1, C1, F1, F1C1, R1C1, C1Z1, q1, zona1, zona_1.</summary>
    public static bool IsStructuralReference(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string text = name.Trim();
        if (text.Length < 2) return false;

        if (IsCellRef(text, 'F', 'C') || IsCellRef(text, 'R', 'C') || IsCellRef(text, 'C', 'Z'))
            return true;

        if (StartsWithRowPrefix(text, 'R') || StartsWithRowPrefix(text, 'U')
            || StartsWithRowPrefix(text, 'C') || StartsWithRowPrefix(text, 'F'))
            return true;

        if ((text[0] == 'q' || text[0] == 'Q') && AllDigits(text, 1))
            return true;

        if (text.StartsWith("zona", StringComparison.OrdinalIgnoreCase))
        {
            int i = 4;
            if (i < text.Length && text[i] == '_') i++;
            return AllDigits(text, i);
        }

        return false;
    }

    public static bool IsProjectVariable(string? name)
        => !string.IsNullOrWhiteSpace(name) && ProjectVariables.Contains(name.Trim());

    /// <summary>Diccionario de diagnóstico: las referencias del dialecto valen 0, el resto no existe.</summary>
    public static IReadOnlyDictionary<string, decimal> StructuralReferences { get; } = new StructuralReferenceMap();

    private static bool StartsWithRowPrefix(string text, char prefix)
    {
        if (char.ToUpperInvariant(text[0]) != prefix) return false;
        return AllDigits(text, 1);
    }

    private static bool IsCellRef(string text, char rowPrefix, char colPrefix)
    {
        if (char.ToUpperInvariant(text[0]) != rowPrefix) return false;
        int i = 1;
        if (!TakeDigits(text, ref i)) return false;
        if (i >= text.Length || char.ToUpperInvariant(text[i]) != colPrefix) return false;
        i++;
        return TakeDigits(text, ref i) && i == text.Length;
    }

    private static bool AllDigits(string text, int start)
    {
        int i = start;
        return TakeDigits(text, ref i) && i == text.Length;
    }

    private static bool TakeDigits(string text, ref int i)
    {
        int start = i;
        while (i < text.Length && char.IsDigit(text[i])) i++;
        return i > start;
    }

    private sealed class StructuralReferenceMap : IReadOnlyDictionary<string, decimal>
    {
        public decimal this[string key] => TryGetValue(key, out decimal value) ? value : throw new KeyNotFoundException(key);
        public IEnumerable<string> Keys => Array.Empty<string>();
        public IEnumerable<decimal> Values => Array.Empty<decimal>();
        public int Count => 0;
        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, out decimal value)
        {
            if (IsStructuralReference(key) || IsProjectVariable(key))
            {
                value = 0m;
                return true;
            }

            value = 0m;
            return false;
        }

        public IEnumerator<KeyValuePair<string, decimal>> GetEnumerator()
            => Empty().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<KeyValuePair<string, decimal>> Empty()
        {
            yield break;
        }
    }
}
