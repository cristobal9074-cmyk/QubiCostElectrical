using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class AutoFillSeriesGeneratorTests
{
    [Fact]
    public void GenerateNextValues_ConsecutiveNumbers_ExtendsSeries()
    {
        var source = new List<string> { "201", "202" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 4, isHorizontal: true);

        Assert.Equal(4, next.Count);
        Assert.Equal("203", next[0]);
        Assert.Equal("204", next[1]);
        Assert.Equal("205", next[2]);
        Assert.Equal("206", next[3]);
    }

    [Fact]
    public void GenerateNextValues_TextWithNumberSuffix_ExtendsSeries()
    {
        var source = new List<string> { "Depto 201", "Depto 202" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 3, isHorizontal: true);

        Assert.Equal(3, next.Count);
        Assert.Equal("Depto 203", next[0]);
        Assert.Equal("Depto 204", next[1]);
        Assert.Equal("Depto 205", next[2]);
    }

    [Fact]
    public void GenerateNextValues_SingleTextWithNumber_IncrementsStepOne()
    {
        var source = new List<string> { "Depto 101" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 3, isHorizontal: true);

        Assert.Equal(3, next.Count);
        Assert.Equal("Depto 102", next[0]);
        Assert.Equal("Depto 103", next[1]);
        Assert.Equal("Depto 104", next[2]);
    }

    [Fact]
    public void GenerateNextValues_PaddedZeros_PreservesPadding()
    {
        var source = new List<string> { "Piso 01", "Piso 02" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 2, isHorizontal: true);

        Assert.Equal(2, next.Count);
        Assert.Equal("Piso 03", next[0]);
        Assert.Equal("Piso 04", next[1]);
    }

    [Fact]
    public void GenerateNextValues_StepOfTen_AdvancesCorrectly()
    {
        var source = new List<string> { "10", "20" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 3, isHorizontal: true);

        Assert.Equal(3, next.Count);
        Assert.Equal("30", next[0]);
        Assert.Equal("40", next[1]);
        Assert.Equal("50", next[2]);
    }

    [Fact]
    public void GenerateNextValues_BackwardDrag_DecrementsSeries()
    {
        var source = new List<string> { "201", "202" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 2, isHorizontal: true, isBackward: true);

        Assert.Equal(2, next.Count);
        Assert.Equal("200", next[0]);
        Assert.Equal("199", next[1]);
    }

    [Fact]
    public void GenerateNextValues_Formulas_ShiftsCellReferencesHorizontally()
    {
        var source = new List<string> { "=C1+D1" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 2, isHorizontal: true);

        Assert.Equal(2, next.Count);
        Assert.Equal("=D1+E1", next[0]);
        Assert.Equal("=E1+F1", next[1]);
    }

    [Fact]
    public void GenerateNextValues_Formulas_ShiftsCellReferencesVertically()
    {
        var source = new List<string> { "=C1*2" };
        var next = AutoFillSeriesGenerator.GenerateNextValues(source, 2, isHorizontal: false);

        Assert.Equal(2, next.Count);
        Assert.Equal("=C2*2", next[0]);
        Assert.Equal("=C3*2", next[1]);
    }

    [Fact]
    public void FormatDecimal_RoundsToMaxTwoDecimals_AndIntegerWithoutDecimalPoint()
    {
        Assert.Equal("201", HojaCubicacionFormulaEngine.FormatDecimal(201m));
        Assert.Equal("201,5", HojaCubicacionFormulaEngine.FormatDecimal(201.5m).Replace('.', ','));
        Assert.Equal("10,33", HojaCubicacionFormulaEngine.FormatDecimal(10.33333333m).Replace('.', ','));
        Assert.Equal("14,29", HojaCubicacionFormulaEngine.FormatDecimal(14.285714m).Replace('.', ','));
    }
}
