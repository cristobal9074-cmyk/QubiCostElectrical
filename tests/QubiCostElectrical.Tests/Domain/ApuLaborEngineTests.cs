using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ApuLaborEngineTests
{
    [Fact]
    public void FactorCompuesto_Default_IsOne()
    {
        Assert.Equal(1m, ApuLaborEngine.FactorCompuesto(new ApuCondicionesLaboralesRow()));
    }

    [Fact]
    public void FactorCompuesto_StacksNocturnoAndAltura()
    {
        var c = new ApuCondicionesLaboralesRow
        {
            FactorAlturaPct = 10m,
            TrabajoNocturno = true,
            RecargoNocturnoPct = 25m
        };
        Assert.Equal(1.35m, ApuLaborEngine.FactorCompuesto(c));
    }

    [Fact]
    public void CostoUnitLabor_AppliesFactorOnlyToMoHhSc()
    {
        var cond = new ApuCondicionesLaboralesRow { FactorAlturaPct = 20m };
        var mo = new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, ValorHHUf = 10m };
        var mat = new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Rendimiento = 1m, PrecioUf = 10m };

        Assert.Equal(12m, ApuLaborEngine.CostoUnitLabor(mo, cond));
        Assert.Equal(10m, ApuLaborEngine.CostoUnitLabor(mat, cond));
    }

    [Fact]
    public void DesviacionRendimiento_ComputesPct()
    {
        var c = new ApuCondicionesLaboralesRow { RendimientoEstimadoHH = 10m, RendimientoRealHH = 13m };
        Assert.Equal(30m, ApuLaborEngine.DesviacionRendimientoPct(c));
        Assert.True(ApuLaborEngine.RequiereRevisionRendimiento(c));
    }

    [Fact]
    public void Apu_CostoMo_UsesLaborFactor()
    {
        var apu = new ApuRow
        {
            CondicionesLaborales = new ApuCondicionesLaboralesRow { EdificioEnOperacion = true, RecargoOperacionPct = 10m },
            HerramientasPct = 0m,
            ImprevistosAlzaPct = 0m,
            Recursos =
            {
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MO, Rendimiento = 1m, ValorHHUf = 10m }
            }
        };

        Assert.Equal(11m, apu.CostoMoUf);
    }
}
