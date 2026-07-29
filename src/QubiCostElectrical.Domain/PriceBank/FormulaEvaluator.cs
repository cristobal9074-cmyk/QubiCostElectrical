using System;
using System.Collections.Generic;
using System.Globalization;

namespace QubiCostElectrical;

public static class FormulaEvaluator
{
    public static decimal Evaluate(string formula, decimal maestroQty) =>
        Evaluate(formula, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["MAESTRO"] = maestroQty });

    public static decimal Evaluate(string formula, IReadOnlyDictionary<string, decimal> variables)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return (variables?.TryGetValue("MAESTRO", out var fallback) ?? false) ? fallback : 0m;

        var tokens = Tokenize(formula.Replace(" ", ""), variables ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
        var pos = 0;
        var result = ParseAddSub(tokens, ref pos);
        if (pos < tokens.Count)
            throw new FormatException($"Token inesperado '{tokens[pos].Value}' en posicion {pos}");
        return result;
    }

    public static string Validate(string formula, IReadOnlyDictionary<string, decimal>? sampleVars = null)
    {
        try
        {
            var vars = sampleVars != null
                ? new Dictionary<string, decimal>(sampleVars, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            vars.TryAdd("MAESTRO", 100);
            Evaluate(formula, vars);
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private enum TokenType { Number, Add, Sub, Mul, Div, LParen, RParen, Comma, Func }

    private sealed record Token(TokenType Type, string Value);

    private static List<Token> Tokenize(string expr, IReadOnlyDictionary<string, decimal> variables)
    {
        var list = new List<Token>();
        for (int i = 0; i < expr.Length;)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                int start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                list.Add(new Token(TokenType.Number, expr[start..i]));
                continue;
            }

            if (c == '-') { list.Add(new Token(TokenType.Sub, "-")); i++; continue; }
            if (c == '+') { list.Add(new Token(TokenType.Add, "+")); i++; continue; }
            if (c == '*') { list.Add(new Token(TokenType.Mul, "*")); i++; continue; }
            if (c == '/') { list.Add(new Token(TokenType.Div, "/")); i++; continue; }
            if (c == '(') { list.Add(new Token(TokenType.LParen, "(")); i++; continue; }
            if (c == ')') { list.Add(new Token(TokenType.RParen, ")")); i++; continue; }
            if (c == ',') { list.Add(new Token(TokenType.Comma, ",")); i++; continue; }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                var name = expr[start..i].ToUpperInvariant();

                if (name is "CEIL" or "FLOOR" or "ROUND" or "ABS")
                {
                    list.Add(new Token(TokenType.Func, name));
                }
                else
                {
                    var key = name.Replace("_", "");
                    if (!variables.TryGetValue(key, out var val))
                        throw new FormatException($"Variable desconocida '{name}' en posicion {start}");
                    list.Add(new Token(TokenType.Number, val.ToString("G", CultureInfo.InvariantCulture)));
                }
                continue;
            }

            throw new FormatException($"Caracter inesperado '{c}' en posicion {i}");
        }
        return list;
    }

    private static decimal ParseAddSub(List<Token> tokens, ref int pos)
    {
        var left = ParseMulDiv(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Type is TokenType.Add or TokenType.Sub)
        {
            var op = tokens[pos].Value;
            pos++;
            var right = ParseMulDiv(tokens, ref pos);
            left = op == "+" ? left + right : left - right;
        }
        return left;
    }

    private static decimal ParseMulDiv(List<Token> tokens, ref int pos)
    {
        var left = ParseAtom(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Type is TokenType.Mul or TokenType.Div)
        {
            var op = tokens[pos].Value;
            pos++;
            var right = ParseAtom(tokens, ref pos);
            left = op == "*"
                ? left * right
                : right != 0m ? left / right : throw new DivideByZeroException("Division por cero");
        }
        return left;
    }

    private static decimal ParseAtom(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new FormatException("Expresion incompleta");

        Token t = tokens[pos];

        if (t.Type == TokenType.Number)
        {
            pos++;
            return decimal.Parse(t.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        if (t.Type == TokenType.Sub)
        {
            pos++;
            return -ParseAtom(tokens, ref pos);
        }

        if (t.Type == TokenType.LParen)
        {
            pos++;
            var inner = ParseAddSub(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RParen)
                throw new FormatException("Falta parentesis de cierre");
            pos++;
            return inner;
        }

        if (t.Type == TokenType.Func)
        {
            var funcName = t.Value;
            pos++;
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.LParen)
                throw new FormatException($"Esperaba '(' despues de '{funcName}'");
            pos++;
            var arg = ParseAddSub(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RParen)
                throw new FormatException($"Falta ')' en funcion '{funcName}'");
            pos++;

            return funcName switch
            {
                "CEIL" => Math.Ceiling(arg),
                "FLOOR" => Math.Floor(arg),
                "ROUND" => Math.Round(arg),
                "ABS" => Math.Abs(arg),
                _ => arg
            };
        }

        throw new FormatException($"Token inesperado '{t.Value}'");
    }
}
