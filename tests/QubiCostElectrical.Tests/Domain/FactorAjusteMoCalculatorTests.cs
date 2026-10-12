using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class FactorAjusteMoCalculatorTests
{
    private static FactorAjusteMoLinea Linea(decimal moFija, decimal basePorcentaje, decimal cantidad) =>
        new(moFija, basePorcentaje, cantidad);

    [Fact]
    public void Calcular_RemanenteSobreBase_DerivaElFa()
    {
        // M.O. real 1.000.000 − ya cubierta (200 × 1.000 = 200.000) = 800.000
        // base % (40 × 1.000 = 40.000) → FA = 20,00
        var resultado = FactorAjusteMoCalculator.Calcular(
            1_000_000m,
            new[] { Linea(200m, 0m, 1_000m), Linea(0m, 40m, 1_000m) });

        Assert.Equal(1_000_000m, resultado.MoRealClp);
        Assert.Equal(200_000m, resultado.MoCubiertaClp);
        Assert.Equal(40_000m, resultado.BasePorcentajeClp);
        Assert.Equal(800_000m, resultado.RemanenteClp);
        Assert.Equal(1, resultado.LineasPorcentaje);
        Assert.Equal(20.00m, resultado.Fa);
        Assert.True(resultado.PuedeCalcular);
    }

    [Fact]
    public void Calcular_SinPlanDeMo_NoDerivaFa()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(0m, new[] { Linea(0m, 40m, 10m) });

        Assert.False(resultado.TienePlanMo);
        Assert.Null(resultado.Fa);
        Assert.False(resultado.PuedeCalcular);
    }

    [Fact]
    public void Calcular_SinMaterialesPorcentaje_NoDerivaFa()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(1_000_000m, new[] { Linea(500m, 0m, 10m) });

        Assert.True(resultado.TienePlanMo);
        Assert.False(resultado.HayBase);
        Assert.Null(resultado.Fa);
    }

    [Fact]
    public void Calcular_CatalogoYaCubreLaMoReal_NoInventaFaNegativo()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(100_000m, new[] { Linea(500m, 0m, 1_000m), Linea(0m, 40m, 1m) });

        Assert.Equal(-400_000m, resultado.RemanenteClp);
        Assert.Null(resultado.Fa);
    }

    [Fact]
    public void Calcular_LineasSinCantidad_SeIgnoran()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(
            1_000_000m,
            new[] { Linea(999_999m, 0m, 0m), Linea(0m, 0m, -5m), Linea(0m, 40m, 1_000m) });

        Assert.Equal(0m, resultado.MoCubiertaClp);
        Assert.Equal(25.00m, resultado.Fa);
    }

    [Fact]
    public void Calcular_BaseMinima_TopeaElFaEnElMaximo()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(1_000_000_000m, new[] { Linea(0m, 1m, 1m) });

        Assert.Equal(FactorAjusteMoCalculator.FaMaximo, resultado.Fa);
    }

    [Fact]
    public void Calcular_FaSeRedondeaADosDecimales()
    {
        // 1.000 ÷ 3 = 333,333... → 333,33
        var resultado = FactorAjusteMoCalculator.Calcular(1_000m, new[] { Linea(0m, 1m, 3m) });

        Assert.Equal(333.33m, resultado.Fa);
    }

    [Fact]
    public void Calcular_SinLineas_NuncaDerivaFa()
    {
        Assert.Null(FactorAjusteMoCalculator.Calcular(500m, null).Fa);
        Assert.Null(FactorAjusteMoCalculator.Calcular(500m, Array.Empty<FactorAjusteMoLinea>()).Fa);
    }

    // ── Brecha EN PPTO vs COSTO (Excel COSTOS!F192 contra G192) ───────────────

    /// <summary>
    /// Con el FA derivado, la M.O. del itemizado (EN PPTO) cuadra con el costo real (COSTO):
    /// 200.000 fijos + 40.000 de base × 20,00 = 1.000.000. Es la igualdad que el Excel busca.
    /// </summary>
    [Fact]
    public void MoItemizadoClp_ConElFaDerivado_CuadraConElCostoReal()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(
            1_000_000m,
            new[] { Linea(200m, 0m, 1_000m), Linea(0m, 40m, 1_000m) });

        Assert.Equal(1_000_000m, resultado.MoItemizadoClp(resultado.Fa!.Value));
        Assert.Equal(0m, FactorAjusteMoResultado.DesviacionPct(resultado.MoItemizadoClp(20m), 1_000_000m));
    }

    [Fact]
    public void MoItemizadoClp_ConFaDesalineado_MideLaBrechaEnPorcentaje()
    {
        var resultado = FactorAjusteMoCalculator.Calcular(
            1_000_000m,
            new[] { Linea(200m, 0m, 1_000m), Linea(0m, 40m, 1_000m) });

        // FA 25,00 ⇒ 200.000 + 1.000.000 = 1.200.000 contra 1.000.000 real → +20,00 %
        Assert.Equal(1_200_000m, resultado.MoItemizadoClp(25m));
        Assert.Equal(20.00m, FactorAjusteMoResultado.DesviacionPct(resultado.MoItemizadoClp(25m), 1_000_000m));
    }

    [Fact]
    public void DesviacionPct_SinCostoReal_NoDividePorCero()
    {
        Assert.Equal(0m, FactorAjusteMoResultado.DesviacionPct(500_000m, 0m));
    }
}
