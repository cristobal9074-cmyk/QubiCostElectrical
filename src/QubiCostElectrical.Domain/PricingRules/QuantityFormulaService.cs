using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed record QuantityFormulaResult(decimal Value, bool Success, string Error);

/// <summary>
/// Evaluador del dialecto cerrado de cantidades. Ver <see cref="QuantityFormulaDialect"/>.
/// Un token fuera de esa lista vuelve como error. No se traga en un catch genérico ni se publica como cero.
/// </summary>
public static class QuantityFormulaService
{
    public static QuantityFormulaResult Evaluate(string? expression, IReadOnlyDictionary<string, decimal>? variables = null)
    {
        string text = NormalizeExpression(expression);
        if (string.IsNullOrWhiteSpace(text))
            return new QuantityFormulaResult(0m, true, "");

        var parser = new Parser(text, variables ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
        decimal value;
        try
        {
            value = parser.ParseExpression();
        }
        catch (OverflowException)
        {
            return new QuantityFormulaResult(0m, false, "Resultado demasiado grande.");
        }

        if (parser.Error != null)
            return new QuantityFormulaResult(0m, false, parser.Error);
        parser.SkipWhiteSpace();
        if (!parser.IsAtEnd)
            return new QuantityFormulaResult(0m, false, $"Texto no reconocido cerca de '{parser.PeekRemaining()}'.");
        return new QuantityFormulaResult(value, true, "");
    }

    /// <summary>
    /// Comprueba el dialecto sin las cantidades reales del proyecto.
    /// R1, U1 y las casillas se aceptan; una función que no está en la lista, no.
    /// </summary>
    public static QuantityFormulaResult ValidateDialect(string? expression)
        => Evaluate(expression, QuantityFormulaDialect.StructuralReferences);

    /// <summary>Texto de la barra de fórmulas: las expresiones llevan "=" adelante, como Excel.</summary>
    public static string FormatForFormulaBar(string? raw)
    {
        string text = (raw ?? "").Trim();
        if (text.Length == 0) return "";
        if (text.StartsWith("=", StringComparison.Ordinal)) return text;
        return IsFormulaLike(text) ? "=" + text : text;
    }

    /// <summary>Quita el "=" de la barra antes de guardar la casilla.</summary>
    public static string ParseFromFormulaBar(string? barText)
    {
        string text = (barText ?? "").Trim();
        if (text.StartsWith("=", StringComparison.Ordinal))
            text = text[1..].TrimStart();
        return text;
    }

    public static bool IsFormulaLike(string? expression)
    {
        string raw = (expression ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (raw.StartsWith("=", StringComparison.Ordinal) || raw.StartsWith("+", StringComparison.Ordinal)) return true;

        string text = NormalizeExpression(raw);
        return text.Any(ch => ch == '+' || ch == '*' || ch == '/' || ch == '^' || ch == '(' || ch == ')')
               || text.Any(ch => char.IsLetter(ch) || ch == '_');
    }

    public static bool IsValidVariableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string text = name.Trim();
        if (!(char.IsLetter(text[0]) || text[0] == '_')) return false;
        for (int i = 1; i < text.Length; i++)
        {
            char ch = text[i];
            if (!(char.IsLetterOrDigit(ch) || ch == '_')) return false;
        }
        return true;
    }

    public static string NormalizeVariableName(string? name)
    {
        return (name ?? "").Trim();
    }

    private static string NormalizeExpression(string? expression)
    {
        string text = (expression ?? "").Trim();
        if (text.StartsWith("=", StringComparison.Ordinal))
            text = text[1..].Trim();
        if (text.StartsWith("+", StringComparison.Ordinal))
            text = text[1..].Trim();
        if (text.Contains('$'))
            text = text.Replace("$", "");
        return ExpandRanges(text);
    }

    private static string ExpandRanges(string text)
    {
        if (!text.Contains(':') && !text.Contains(".."))
            return text;

        // 1D: C1:C4 o R1:R5 o F2:F6 o C1..C4
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b(?<p>[A-Za-z]+)(?<s1>\d+)\s*(?::|\.\.)\s*\k<p>(?<s2>\d+)\b",
            m =>
            {
                string prefix = m.Groups["p"].Value;
                int start = int.Parse(m.Groups["s1"].Value, CultureInfo.InvariantCulture);
                int end = int.Parse(m.Groups["s2"].Value, CultureInfo.InvariantCulture);
                int min = Math.Min(start, end);
                int max = Math.Max(start, end);
                if (max - min > 300) return m.Value;
                var items = Enumerable.Range(min, max - min + 1).Select(i => $"{prefix}{i}");
                return string.Join(", ", items);
            });

        // 2D: F1C1:F1C4 (rango dentro de la misma fila para casillas)
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\b(?<fp>[Ff])(?<row>\d+)(?<cp>[Cc])(?<c1>\d+)\s*(?::|\.\.)\s*\k<fp>\k<row>\k<cp>(?<c2>\d+)\b",
            m =>
            {
                string fp = m.Groups["fp"].Value;
                string row = m.Groups["row"].Value;
                string cp = m.Groups["cp"].Value;
                int start = int.Parse(m.Groups["c1"].Value, CultureInfo.InvariantCulture);
                int end = int.Parse(m.Groups["c2"].Value, CultureInfo.InvariantCulture);
                int min = Math.Min(start, end);
                int max = Math.Max(start, end);
                if (max - min > 300) return m.Value;
                var items = Enumerable.Range(min, max - min + 1).Select(c => $"{fp}{row}{cp}{c}");
                return string.Join(", ", items);
            });

        return text;
    }

    private sealed class Parser
    {
        // Límite defensivo: el parser es recursivo-descendente (paréntesis, signos unarios y
        // potencias encadenadas recursan). Sin este límite, una fórmula pegada con miles de
        // paréntesis/signos anidados agota el stack con StackOverflowException, que en .NET
        // NO se puede capturar con try/catch y mata el proceso completo sin aviso.
        private const int MaxRecursionDepth = 200;

        private readonly string text;
        private readonly IReadOnlyDictionary<string, decimal> variables;
        private int pos;
        private int depth;
        private string? error;

        public Parser(string text, IReadOnlyDictionary<string, decimal> variables)
        {
            this.text = text;
            this.variables = variables;
        }

        public string? Error => error;

        private decimal Fail(string message)
        {
            error ??= message;
            return 0m;
        }

        public bool IsAtEnd => pos >= text.Length;

        public string PeekRemaining()
        {
            if (pos >= text.Length) return "fin";
            int len = Math.Min(12, text.Length - pos);
            return text.Substring(pos, len);
        }

        public decimal ParseExpression()
        {
            if (error != null) return 0m;
            if (!EnterRecursion()) return 0m;
            try
            {
                decimal left = ParseAdditive();
                if (error != null) return 0m;
                while (error == null)
                {
                    SkipWhiteSpace();
                    if (Match("==") || Match("="))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left == right ? 1m : 0m;
                    }
                    else if (Match("<>") || Match("!="))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left != right ? 1m : 0m;
                    }
                    else if (Match("<="))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left <= right ? 1m : 0m;
                    }
                    else if (Match(">="))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left >= right ? 1m : 0m;
                    }
                    else if (Match('<'))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left < right ? 1m : 0m;
                    }
                    else if (Match('>'))
                    {
                        decimal right = ParseAdditive();
                        if (error != null) return 0m;
                        left = left > right ? 1m : 0m;
                    }
                    else
                    {
                        break;
                    }
                }
                return left;
            }
            finally
            {
                depth--;
            }
        }

        private decimal ParseAdditive()
        {
            if (error != null) return 0m;
            decimal value = ParseTerm();
            while (error == null)
            {
                SkipWhiteSpace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else break;
            }
            return error == null ? value : 0m;
        }

        private decimal ParseTerm()
        {
            if (error != null) return 0m;
            decimal value = ParsePower();
            while (error == null)
            {
                SkipWhiteSpace();
                if (Match('*')) value *= ParsePower();
                else if (Match('/'))
                {
                    decimal divisor = ParsePower();
                    if (error != null) return 0m;
                    if (divisor == 0m) return Fail("División por cero.");
                    value /= divisor;
                }
                else break;
            }
            return error == null ? value : 0m;
        }

        private decimal ParsePower()
        {
            if (error != null) return 0m;
            decimal value = ParseUnary();
            if (error != null) return 0m;
            SkipWhiteSpace();
            if (Match('^'))
            {
                if (!EnterRecursion()) return 0m;
                try
                {
                    decimal exponent = ParsePower();
                    if (error != null) return 0m;
                    double pow = Math.Pow((double)value, (double)exponent);
                    if (double.IsNaN(pow) || double.IsInfinity(pow))
                        return Fail("Resultado inválido en potencia.");
                    value = Convert.ToDecimal(pow);
                }
                finally
                {
                    depth--;
                }
            }
            return value;
        }

        private decimal ParseUnary()
        {
            if (error != null) return 0m;
            SkipWhiteSpace();
            if (Match('+') || Match('-'))
            {
                bool negate = text[pos - 1] == '-';
                if (!EnterRecursion()) return 0m;
                try
                {
                    decimal value = ParseUnary();
                    if (error != null) return 0m;
                    return negate ? -value : value;
                }
                finally
                {
                    depth--;
                }
            }
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            if (error != null) return 0m;
            SkipWhiteSpace();
            decimal result;
            if (Match('('))
            {
                result = ParseExpression();
                if (error != null) return 0m;
                SkipWhiteSpace();
                if (!Match(')')) return Fail("Falta cerrar paréntesis.");
            }
            else if (!IsAtEnd && (char.IsLetter(text[pos]) || text[pos] == '_'))
            {
                result = ParseIdentifierOrFunction();
            }
            else
            {
                result = ParseNumber();
            }

            if (error != null) return 0m;
            SkipWhiteSpace();
            while (Match('%'))
            {
                result *= 0.01m;
                SkipWhiteSpace();
            }
            return result;
        }

        private bool EnterRecursion()
        {
            depth++;
            if (depth > MaxRecursionDepth)
            {
                Fail("Fórmula demasiado anidada o compleja.");
                return false;
            }
            return true;
        }

        private decimal ParseIdentifierOrFunction()
        {
            if (error != null) return 0m;
            int start = pos;
            pos++;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_' || text[pos] == '.'))
                pos++;

            string name = text.Substring(start, pos - start);
            SkipWhiteSpace();
            if (Match('('))
            {
                if (!EnterRecursion()) return 0m;
                try
                {
                    var args = new List<decimal>();
                    SkipWhiteSpace();
                    if (!Match(')'))
                    {
                        while (error == null)
                        {
                            args.Add(ParseExpression());
                            if (error != null) return 0m;
                            SkipWhiteSpace();
                            if (Match(',') || Match(';'))
                            {
                                SkipWhiteSpace();
                                continue;
                            }
                            if (Match(')')) break;
                            return Fail($"Se esperaba ',' o ')' en la función {name}.");
                        }
                    }
                    return ExecuteFunction(name, args);
                }
                finally
                {
                    depth--;
                }
            }

            if (variables.TryGetValue(name, out decimal value))
                return value;

            foreach (var kv in variables)
            {
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            if (QuantityFormulaDialect.IsFunction(name))
                return Fail($"Función {name} sin paréntesis. Escriba {name}(...).");

            return Fail($"Variable no definida: {name}.");
        }

        private decimal ExecuteFunction(string name, List<decimal> args)
        {
            if (error != null) return 0m;
            if (!QuantityFormulaDialect.IsFunction(name))
                return Fail($"Función no reconocida: {name}. No está en el dialecto.");

            string upper = name.ToUpperInvariant();
            switch (upper)
            {
                case "SUM":
                case "SUMA":
                    return args.Count > 0 ? args.Sum() : 0m;
                case "AVERAGE":
                case "PROMEDIO":
                    if (args.Count == 0) return Fail("PROMEDIO requiere al menos un argumento.");
                    return args.Average();
                case "MAX":
                case "MAXIMO":
                    if (args.Count == 0) return Fail("MAX requiere al menos un argumento.");
                    return args.Max();
                case "MIN":
                case "MINIMO":
                    if (args.Count == 0) return Fail("MIN requiere al menos un argumento.");
                    return args.Min();
                case "ROUND":
                case "REDONDEAR":
                {
                    if (args.Count == 0) return Fail("REDONDEAR requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    return Math.Round(args[0], Math.Clamp(d, 0, 10), MidpointRounding.AwayFromZero);
                }
                case "ROUNDUP":
                case "REDONDEAR.MAS":
                case "REDONDEARMAS":
                {
                    if (args.Count == 0) return Fail("REDONDEAR.MAS requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    decimal factor = (decimal)Math.Pow(10, Math.Clamp(d, 0, 10));
                    return Math.Ceiling(args[0] * factor) / factor;
                }
                case "ROUNDDOWN":
                case "REDONDEAR.MENOS":
                case "REDONDEARMENOS":
                {
                    if (args.Count == 0) return Fail("REDONDEAR.MENOS requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    decimal factor = (decimal)Math.Pow(10, Math.Clamp(d, 0, 10));
                    return Math.Floor(args[0] * factor) / factor;
                }
                case "INT":
                case "ENTERO":
                    if (args.Count == 0) return Fail("ENTERO requiere un argumento.");
                    return Math.Floor(args[0]);
                case "ABS":
                    if (args.Count == 0) return Fail("ABS requiere un argumento.");
                    return Math.Abs(args[0]);
                case "SQRT":
                case "RAIZ":
                    if (args.Count == 0) return Fail("RAIZ requiere un argumento.");
                    if (args[0] < 0m) return Fail("RAIZ no admite números negativos.");
                    return (decimal)Math.Sqrt((double)args[0]);
                case "POWER":
                case "POTENCIA":
                    if (args.Count < 2) return Fail("POTENCIA requiere base y exponente.");
                    return (decimal)Math.Pow((double)args[0], (double)args[1]);
                case "IF":
                case "SI":
                    if (args.Count < 2) return Fail("SI requiere condición y valor.");
                    return args[0] != 0m ? args[1] : (args.Count > 2 ? args[2] : 0m);
                default:
                    return Fail($"Función no reconocida: {name}. No está en el dialecto.");
            }
        }

        private decimal ParseNumber()
        {
            if (error != null) return 0m;
            SkipWhiteSpace();
            int start = pos;
            bool hasDigit = false;
            bool hasSeparator = false;

            while (!IsAtEnd)
            {
                char ch = text[pos];
                if (char.IsDigit(ch))
                {
                    hasDigit = true;
                    pos++;
                }
                else if (ch == '.' && !hasSeparator)
                {
                    hasSeparator = true;
                    pos++;
                }
                else if (ch == ',' && !hasSeparator && pos + 1 < text.Length && char.IsDigit(text[pos + 1]))
                {
                    hasSeparator = true;
                    pos++;
                }
                else
                {
                    break;
                }
            }

            if (!hasDigit)
                return Fail($"Se esperaba número, variable o paréntesis cerca de '{PeekRemaining()}'.");

            string raw = text.Substring(start, pos - start).Replace(',', '.');
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                return Fail($"Número inválido: {raw}.");

            return value;
        }

        public void SkipWhiteSpace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(text[pos])) pos++;
        }

        private bool Match(char expected)
        {
            if (IsAtEnd || text[pos] != expected) return false;
            pos++;
            return true;
        }

        private bool Match(string expected)
        {
            if (pos + expected.Length > text.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (text[pos + i] != expected[i]) return false;
            }
            pos += expected.Length;
            return true;
        }
    }
}
