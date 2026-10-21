using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionClipboardServiceTests
{
    [Fact]
    public void ParseExcelText_SimpleRows_ParsesAll()
    {
        string text = "PVC20\tTubo PVC 20mm\tML\t50\nPVC25\tTubo PVC 25mm\tML\t30";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(0, result.RejectedCount);
        Assert.Equal("PVC20", result.Rows[0].Codigo);
        Assert.Equal("50", result.Rows[0].CantidadRaw);
    }

    [Fact]
    public void ParseExcelText_BlankSeparatorLine_IsSkippedWithoutMisalignment()
    {
        string text = "PVC20\tTubo PVC 20mm\tML\t50\n\nPVC25\tTubo PVC 25mm\tML\t30";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("PVC20", result.Rows[0].Codigo);
        Assert.Equal("PVC25", result.Rows[1].Codigo);
    }

    [Fact]
    public void ParseExcelText_InvalidCantidad_RejectsRowAndWarns()
    {
        string text = "PVC20\tTubo PVC 20mm\tML\t@@@";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Equal(1, result.RejectedCount);
        Assert.True(result.Rows[0].IsRejected);
        Assert.Contains(result.Warnings, w => w.Contains("Fila rechazada", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseExcelText_ExtraColumns_AreCapturedAsSectorValues()
    {
        string text = "PVC20\tTubo PVC 20mm\tML\t50\t10\t20\t30";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].IsRejected);
        Assert.Equal("PVC20", result.Rows[0].Codigo);
        Assert.Equal(new[] { "10", "20", "30" }, result.Rows[0].SectorValues);
        Assert.Contains(result.Warnings, w => w.Contains("casilla/sector"));
    }

    [Fact]
    public void ParseExcelText_NoExtraColumns_NoOverflowWarning()
    {
        string text = "PVC20\tTubo PVC 20mm\tML\t50";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("más allá de"));
    }

    [Fact]
    public void ParseCantidad_FormulaLike_EvaluatesExpression()
    {
        decimal value = CubicacionClipboardService.ParseCantidad("2*3+4");

        Assert.Equal(10m, value);
    }

    [Fact]
    public void ParseCantidad_EmptyOrNull_ReturnsZero()
    {
        Assert.Equal(0m, CubicacionClipboardService.ParseCantidad(null));
        Assert.Equal(0m, CubicacionClipboardService.ParseCantidad("  "));
    }

    [Fact]
    public void ParseExcelText_RedondearDelDialecto_NoSeRechaza()
    {
        string text = "PVC20\tTubo\tML\t=REDONDEAR(10,5;0)";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Equal(0, result.RejectedCount);
        Assert.Equal("=REDONDEAR(10,5;0)", result.Rows[0].CantidadRaw);
    }

    [Fact]
    public void ParseExcelText_FuncionFueraDelDialecto_QuedaEnRojoYConservaElTexto()
    {
        string text = "PVC20\tTubo\tML\t=BUSCARV(1;2;3;4)";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.Equal(1, result.RejectedCount);
        Assert.True(result.Rows[0].IsRejected);
        Assert.Equal("=BUSCARV(1;2;3;4)", result.Rows[0].CantidadRaw);
        Assert.Contains("no está en el dialecto", result.Rows[0].RejectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("fx", result.Rows[0].RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseExcelText_CasillaFueraDelDialecto_RechazaLaFila()
    {
        string text = "PVC20\tTubo\tML\t4\t=BUSCARV(1;2)";

        var result = CubicacionClipboardService.ParseExcelText(text);

        Assert.True(result.Rows[0].IsRejected);
        Assert.Contains("Casilla 1", result.Rows[0].RejectionReason, StringComparison.Ordinal);
        Assert.Equal("4", result.Rows[0].CantidadRaw);
    }
}
