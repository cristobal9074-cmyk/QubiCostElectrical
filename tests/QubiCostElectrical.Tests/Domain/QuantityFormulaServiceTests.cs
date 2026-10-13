using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class QuantityFormulaServiceTests
{
    [Theory]
    [InlineData("2+3", 5)]
    [InlineData("10-3", 7)]
    [InlineData("4*5", 20)]
    [InlineData("20/4", 5)]
    public void Evaluate_BasicArithmetic(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("2+3*4", 14)]
    [InlineData("10-6/2", 7)]
    [InlineData("3*4+2", 14)]
    public void Evaluate_OperatorPrecedence(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("(2+3)*4", 20)]
    [InlineData("10/(2+3)", 2)]
    [InlineData("((2+3)*2)+1", 11)]
    public void Evaluate_Parentheses(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("2^3", 8)]
    [InlineData("3^2", 9)]
    [InlineData("2^0", 1)]
    public void Evaluate_PowerOperator(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("-5", -5)]
    [InlineData("--5", 5)]
    [InlineData("-2+3", 1)]
    [InlineData("-(2+3)", -5)]
    public void Evaluate_UnaryMinus(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("3,5", 3.5)]
    [InlineData("2,5+1,5", 4)]
    [InlineData("1,5*3", 4.5)]
    public void Evaluate_CommaAsDecimalSeparator(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("3.5", 3.5)]
    [InlineData("2.5+1.5", 4)]
    [InlineData("1.5*3", 4.5)]
    public void Evaluate_DotAsDecimalSeparator(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("=2+3", 5)]
    [InlineData(" = 2+3", 5)]
    [InlineData("= 4*5", 20)]
    public void Evaluate_EqualPrefixIsStripped(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Evaluate_WithVariables()
    {
        var vars = new Dictionary<string, decimal> { { "ancho", 3m }, { "largo", 4.5m } };
        var result = QuantityFormulaService.Evaluate("ancho * largo", vars);
        Assert.True(result.Success);
        Assert.Equal(13.5m, result.Value);
    }

    [Fact]
    public void Evaluate_WithCaseInsensitiveVariables()
    {
        var vars = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase) { { "ANCHO", 2m } };
        var result = QuantityFormulaService.Evaluate("ancho * 3", vars);
        Assert.True(result.Success);
        Assert.Equal(6m, result.Value);
    }

    [Fact]
    public void Evaluate_WithUndefinedVariable_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("ancho * largo", new Dictionary<string, decimal>());
        Assert.False(result.Success);
        Assert.Contains("Variable no definida", result.Error);
    }

    [Fact]
    public void Evaluate_MissingClosingParen_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("(2+3");
        Assert.False(result.Success);
        Assert.Contains("cerrar", result.Error);
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("5/0");
        Assert.False(result.Success);
        Assert.Contains("cero", result.Error);
    }

    [Fact]
    public void Evaluate_InvalidPower_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("(-2)^0.5");
        Assert.False(result.Success);
    }

    [Fact]
    public void Evaluate_EmptyOrNull_ReturnsZeroSuccess()
    {
        var result = QuantityFormulaService.Evaluate("");
        Assert.True(result.Success);
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public void Evaluate_Null_ReturnsZeroSuccess()
    {
        var result = QuantityFormulaService.Evaluate(null);
        Assert.True(result.Success);
        Assert.Equal(0m, result.Value);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("42", false)]
    [InlineData("=", true)]
    [InlineData("=2+3", true)]
    [InlineData("2+3", true)]
    [InlineData("a*3", true)]
    [InlineData("3*a", true)]
    [InlineData("(2)", true)]
    [InlineData("2/3", true)]
    [InlineData("2^3", true)]
    public void IsFormulaLike_VariousPatterns(string? input, bool expected)
    {
        Assert.Equal(expected, QuantityFormulaService.IsFormulaLike(input));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("2ancho", false)]
    [InlineData("ancho", true)]
    [InlineData("_tmp", true)]
    [InlineData("largo1", true)]
    [InlineData("a_b", true)]
    public void IsValidVariableName_VariousNames(string? name, bool expected)
    {
        Assert.Equal(expected, QuantityFormulaService.IsValidVariableName(name));
    }

    [Theory]
    [InlineData(" ancho ", "ancho")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeVariableName_Trims(string? input, string expected)
    {
        Assert.Equal(expected, QuantityFormulaService.NormalizeVariableName(input));
    }

    [Fact]
    public void Evaluate_ComplexExpressionWithVariables()
    {
        var vars = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "unidades", 10m },
            { "largo", 2.5m },
            { "ancho", 0.3m }
        };
        var result = QuantityFormulaService.Evaluate("unidades * largo * ancho", vars);
        Assert.True(result.Success);
        Assert.Equal(7.5m, result.Value);
    }

    [Theory]
    [InlineData(" 2 + 3 ", 5)]
    [InlineData("4* 5", 20)]
    [InlineData("10 / 2", 5)]
    public void Evaluate_IgnoresWhitespace(string expr, decimal expected)
    {
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Evaluate_PowerRightAssociative()
    {
        var result = QuantityFormulaService.Evaluate("2^3^2");
        Assert.True(result.Success);
        Assert.Equal(512m, result.Value);
    }

    [Fact]
    public void Evaluate_GarbageText_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("hola mundo");
        Assert.False(result.Success);
    }

    [Fact]
    public void Evaluate_NumbersThenGarbage_ReturnsError()
    {
        var result = QuantityFormulaService.Evaluate("2+3 abc");
        Assert.False(result.Success);
        Assert.Contains("reconocido", result.Error);
    }

    // Regresión: el parser es recursivo-descendente (paréntesis, signos unarios y potencias
    // encadenadas recursan). Sin un límite de profundidad, una fórmula pegada desde Excel con
    // miles de paréntesis/signos anidados agotaba el stack con StackOverflowException — una
    // excepción que en .NET NO se puede capturar con try/catch y mata el proceso completo sin
    // aviso ("se cierra inesperadamente"). Estos tests verifican que ahora se devuelve un error
    // controlado en vez de crashear.
    [Fact]
    public void Evaluate_DeeplyNestedParentheses_ReturnsErrorInsteadOfCrashing()
    {
        string expr = new string('(', 5000) + "1" + new string(')', 5000);
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.False(result.Success);
        Assert.Contains("anidada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ManyChainedUnaryMinus_ReturnsErrorInsteadOfCrashing()
    {
        string expr = new string('-', 5000) + "1";
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.False(result.Success);
        Assert.Contains("anidada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ManyChainedPowers_ReturnsErrorInsteadOfCrashing()
    {
        string expr = string.Join("^", System.Linq.Enumerable.Repeat("1", 5000));
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.False(result.Success);
        Assert.Contains("anidada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ModeratelyNestedParentheses_StillSucceeds()
    {
        // El límite no debe romper fórmulas reales razonablemente anidadas.
        string expr = new string('(', 50) + "1" + new string(')', 50);
        var result = QuantityFormulaService.Evaluate(expr);
        Assert.True(result.Success);
        Assert.Equal(1m, result.Value);
    }

    [Theory]
    [InlineData("+C1+C2", 30)]
    [InlineData("=C1*C2", 200)]
    [InlineData("+C1*1.15", 11.5)]
    [InlineData("+(C1+C2)/2", 15)]
    [InlineData("=SUMA(C1, C2, C3)", 60)]
    [InlineData("+SUMA(C1:C3)", 60)]
    [InlineData("=SUM(C1..C3)", 60)]
    [InlineData("=PROMEDIO(C1:C3)", 20)]
    [InlineData("=MAX(C1, C2, C3)", 30)]
    [InlineData("=MIN(C1, C2, C3)", 10)]
    [InlineData("=REDONDEAR(C1/3, 2)", 3.33)]
    [InlineData("=REDONDEAR(C1/3; 2)", 3.33)]
    [InlineData("+C1*15%", 1.5)]
    [InlineData("=ABS(-25)", 25)]
    [InlineData("=RAIZ(100)", 10)]
    [InlineData("=SI(C1 > 0, 100, 0)", 100)]
    public void Evaluate_ExcelFunctions_AndCasillaAliases(string expr, decimal expected)
    {
        var vars = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["C1"] = 10m,
            ["C2"] = 20m,
            ["C3"] = 30m,
            ["R1"] = 10m,
            ["F1"] = 10m
        };
        var result = QuantityFormulaService.Evaluate(expr, vars);
        Assert.True(result.Success, result.Error);
        Assert.Equal(expected, result.Value);
    }
}
