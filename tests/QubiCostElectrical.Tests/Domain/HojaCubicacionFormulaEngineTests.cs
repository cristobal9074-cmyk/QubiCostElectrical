using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class HojaCubicacionFormulaEngineTests
{
    [Fact]
    public void IndexToColumnLetter_MatchesExcelConventions()
    {
        Assert.Equal("C", HojaCubicacionFormulaEngine.IndexToColumnLetter(0));
        Assert.Equal("D", HojaCubicacionFormulaEngine.IndexToColumnLetter(1));
        Assert.Equal("Z", HojaCubicacionFormulaEngine.IndexToColumnLetter(23));
        Assert.Equal("AA", HojaCubicacionFormulaEngine.IndexToColumnLetter(24));
        Assert.Equal("AB", HojaCubicacionFormulaEngine.IndexToColumnLetter(25));
    }

    [Fact]
    public void ColumnLetterToDataIndex_MatchesCorrectly()
    {
        Assert.Equal(0, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("C"));
        Assert.Equal(1, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("d"));
        Assert.Equal(23, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("Z"));
        Assert.Equal(24, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("AA"));
        Assert.Equal(-1, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("A")); // Reservada Descripción
        Assert.Equal(-1, HojaCubicacionFormulaEngine.ColumnLetterToDataIndex("B")); // Reservada Suma
    }

    [Fact]
    public void EvaluateRow_BasicNumbersAndRowSum()
    {
        var raw = new List<string> { "10", "20.5", "30,5" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.Equal(3, result.Cells.Count);
        Assert.True(result.Cells[0].Success);
        Assert.Equal(10m, result.Cells[0].Value);
        Assert.Equal(20.5m, result.Cells[1].Value);
        Assert.Equal(30.5m, result.Cells[2].Value);
        Assert.Equal(61m, result.Suma);
    }

    [Fact]
    public void EvaluateRow_FormulasWithCellReferences()
    {
        // C=10, D=20, E = =C+D*2 (50)
        var raw = new List<string> { "10", "20", "=C + D * 2" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[2].Success);
        Assert.Equal(50m, result.Cells[2].Value);
        Assert.Equal(80m, result.Suma);
    }

    [Fact]
    public void EvaluateRow_FormulasWithExcelFunctions_SpanishAndEnglish()
    {
        // C=10, D=20, E=30, F==SUMA(C:E), G==PROMEDIO(C:E)
        var raw = new List<string> { "10", "20", "30", "=SUMA(C:E)", "=PROMEDIO(C:E)" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[3].Success);
        Assert.Equal(60m, result.Cells[3].Value);

        Assert.True(result.Cells[4].Success);
        Assert.Equal(20m, result.Cells[4].Value);
    }

    [Fact]
    public void EvaluateRow_SupportsSemicolonAndCommaSeparators()
    {
        // C=10, D=25, E==SUM(C; D; 15), F==MAX(C, D, 50)
        var raw = new List<string> { "10", "25", "=SUM(C; D; 15)", "=MAX(C, D, 50)" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[2].Success);
        Assert.Equal(50m, result.Cells[2].Value);

        Assert.True(result.Cells[3].Success);
        Assert.Equal(50m, result.Cells[3].Value);
    }

    [Fact]
    public void EvaluateRow_ChainedDependenciesBetweenFormulas()
    {
        // C = =5*10 (50)
        // D = =C*2 (100)
        // E = =D+C (150)
        var raw = new List<string> { "=5*10", "=C*2", "=D+C" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[0].Success);
        Assert.Equal(50m, result.Cells[0].Value);

        Assert.True(result.Cells[1].Success);
        Assert.Equal(100m, result.Cells[1].Value);

        Assert.True(result.Cells[2].Success);
        Assert.Equal(150m, result.Cells[2].Value);

        Assert.Equal(300m, result.Suma);
    }

    [Fact]
    public void EvaluateRow_DetectsCircularReferences()
    {
        // C = =D, D = =C
        var raw = new List<string> { "=D", "=C" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.False(result.Cells[0].Success);
        Assert.Equal("¡CIRCULAR!", result.Cells[0].DisplayText);
        Assert.False(result.Cells[1].Success);
        Assert.Equal("¡CIRCULAR!", result.Cells[1].DisplayText);
    }

    [Fact]
    public void EvaluateRow_PercentageAndRounding()
    {
        // C=100, D==C*19%, E==REDONDEAR(12.3456, 2)
        var raw = new List<string> { "100", "=C*19%", "=REDONDEAR(12.3456; 2)" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[1].Success);
        Assert.Equal(19m, result.Cells[1].Value);

        Assert.True(result.Cells[2].Success);
        Assert.Equal(12.35m, result.Cells[2].Value);
    }

    [Fact]
    public void EvaluateRow_CellReferencesWithRowNumbersIgnoredFlexibly()
    {
        // Si el usuario escribe C1, D1, etc.
        var raw = new List<string> { "15", "25", "=C1 + D1" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.True(result.Cells[2].Success);
        Assert.Equal(40m, result.Cells[2].Value);
    }

    [Fact]
    public void EvaluateGrid_CrossRowReferences()
    {
        // Fila 0 (Fila 1 de Excel): C1 = 50, D1 = 10
        // Fila 1 (Fila 2 de Excel): C2 = =C1*2 (100), D2 = =C2+D1 (110)
        var row0 = new List<string> { "50", "10" };
        var row1 = new List<string> { "=C1*2", "=C2+D1" };
        var grid = new List<IReadOnlyList<string?>> { row0, row1 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(60m, result.Rows[0].Suma);
        Assert.Equal(100m, result.Rows[1].Cells[0].Value);
        Assert.Equal(110m, result.Rows[1].Cells[1].Value);
        Assert.Equal(210m, result.Rows[1].Suma);
        Assert.Equal(270m, result.Subtotal);
    }

    [Fact]
    public void EvaluateGrid_VerticalRangeSum()
    {
        // C1=10, C2=20, C3=30, C4==SUMA(C1:C3) -> 60
        var row0 = new List<string> { "10" };
        var row1 = new List<string> { "20" };
        var row2 = new List<string> { "30" };
        var row3 = new List<string> { "=SUMA(C1:C3)" };
        var grid = new List<IReadOnlyList<string?>> { row0, row1, row2, row3 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(60m, result.Rows[3].Cells[0].Value);
    }

    [Fact]
    public void EvaluateRow_PlusPrefixSupportedLikeExcel()
    {
        // En Excel escribir +50*2 es común con teclado numérico
        var raw = new List<string> { "+50*2", "+100+25" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.Equal(100m, result.Cells[0].Value);
        Assert.Equal(125m, result.Cells[1].Value);
    }

    [Fact]
    public void EvaluateRow_PlainTextToleratedWithoutError()
    {
        // Si el usuario escribe etiquetas de texto como "Depto 101"
        var raw = new List<string> { "Depto 101", "Piso 2", "50" };
        var result = HojaCubicacionFormulaEngine.EvaluateRow(raw);

        Assert.Equal("Depto 101", result.Cells[0].DisplayText);
        Assert.Equal(0m, result.Cells[0].Value);
        Assert.Equal("Piso 2", result.Cells[1].DisplayText);
        Assert.Equal(50m, result.Cells[2].Value);
        Assert.Equal(50m, result.Suma);
    }

    [Fact]
    public void EvaluateGrid_ArbitraryCellOperationsWithoutEqualSign()
    {
        // El usuario puede escribir C1-H2 o c1*h2 directamente
        // C1 = 100, H1 (data index 5) = 20
        // D1 = C1-H1 (80)
        // E1 = C1*H1 (2000)
        var row0 = new List<string> { "100", "C1-H1", "C1*H1", "", "", "20" };
        var grid = new List<IReadOnlyList<string?>> { row0 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(80m, result.Rows[0].Cells[1].Value);
        Assert.Equal(2000m, result.Rows[0].Cells[2].Value);
    }

    [Fact]
    public void EvaluateGrid_ReferencingEmptyOrFutureCell_DefaultsToZeroLikeExcel()
    {
        // En Excel, si H2 está vacía o en una fila que aún no existe, su valor numérico es 0
        // C1 = 50, D1 = C1 - H2 -> 50 - 0 = 50
        var row0 = new List<string> { "50", "=C1-H2" };
        var grid = new List<IReadOnlyList<string?>> { row0 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(50m, result.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void EvaluateFunction_ProductoAndPi()
    {
        // PRODUCTO(C1, D1) con C1=5, D1=4 -> 20
        var row0 = new List<string> { "5", "4", "=PRODUCTO(C1; D1)", "=PI()*2" };
        var grid = new List<IReadOnlyList<string?>> { row0 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(20m, result.Rows[0].Cells[2].Value);
        Assert.True(result.Rows[0].Cells[3].Value > 6.28m && result.Rows[0].Cells[3].Value < 6.29m);
    }

    [Fact]
    public void EvaluateGrid_CrossCellArbitraryMultiplicationAndSubtraction_C1MinusH2_C1TimesH2()
    {
        // Fila 0 (Fila 1 de Excel): C1 = 100, D1 = =C1-H2, E1 = =C1*H2
        // Fila 1 (Fila 2 de Excel): C2 = 10,  D2 = 5,       H2 = 25 (H es índice de datos 5)
        var row0 = new List<string?> { "100", "=C1-H2", "=C1*H2", "", "", "" };
        var row1 = new List<string?> { "10", "5", "", "", "", "25" };
        var grid = new List<IReadOnlyList<string?>> { row0, row1 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        // C1 (100) - H2 (25) = 75
        Assert.Equal(75m, result.Rows[0].Cells[1].Value);
        // C1 (100) * H2 (25) = 2500
        Assert.Equal(2500m, result.Rows[0].Cells[2].Value);
    }

    [Fact]
    public void EvaluateGrid_ArbitraryFormulaWithoutEqual_C1TimesH2MinusD1()
    {
        // El usuario tipea directamente C1*H2 o C1*H2-D1 sin signo igual
        // Fila 0 (Fila 1): C1 = 50, D1 = 10, E1 = C1*H2-D1
        // Fila 1 (Fila 2): C2 = 0,  H2 = 4 (data index 5)
        var row0 = new List<string?> { "50", "10", "C1*H2-D1", "", "", "" };
        var row1 = new List<string?> { "0", "", "", "", "", "4" };
        var grid = new List<IReadOnlyList<string?>> { row0, row1 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        // 50 * 4 - 10 = 190
        Assert.Equal(190m, result.Rows[0].Cells[2].Value);
    }

    [Fact]
    public void EvaluateGrid_MultiplicationAndDivisionBetweenAdjacentAndRemoteCells()
    {
        // Celdas adyacentes y remotas
        // Fila 1: C1 = 80, D1 = =C1/4 (20), E1 = =(C1+D1)*2 (200)
        // Fila 2: C2 = =E1/H2, H2 = 10
        var row0 = new List<string?> { "80", "=C1/4", "=(C1+D1)*2", "", "", "" };
        var row1 = new List<string?> { "=E1/H2", "", "", "", "", "10" };
        var grid = new List<IReadOnlyList<string?>> { row0, row1 };

        var result = HojaCubicacionFormulaEngine.EvaluateGrid(grid);

        Assert.Equal(20m, result.Rows[0].Cells[1].Value);
        Assert.Equal(200m, result.Rows[0].Cells[2].Value);
        Assert.Equal(20m, result.Rows[1].Cells[0].Value); // 200 / 10 = 20
    }
}
