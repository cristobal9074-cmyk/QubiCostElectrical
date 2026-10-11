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
    public void SumStaffingPlanClp_MultipliesTarifaByDedicacionAndMeses()
    {
        var roles = new List<StaffingRoleRow>
        {
            new() { Cargo = "Administrador", TarifaMensualClp = 1_000_000m, DedicacionPct = 30m, Meses = 10 }, // 3_000_000
            new() { Cargo = "Supervisor", TarifaMensualClp = 2_000_000m, DedicacionPct = 100m, Meses = 10 }    // 20_000_000
        };

        Assert.Equal(23_000_000m, ExcelCostosBridge.SumStaffingPlanClp(roles));
    }

    [Fact]
    public void EnrichFromProjectData_IncludesStaffingPlanAndNegotiatedDiscounts()
    {
        var data = new ProjectFileData
        {
            GastosOperacionales = new List<GastoOperacionalRow> { new() { MontoClp = 1_000m } },
            StaffingPlan = new List<StaffingRoleRow>
            {
                new() { TarifaMensualClp = 500_000m, DedicacionPct = 100m, Meses = 1 }
            },
            NegotiatedDiscounts = new List<NegotiatedDiscountRow>
            {
                new() { Codigo = "TRAFO", DescuentoPct = 5m }
            }
        };

        var settings = ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(null), data);

        Assert.Equal(501_000m, settings.GastosOperacionalesClp);
        Assert.Single(settings.NegotiatedDiscounts);
        Assert.Equal("TRAFO", settings.NegotiatedDiscounts[0].Codigo);
    }

    [Fact]
    public void PresetEstandar_HasMatAndPvcAt157()
    {
        Assert.Equal(1.57m, ExcelCostosBridge.PresetFactoresEstandar["Conductores"]);
        Assert.Equal(1.57m, ExcelCostosBridge.PresetFactoresEstandar["Canalización"]);
        Assert.Equal(1.30m, ExcelCostosBridge.PresetFactoresEstandar["Equipos"]);
    }

    [Fact]
    public void PresentationFactor_AppliesFamilyDiscount()
    {
        var eff = ExcelCostosBridge.PresentationFactor(1.57m, 10m);
        Assert.Equal(1.413m, eff);
    }

    [Fact]
    public void SumMoIndirecta_MultipliesByFraccionObra()
    {
        var indirecta = new List<MoIndirectaRow>
        {
            new() { Cargo = "Admin", Dotacion = 1, SueldoMensualClp = 1_000_000m, FraccionObra = 0.3m },
            new() { Cargo = "Supervisor", Dotacion = 1, SueldoMensualClp = 1_000_000m }, // default 1
        };

        // (0,3 × 1M + 1 × 1M) × 3 meses = 3_900_000
        Assert.Equal(3_900_000m, ExcelCostosBridge.SumMoIndirecta(indirecta, fallbackMonths: 3));
    }

    [Fact]
    public void SumMoIndirecta_FraccionObraCero_NoCobraNada()
    {
        var indirecta = new List<MoIndirectaRow>
        {
            new() { Cargo = "Admin", Dotacion = 1, SueldoMensualClp = 1_000_000m, FraccionObra = 0m }
        };

        Assert.Equal(0m, ExcelCostosBridge.SumMoIndirecta(indirecta, fallbackMonths: 12));
    }

    [Fact]
    public void SumMoIndirecta_JsonSinFraccionObra_RetrocompatibleConValorUno()
    {
        // Fila guardada por una versión anterior (sin el campo): debe seguir cobrando 100%.
        const string json = """{"id":"abc","cargo":"Admin","dotacion":1,"sueldoMensualClp":1000000}""";
        var row = System.Text.Json.JsonSerializer.Deserialize<MoIndirectaRow>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(row);
        Assert.Equal(1m, row!.FraccionObra);
        Assert.Equal(1_000_000m, ExcelCostosBridge.SumMoIndirecta(new List<MoIndirectaRow> { row }, fallbackMonths: 1));
    }
}
