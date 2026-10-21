using System.Collections.Generic;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class CubicacionOpenCellRulesTests
{
    [Fact]
    public void FormulaValida_SeAcepta()
    {
        var ok = CubicacionOpenCellRules.TryAcceptQuantityText("10+2", new Dictionary<string, decimal>(), out string error);

        Assert.True(ok);
        Assert.Equal("", error);
    }

    [Fact]
    public void FormulaInvalida_NoSeConvierteEnCero()
    {
        var ok = CubicacionOpenCellRules.TryAcceptQuantityText("10/0", new Dictionary<string, decimal>(), out string error);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Numero_SeAcepta()
    {
        Assert.True(CubicacionOpenCellRules.TryAcceptQuantityText("12", new Dictionary<string, decimal>(), out _));
    }

    [Fact]
    public void Vacio_SeAceptaComoCero()
    {
        Assert.True(CubicacionOpenCellRules.TryAcceptQuantityText("", new Dictionary<string, decimal>(), out _));
    }

    [Fact]
    public void TextoQueNoEsCantidad_SeRechaza()
    {
        Assert.False(CubicacionOpenCellRules.TryAcceptQuantityText("abc", new Dictionary<string, decimal>(), out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
