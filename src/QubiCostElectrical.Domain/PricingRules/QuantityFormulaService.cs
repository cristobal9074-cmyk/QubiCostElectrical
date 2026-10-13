using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed record QuantityFormulaResult(decimal Value, bool Success, string Error);

/// <summary>
/// Evaluador liviano para fórmulas de cantidad.
/// Soporta: +, -, *, /, ^, paréntesis, decimales con punto/coma y variables nombradas.
/// No ejecuta código externo: solo analiza la expresión matemática permitida.
/// </summary>
public static class QuantityFormulaService
{
    public static QuantityFormulaResult Evaluate(string? expression, IReadOnlyDictionary<string, decimal>? variables = null)
    {
        string text = NormalizeExpression(expression);
        if (string.IsNullOrWhiteSpace(text))
            return new QuantityFormulaResult(0m, true, "");

        try
        {
            var parser = new Parser(text, variables ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
            decimal value = parser.ParseExpression();
            parser.SkipWhiteSpace();
            if (!parser.IsAtEnd)
                return new QuantityFormulaResult(0m, false, $"Texto no reconocido cerca de '{parser.PeekRemaining()}'.");


            return new QuantityFormulaResult(value, true, "");
        }
        catch (DivideByZeroException)
        {
            return new QuantityFormulaResult(0m, false, "División por cero.");
        }
        catch (FormulaException ex)
        {
            return new QuantityFormulaResult(0m, false, ex.Message);
        }
        catch (Exception ex)
        {
            return new QuantityFormulaResult(0m, false, ex.Message);
        }
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

        public Parser(string text, IReadOnlyDictionary<string, decimal> variables)
        {
            this.text = text;
            this.variables = variables;
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
            EnterRecursion();
            try
            {
                decimal left = ParseAdditive();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match("==") || Match("="))
                    {
                        decimal right = ParseAdditive();
                        left = left == right ? 1m : 0m;
                    }
                    else if (Match("<>") || Match("!="))
                    {
                        decimal right = ParseAdditive();
                        left = left != right ? 1m : 0m;
                    }
                    else if (Match("<="))
                    {
                        decimal right = ParseAdditive();
                        left = left <= right ? 1m : 0m;
                    }
                    else if (Match(">="))
                    {
                        decimal right = ParseAdditive();
                        left = left >= right ? 1m : 0m;
                    }
                    else if (Match('<'))
                    {
                        decimal right = ParseAdditive();
                        left = left < right ? 1m : 0m;
                    }
                    else if (Match('>'))
                    {
                        decimal right = ParseAdditive();
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
            decimal value = ParseTerm();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else break;
            }
            return value;
        }

        private decimal ParseTerm()
        {
            decimal value = ParsePower();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('*')) value *= ParsePower();
                else if (Match('/'))
                {
                    decimal divisor = ParsePower();
                    if (divisor == 0m) throw new DivideByZeroException();
                    value /= divisor;
                }
                else break;
            }
            return value;
        }

        private decimal ParsePower()
        {
            decimal value = ParseUnary();
            SkipWhiteSpace();
            if (Match('^'))
            {
                EnterRecursion();
                try
                {
                    decimal exponent = ParsePower();
                    double pow = Math.Pow((double)value, (double)exponent);
                    if (double.IsNaN(pow) || double.IsInfinity(pow))
                        throw new FormulaException("Resultado inválido en potencia.");
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
            SkipWhiteSpace();
            if (Match('+') || Match('-'))
            {
                bool negate = text[pos - 1] == '-';
                EnterRecursion();
                try
                {
                    decimal value = ParseUnary();
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
            SkipWhiteSpace();
            decimal result;
            if (Match('('))
            {
                result = ParseExpression();
                SkipWhiteSpace();
                if (!Match(')')) throw new FormulaException("Falta cerrar paréntesis.");
            }
            else if (!IsAtEnd && (char.IsLetter(text[pos]) || text[pos] == '_'))
            {
                result = ParseIdentifierOrFunction();
            }
            else
            {
                result = ParseNumber();
            }

            SkipWhiteSpace();
            while (Match('%'))
            {
                result *= 0.01m;
                SkipWhiteSpace();
            }
            return result;
        }

        private void EnterRecursion()
        {
            depth++;
            if (depth > MaxRecursionDepth)
                throw new FormulaException("Fórmula demasiado anidada o compleja.");
        }

        private decimal ParseIdentifierOrFunction()
        {
            int start = pos;
            pos++;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_' || text[pos] == '.'))
                pos++;

            string name = text.Substring(start, pos - start);
            SkipWhiteSpace();
            if (Match('('))
            {
                EnterRecursion();
                try
                {
                    var args = new List<decimal>();
                    SkipWhiteSpace();
                    if (!Match(')'))
                    {
                        while (true)
                        {
                            args.Add(ParseExpression());
                            SkipWhiteSpace();
                            if (Match(',') || Match(';'))
                            {
                                SkipWhiteSpace();
                                continue;
                            }
                            if (Match(')')) break;
                            throw new FormulaException($"Se esperaba ',' o ')' en la función {name}.");
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

            // Fallback manual por si el diccionario que llega no usa comparador ignore-case.
            foreach (var kv in variables)
            {
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            throw new FormulaException($"Variable no definida: {name}.");
        }

        private static decimal ExecuteFunction(string name, List<decimal> args)
        {
            string upper = name.ToUpperInvariant();
            switch (upper)
            {
                case "SUM":
                case "SUMA":
                    return args.Count > 0 ? args.Sum() : 0m;
                case "AVERAGE":
                case "PROMEDIO":
                    if (args.Count == 0) throw new FormulaException("PROMEDIO requiere al menos un argumento.");
                    return args.Average();
                case "MAX":
                case "MAXIMO":
                    if (args.Count == 0) throw new FormulaException("MAX requiere al menos un argumento.");
                    return args.Max();
                case "MIN":
                case "MINIMO":
                    if (args.Count == 0) throw new FormulaException("MIN requiere al menos un argumento.");
                    return args.Min();
                case "ROUND":
                case "REDONDEAR":
                {
                    if (args.Count == 0) throw new FormulaException("REDONDEAR requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    return Math.Round(args[0], Math.Clamp(d, 0, 10), MidpointRounding.AwayFromZero);
                }
                case "ROUNDUP":
                case "REDONDEAR.MAS":
                case "REDONDEARMAS":
                {
                    if (args.Count == 0) throw new FormulaException("REDONDEAR.MAS requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    decimal factor = (decimal)Math.Pow(10, Math.Clamp(d, 0, 10));
                    return Math.Ceiling(args[0] * factor) / factor;
                }
                case "ROUNDDOWN":
                case "REDONDEAR.MENOS":
                case "REDONDEARMENOS":
                {
                    if (args.Count == 0) throw new FormulaException("REDONDEAR.MENOS requiere al menos un argumento.");
                    int d = args.Count > 1 ? (int)args[1] : 0;
                    decimal factor = (decimal)Math.Pow(10, Math.Clamp(d, 0, 10));
                    return Math.Floor(args[0] * factor) / factor;
                }
                case "INT":
                case "ENTERO":
                    if (args.Count == 0) throw new FormulaException("ENTERO requiere un argumento.");
                    return Math.Floor(args[0]);
                case "ABS":
                    if (args.Count == 0) throw new FormulaException("ABS requiere un argumento.");
                    return Math.Abs(args[0]);
                case "SQRT":
                case "RAIZ":
                    if (args.Count == 0) throw new FormulaException("RAIZ requiere un argumento.");
                    if (args[0] < 0m) throw new FormulaException("RAIZ no admite números negativos.");
                    return (decimal)Math.Sqrt((double)args[0]);
                case "POWER":
                case "POTENCIA":
                    if (args.Count < 2) throw new FormulaException("POTENCIA requiere base y exponente.");
                    return (decimal)Math.Pow((double)args[0], (double)args[1]);
                case "IF":
                case "SI":
                    if (args.Count < 2) throw new FormulaException("SI requiere condición y valor.");
                    return args[0] != 0m ? args[1] : (args.Count > 2 ? args[2] : 0m);
                default:
                    throw new FormulaException($"Función no reconocida: {name}.");
            }
        }

        private decimal ParseNumber()
        {
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
                throw new FormulaException($"Se esperaba número, variable o paréntesis cerca de '{PeekRemaining()}'.");

            string raw = text.Substring(start, pos - start).Replace(',', '.');
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                throw new FormulaException($"Número inválido: {raw}.");

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

    private sealed class FormulaException : Exception
    {
        public FormulaException(string message) : base(message) { }
    }
}
