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
        if (raw.StartsWith("=", StringComparison.Ordinal)) return true;

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
            finally
            {
                depth--;
            }
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
            if (Match('('))
            {
                decimal value = ParseExpression();
                SkipWhiteSpace();
                if (!Match(')')) throw new FormulaException("Falta cerrar paréntesis.");
                return value;
            }

            if (!IsAtEnd && (char.IsLetter(text[pos]) || text[pos] == '_'))
                return ParseVariable();

            return ParseNumber();
        }

        private void EnterRecursion()
        {
            depth++;
            if (depth > MaxRecursionDepth)
                throw new FormulaException("Fórmula demasiado anidada o compleja.");
        }

        private decimal ParseVariable()
        {
            int start = pos;
            pos++;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                pos++;

            string name = text.Substring(start, pos - start);
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
                else if ((ch == '.' || ch == ',') && !hasSeparator)
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
    }

    private sealed class FormulaException : Exception
    {
        public FormulaException(string message) : base(message) { }
    }
}
