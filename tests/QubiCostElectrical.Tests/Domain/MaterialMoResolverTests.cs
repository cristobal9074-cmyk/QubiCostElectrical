using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class MaterialMoResolverTests
{
    private static ProjectInfoRow ProyectoRendimiento(decimal tarifaDh) => new()
    {
        ModoMoInstalacion = MaterialMoResolver.ModoRendimiento,
        TarifaDhClp = tarifaDh
    };

    private static MaterialRow Material(decimal moClp, decimal rendimientoDh) => new()
    {
        Codigo = "X1",
        MoClp = moClp,
        RendimientoDh = rendimientoDh
    };

    // ── DebeUsarRendimiento ───────────────────────────────────────────────────

    [Fact]
    public void DebeUsarRendimiento_ModoRendimientoConTarifaYRendimiento_RetornaTrue()
    {
        Assert.True(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 464m), ProyectoRendimiento(42955m)));
    }

    [Fact]
    public void DebeUsarRendimiento_ModoDirecto_RetornaFalse()
    {
        var proyecto = new ProjectInfoRow { ModoMoInstalacion = MaterialMoResolver.ModoDirecto, TarifaDhClp = 42955m };
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 464m), proyecto));
    }

    [Fact]
    public void DebeUsarRendimiento_ModoVacio_ComportamientoHistorico_RetornaFalse()
    {
        var proyecto = new ProjectInfoRow { TarifaDhClp = 42955m };
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 464m), proyecto));
    }

    [Fact]
    public void DebeUsarRendimiento_SinRendimiento_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 0m), ProyectoRendimiento(42955m)));
    }

    [Fact]
    public void DebeUsarRendimiento_SinTarifa_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 464m), ProyectoRendimiento(0m)));
    }

    [Fact]
    public void DebeUsarRendimiento_MaterialOProyectoNulo_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(null, ProyectoRendimiento(42955m)));
        Assert.False(MaterialMoResolver.DebeUsarRendimiento(Material(100m, 464m), null));
    }

    // ── MoClpEfectiva ─────────────────────────────────────────────────────────

    [Fact]
    public void MoClpEfectiva_ModoRendimiento_DivideTarifaEntreRendimiento()
    {
        // 42.955 ÷ 464 = 92,58…
        var efectiva = MaterialMoResolver.MoClpEfectiva(Material(100m, 464m), ProyectoRendimiento(42955m));
        Assert.Equal(92.58m, efectiva);
    }

    [Fact]
    public void MoClpEfectiva_ModoDirecto_RetornaMoDirecta()
    {
        var proyecto = new ProjectInfoRow { ModoMoInstalacion = MaterialMoResolver.ModoDirecto, TarifaDhClp = 42955m };
        Assert.Equal(100m, MaterialMoResolver.MoClpEfectiva(Material(100m, 464m), proyecto));
    }

    [Fact]
    public void MoClpEfectiva_SinRendimiento_FallbackADirecto()
    {
        Assert.Equal(250m, MaterialMoResolver.MoClpEfectiva(Material(250m, 0m), ProyectoRendimiento(42955m)));
    }

    [Fact]
    public void MoClpEfectiva_SinTarifa_FallbackADirecto()
    {
        Assert.Equal(250m, MaterialMoResolver.MoClpEfectiva(Material(250m, 464m), ProyectoRendimiento(0m)));
    }

    [Fact]
    public void MoClpEfectiva_MaterialNulo_RetornaCero()
    {
        Assert.Equal(0m, MaterialMoResolver.MoClpEfectiva(null, ProyectoRendimiento(42955m)));
    }

    // ── DerivarTarifaDh ───────────────────────────────────────────────────────

    [Fact]
    public void DerivarTarifaDh_SinLeyes_DivideEntre22()
    {
        Assert.Equal(42955m, MaterialMoResolver.DerivarTarifaDh(945000m, 35m, conLeyes: false));
    }

    [Fact]
    public void DerivarTarifaDh_ConLeyes_MultiplicaPorFactor()
    {
        // 945.000 / 22 × 1,35 = 57.988,6…
        Assert.Equal(57989m, MaterialMoResolver.DerivarTarifaDh(945000m, 35m, conLeyes: true));
    }

    [Fact]
    public void DerivarTarifaDh_SinLeyesPct_ConLeyes_IgnoraFactor()
    {
        Assert.Equal(42955m, MaterialMoResolver.DerivarTarifaDh(945000m, 0m, conLeyes: true));
    }

    [Fact]
    public void DerivarTarifaDh_SueldoInvalido_RetornaCero()
    {
        Assert.Equal(0m, MaterialMoResolver.DerivarTarifaDh(0m, 35m, conLeyes: false));
        Assert.Equal(0m, MaterialMoResolver.DerivarTarifaDh(-1m, 35m, conLeyes: false));
    }

    // ── M.O. por % del material (Material × % × FA) ─────────────────────────

    private static MaterialRow MaterialPorcentaje(decimal matClp, decimal pct) => new()
    {
        Codigo = "X1",
        MatClp = matClp,
        MoClp = 999m, // M.O. fija "basura": debe ignorarse con la fórmula porcentaje
        MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
        MoPorcentajeMaterial = pct
    };

    [Fact]
    public void DebeUsarPorcentaje_FormulaPorcentajeY_PctPositivo_RetornaTrue()
    {
        Assert.True(MaterialMoResolver.DebeUsarPorcentaje(MaterialPorcentaje(1000m, 2.5m)));
    }

    [Fact]
    public void DebeUsarPorcentaje_FormulaVacia_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarPorcentaje(new MaterialRow { MatClp = 1000m }));
    }

    [Fact]
    public void DebeUsarPorcentaje_PctCero_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarPorcentaje(MaterialPorcentaje(1000m, 0m)));
    }

    [Fact]
    public void DebeUsarPorcentaje_MaterialNulo_RetornaFalse()
    {
        Assert.False(MaterialMoResolver.DebeUsarPorcentaje(null));
    }

    [Fact]
    public void MoClpEfectiva_FormulaPorcentaje_MultiplicaMatPctFaDefault()
    {
        // Paridad con la planilla estándar: 1000 × 2,5% × 62,80 (FA default) = 1570
        var proyecto = new ProjectInfoRow(); // modo directo, FA por defecto
        Assert.Equal(1570m, MaterialMoResolver.MoClpEfectiva(MaterialPorcentaje(1000m, 2.5m), proyecto));
    }

    [Fact]
    public void MoClpEfectiva_FormulaPorcentaje_UsaIndiceFaDelProyecto()
    {
        var proyecto = new ProjectInfoRow { IndiceMoFa = 50m };
        Assert.Equal(1250m, MaterialMoResolver.MoClpEfectiva(MaterialPorcentaje(1000m, 2.5m), proyecto));
    }

    [Fact]
    public void MoClpEfectiva_FormulaPorcentaje_SinProyecto_UsaFaDefault()
    {
        Assert.Equal(1570m, MaterialMoResolver.MoClpEfectiva(MaterialPorcentaje(1000m, 2.5m), null));
    }

    [Fact]
    public void MoClpEfectiva_ModoRendimientoActivo_PriorizaRendimientoSobrePorcentaje()
    {
        var material = MaterialPorcentaje(1000m, 2.5m);
        material.RendimientoDh = 464m;
        // 42.955 ÷ 464 = 92,58 — el rendimiento gana al porcentaje (misma precedencia que el motor).
        Assert.Equal(92.58m, MaterialMoResolver.MoClpEfectiva(material, ProyectoRendimiento(42955m)));
    }

    [Fact]
    public void MoClpEfectiva_FormulaPorcentaje_PctCero_FallbackADirecta()
    {
        Assert.Equal(999m, MaterialMoResolver.MoClpEfectiva(MaterialPorcentaje(1000m, 0m), new ProjectInfoRow()));
    }
}
