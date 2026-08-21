using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Cálculo del APU: costo de recurso (base + merma + imprevistos), agregación por tipo,
/// herramientas/transporte automáticos vs. por recurso, y precio unitario de partida.
/// </summary>
public sealed class ApuCalculationTests
{
    [Fact]
    public void Resource_CostoUnit_AppliesMermaThenImprevistos()
    {
        var r = new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            Rendimiento = 2m,
            PrecioUf = 3m,
            MermaPct = 10m,
            ImprevistosPct = 5m
        };

        Assert.Equal(6m, r.CostoBaseUf);       // 2 * 3
        Assert.Equal(0.6m, r.MermaUf);         // 6 * 10%
        Assert.Equal(0.33m, r.ImprevistosUf);  // (6 + 0.6) * 5%
        Assert.Equal(6.93m, r.CostoUnitUf);    // 6 + 0.6 + 0.33
    }

    [Fact]
    public void Resource_MO_PrefersValorHHOverPrecioUf()
    {
        var r = new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, PrecioUf = 0m, ValorHHUf = 10m };
        Assert.Equal(10m, r.PrecioBaseUf);
        Assert.Equal(10m, r.CostoBaseUf);
    }

    [Fact]
    public void Apu_PrecioUnit_AddsToolingMermaTransportAndImprevistos()
    {
        var apu = new ApuRow
        {
            Descripcion = "Punto de alumbrado",
            Unidad = "UN",
            HerramientasPct = 3m,
            ImprevistosAlzaPct = 2m,
            Recursos =
            {
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 10m },
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, ValorHHUf = 10m }
            }
        };

        Assert.Equal(10m, apu.CostoMaterialUf);
        Assert.Equal(10m, apu.CostoMoUf);
        Assert.Equal(0.3m, apu.HerramientasCalculadoUf); // (10 mano) * 3%
        // Directo = 10 mat + 10 mo + 0.3 herr = 20.3 ; imprevistos = 20.3 * 2% = 0.406
        Assert.Equal(20.3m, apu.CostoDirectoUf);
        Assert.Equal(0.406m, apu.ImprevistosUf);
        Assert.Equal(20.706m, apu.PrecioUnitUf);
    }

    [Fact]
    public void Apu_ResourceTooling_OverridesAutoTooling()
    {
        var apu = new ApuRow
        {
            Descripcion = "x",
            Unidad = "UN",
            HerramientasPct = 3m,
            Recursos =
            {
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, ValorHHUf = 10m },
                new ApuRecursoRow { Tipo = ApuTipoRecurso.HERR, Rendimiento = 1m, PrecioUf = 1m }
            }
        };

        // Con recurso HERR explícito (1) se usa ese y no el 3% automático.
        Assert.Equal(1m, apu.HerramientasCalculadoUf);
    }

    [Fact]
    public void Apu_TotalPartida_ScalesByQuantity()
    {
        var apu = new ApuRow
        {
            Descripcion = "x",
            Unidad = "UN",
            CantidadPartida = 5m,
            HerramientasPct = 0m,
            ImprevistosAlzaPct = 0m,
            Recursos = { new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 4m } }
        };

        Assert.Equal(4m, apu.PrecioUnitUf);
        Assert.Equal(20m, apu.TotalPartidaUf); // 4 * 5
    }

    [Fact]
    public void GetPricesForCubicacion_SplitsMaterialAndLabour()
    {
        var engine = new ApuEngine();
        var apu = new ApuRow
        {
            Descripcion = "x",
            Unidad = "UN",
            HerramientasPct = 0m,
            ImprevistosAlzaPct = 0m,
            Recursos =
            {
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 10m },
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, ValorHHUf = 4m }
            }
        };

        var (material, manoObra) = engine.GetPricesForCubicacion(apu);

        Assert.Equal(10m, material); // material + equipo + transporte + merma
        Assert.Equal(4m, manoObra);  // mo + sc + hh + herramientas + imprevistos
    }

    [Fact]
    public void GetPricesForCubicacion_NullApu_ReturnsZero()
    {
        var engine = new ApuEngine();
        var (material, manoObra) = engine.GetPricesForCubicacion(null);
        Assert.Equal(0m, material);
        Assert.Equal(0m, manoObra);
    }
}
