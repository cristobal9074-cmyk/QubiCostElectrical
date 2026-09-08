using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class CostoMoLaborProjectionBuilderTests
{
    [Fact]
    public void Build_Empty_UsesDefaultTemplatesWithZeroDotacion()
    {
        var proj = CostoMoLaborProjectionBuilder.Build(null, null);
        Assert.Equal(2, proj.Directa.Count);
        Assert.Equal(6, proj.Indirecta.Count);
        Assert.All(proj.Directa, l => Assert.Equal(0, l.Dotacion));
        Assert.Equal(0m, proj.Totals.TotalClp);
    }

    [Fact]
    public void Build_WithRows_ComputesSharePct()
    {
        var directa = new List<MoDirectaRow>
        {
            new() { Cargo = "A", Dotacion = 2, SueldoMensualClp = 100m },
            new() { Cargo = "B", Dotacion = 1, SueldoMensualClp = 100m },
        };
        var proj = CostoMoLaborProjectionBuilder.Build(directa, []);
        Assert.Equal(300m, proj.Totals.DirectaClp);
        Assert.Equal(200m / 300m * 100m, proj.Directa[0].Pct, 4);
        Assert.Equal(100m, proj.Totals.DirectaPct);
        Assert.Equal(0m, proj.Totals.IndirectaPct);
    }
}

public sealed class CostoGoComposeProjectionBuilderTests
{
    [Fact]
    public void Build_EmptyCategories_ReturnsEmpty()
    {
        var proj = CostoGoComposeProjectionBuilder.Build([], 0);
        Assert.Empty(proj.Segments);
        Assert.Empty(proj.Legend);
    }

    [Fact]
    public void Build_ZeroTotal_ShowsPlaceholderSegmentAndDashPct()
    {
        var cats = new[]
        {
            new CostoGoCategoryShare("3.1", 0, 0),
            new CostoGoCategoryShare("3.2", 0, 0),
        };
        var proj = CostoGoComposeProjectionBuilder.Build(cats, 0);
        Assert.Single(proj.Segments);
        Assert.Equal(1000, proj.Segments[0].WidthUnits);
        Assert.All(proj.Legend, l => Assert.Equal("—", l.PctLabel));
    }

    [Fact]
    public void Build_WithAmounts_SharesWidthByMonto()
    {
        var cats = new[]
        {
            new CostoGoCategoryShare("3.1", 75, 75),
            new CostoGoCategoryShare("3.2", 25, 25),
        };
        var proj = CostoGoComposeProjectionBuilder.Build(cats, 100);
        Assert.Equal(2, proj.Segments.Count);
        Assert.True(proj.Segments[0].WidthUnits > proj.Segments[1].WidthUnits);
        Assert.Equal("75%", proj.Legend[0].PctLabel);
    }
}

public sealed class PartidaCantidadVariablesBuilderTests
{
    [Fact]
    public void Build_MapsR1Rn()
    {
        var vars = PartidaCantidadVariablesBuilder.Build([10m, 2.5m, 0m]);
        Assert.Equal(10m, vars["R1"]);
        Assert.Equal(2.5m, vars["R2"]);
        Assert.Equal(0m, vars["R3"]);
    }

    [Fact]
    public void Evaluate_R1PlusR2_UsesQuantityFormulaService()
    {
        var vars = PartidaCantidadVariablesBuilder.Build([3m, 4m]);
        var result = QuantityFormulaService.Evaluate("R1+R2", vars);
        Assert.True(result.Success);
        Assert.Equal(7m, result.Value);
    }
}

public sealed class CubicacionSectorExpressionCompactorTests
{
    [Fact]
    public void TrimTrailingEmpty_RemovesTail()
    {
        var compacted = CubicacionSectorExpressionCompactor.TrimTrailingEmpty(["1", "2+2", "", "  ", ""]);
        Assert.Equal(new[] { "1", "2+2" }, compacted);
    }

    [Fact]
    public void TrimTrailingEmpty_AllEmpty_ReturnsEmpty()
    {
        Assert.Empty(CubicacionSectorExpressionCompactor.TrimTrailingEmpty(["", " "]));
    }
}
