using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetKpiServiceTests
{
    private readonly BudgetKpiService _service = new(new BudgetCalculationService());

    [Theory]
    [InlineData(150d, "150 UF")]
    [InlineData(50d, "50,0 UF")]
    [InlineData(5d, "5,00 UF")]
    [InlineData(0d, "0,00 UF")]
    [InlineData(-100d, "-100 UF")]
    public void FormatKpiUf_Various(double uf, string expected)
    {
        Assert.Equal(expected, _service.FormatKpiUf((decimal)uf));
    }

    [Theory]
    [InlineData(5d, "↑ 5,0%")]
    [InlineData(-3d, "↓ 3,0%")]
    [InlineData(0d, "↑ 0,0%")]
    public void FormatKpiDelta_Various(double pct, string expected)
    {
        Assert.Equal(expected, _service.FormatKpiDelta((decimal)pct));
    }

    [Fact]
    public void BuildDashboardKpis_ReturnsFourKpis()
    {
        var kpis = _service.BuildDashboardKpis(100m, 60m, 30m, 0.5m, true);
        Assert.Equal(4, kpis.Count);
        Assert.Contains(kpis, k => k.Title == "TOTAL VENTA");
        Assert.Contains(kpis, k => k.Title == "MATERIALES");
        Assert.Contains(kpis, k => k.Title == "MANO DE OBRA");
        Assert.Contains(kpis, k => k.Title == "UF / M²");
    }

    [Fact]
    public void GetDelta_WithVersions_Calculates()
    {
        var versions = new List<BudgetVersionRow>
        {
            new()
            {
                Codigo = "V1",
                FechaCreacion = DateTime.Now.AddDays(-1),
                Snapshot = new BudgetVersionData
                {
                    Cubicacion = new List<CubicacionRow>
                    {
                        new() { ItemId = "P1", Codigo = "MAT1", Cantidad = 10m, MatClp = 5m }
                    },
                    Proyecto = new ProjectInfoRow()
                }
            }
        };
        var delta = _service.GetDelta("Venta", 60m, versions);
        Assert.NotNull(delta);
    }

    [Fact]
    public void GetDelta_NoVersions_ReturnsNull()
    {
        var delta = _service.GetDelta("Venta", 100m, Enumerable.Empty<BudgetVersionRow>());
        Assert.Null(delta);
    }

    [Fact]
    public void GetHistoryValues_SeedsWhenFew()
    {
        var values = _service.GetHistoryValues("Venta", 100m, Enumerable.Empty<BudgetVersionRow>());
        Assert.Equal(7, values.Count);
    }

    [Fact]
    public void GetMetricValue_ReturnsCorrectKey()
    {
        var version = new BudgetVersionRow();
        var svc = new BudgetCalculationService();
        var kpiSvc = new BudgetKpiService(svc);
        var result = kpiSvc.GetMetricValue("Venta", new BudgetVersionData
        {
            Cubicacion = new List<CubicacionRow>(),
            Proyecto = new ProjectInfoRow()
        });
        Assert.Equal(0m, result);
    }
}
