using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public class ExcelCostosBridgeTests
{
    [Fact]
    public void SumMoDotacion_AppliesLeyesAndFallbackMonths()
    {
        var directa = new List<MoDirectaRow>
        {
            new() { Cargo = "Maestro", Dotacion = 2, SueldoMensualClp = 1_000_000m }
        };
        var indirecta = new List<MoIndirectaRow>
        {
            new() { Cargo = "Admin", Dotacion = 1, SueldoMensualClp = 500_000m }
        };

        // Base: (2*1M + 1*0.5M) * 3 meses = 7_500_000; +35% leyes = 10_125_000
        var total = ExcelCostosBridge.SumMoDotacionClp(directa, indirecta, fallbackMonths: 3, leyesSocialesPct: 35m);
        Assert.Equal(10_125_000m, total);
    }

    [Fact]
    public void EnrichFromProjectData_PrefersMoTotalClp()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                MoDuracionMeses = 12,
                LeyesSocialesMoPct = 35m,
                MoTotalClp = 99_000_000m,
            },
            MoDirecta = new List<MoDirectaRow>
            {
                new() { Dotacion = 1, SueldoMensualClp = 100m }
            },
            GastosOperacionales = new List<GastoOperacionalRow>
            {
                new() { MontoClp = 1_500m }
            }
        };

        var settings = ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(data.Proyecto), data);
        Assert.Equal(99_000_000m, settings.ManoObraDotacionClp);
        Assert.Equal(1_500m, settings.GastosOperacionalesClp);
    }

    [Fact]
    public void PresetPel029_HasMatAndPvcAt157()
    {
        Assert.Equal(1.57m, ExcelCostosBridge.PresetFactoresPel029["Conductores"]);
        Assert.Equal(1.57m, ExcelCostosBridge.PresetFactoresPel029["Canalización"]);
        Assert.Equal(1.30m, ExcelCostosBridge.PresetFactoresPel029["Equipos"]);
    }

    [Fact]
    public void PresentationFactor_AppliesFamilyDiscount()
    {
        var eff = ExcelCostosBridge.PresentationFactor(1.57m, 10m);
        Assert.Equal(1.413m, eff);
    }
}
