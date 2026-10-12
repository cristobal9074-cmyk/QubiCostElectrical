using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class ProjectDirtyScopeTests
{
    [Theory]
    [InlineData("itemizado", "itemizado")]
    [InlineData("item", "itemizado")]
    [InlineData("cubicación", "cubicacion")]
    [InlineData("plantillas", "plantillas")]
    [InlineData("templates", "plantillas")]
    [InlineData("conjuntos", "conjuntos_electricos")]
    [InlineData("project", "project_info")]
    [InlineData(null, "project_info")]
    [InlineData("  ", "project_info")]
    public void Parse_NormalizesAliases(string? raw, string expectedKey)
    {
        Assert.Equal(expectedKey, ProjectDirtyScope.Parse(raw).Key);
    }

    [Fact]
    public void MarkDirty_Typed_EquivalentToString()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test");
        session.MarkSaved();

        session.MarkDirty(ProjectDirtyScope.Itemizado, ProjectDirtyScope.Cubicacion);
        var typed = session.GetDirtyScopes().OrderBy(x => x).ToArray();

        session.MarkSaved();
        session.MarkDirty("itemizado", "cubicacion");
        var strings = session.GetDirtyScopes().OrderBy(x => x).ToArray();

        Assert.Equal(strings, typed);
    }
}

public sealed class CostoBreakdownLineBuilderTests
{
    [Fact]
    public void BuildLines_ExcelPolicy_IncludesCostoTotalProyecto()
    {
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true,
            GastosGeneralesPct = 5,
            UtilidadPct = 8,
            ValorAgregadoEquiposPct = 2,
            MetrosCuadrados = 0
        };
        var cost = new CostBreakdown
        {
            ManoObraUf = 1,
            MaterialesUf = 2,
            EquiposUf = 3,
            CostoDirectoUf = 10,
            GastosGeneralesUf = 0.5m,
            UtilidadAditivaUf = 0.8m,
            ValorAgregadoEquiposUf = 0.1m,
            CostoProyectoUf = 11.4m,
            DiferenciaFactoresUf = 1m,
            TotalVentaNetoUf = 12,
            IvaUf = 2,
            TotalVentaUf = 14
        };

        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);
        Assert.Contains(lines, l => l.Label == "Costo total proyecto");
        Assert.Contains(lines, l => l.Label.StartsWith("Utilidades", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Label == "Subtotal");
    }

    [Fact]
    public void BuildLines_ClassicSobreVenta_UsesMargenLabel()
    {
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = false,
            MarkupMethod = "SobreVenta",
            UtilidadPct = 10,
            GastosGeneralesPct = 5
        };
        var cost = new CostBreakdown
        {
            CostoDirectoUf = 10,
            SubtotalUf = 11,
            UtilidadUf = 1.2m,
            TotalVentaNetoUf = 12,
            TotalVentaUf = 14
        };

        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);
        Assert.Contains(lines, l => l.Label.StartsWith("Margen s/ venta", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Cubicación", "Partidas valorizadas desde takeoff")]
    [InlineData("APU extra", "Complementarios y ajustes de APU")]
    [InlineData("", "Origen del costo directo")]
    public void DescribeOrigen_MapsKnownPrefixes(string origen, string expected)
    {
        Assert.Equal(expected, CostoOrigenDescriptor.Describe(origen));
    }

    [Fact]
    public void BuildLines_ClpProject_LabelsPricePerSquareMeterInPesos()
    {
        var project = new ProjectInfoRow { MonedaPresupuesto = "CLP", MetrosCuadrados = 100 };
        var cost = new CostBreakdown { TotalVentaUf = 10_000_000m, UfM2 = 100_000m };

        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);

        var metro = Assert.Single(lines, l => CostoBreakdownLineBuilder.IsMetroLine(l.Label));
        Assert.StartsWith("$ / m²", metro.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("UF", metro.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLines_UfProject_LabelsPricePerSquareMeterInUf()
    {
        var project = new ProjectInfoRow { MonedaPresupuesto = "UF", MetrosCuadrados = 100 };
        var cost = new CostBreakdown { TotalVentaUf = 10_000_000m, UfM2 = 100_000m };

        var lines = CostoBreakdownLineBuilder.BuildLines(cost, project);

        var metro = Assert.Single(lines, l => CostoBreakdownLineBuilder.IsMetroLine(l.Label));
        Assert.StartsWith("UF / m²", metro.Label, StringComparison.Ordinal);
    }
}
