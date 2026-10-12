using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class FactorAjusteMoServiceTests
{
    private static ProjectFileData Proyecto(decimal moTotalClp = 0m) => new()
    {
        Proyecto = new ProjectInfoRow { MoTotalClp = moTotalClp, MoDuracionMeses = 1 }
    };

    // ── M.O. real ─────────────────────────────────────────────────────────────

    [Fact]
    public void ResolverMoRealClp_PrefiereElTotalDelModuloMo()
    {
        var data = Proyecto(moTotalClp: 99_000_000m);
        data.MoDirecta.Add(new MoDirectaRow { Dotacion = 10, SueldoMensualClp = 1_000_000m });

        Assert.Equal(99_000_000m, FactorAjusteMoService.ResolverMoRealClp(data, data.Proyecto));
    }

    [Fact]
    public void ResolverMoRealClp_SinTotal_RecomputaDotacionConLeyes()
    {
        var data = Proyecto();
        data.Proyecto.LeyesSocialesMoPct = 35m;
        data.MoDirecta.Add(new MoDirectaRow { Dotacion = 2, SueldoMensualClp = 1_000_000m, DotacionMensual = [2] });

        // 2 × 1.000.000 × 1,35 = 2.700.000
        Assert.Equal(2_700_000m, FactorAjusteMoService.ResolverMoRealClp(data, data.Proyecto));
    }

    [Fact]
    public void ResolverMoRealClp_SinPlan_DevuelveCero()
    {
        var data = Proyecto();
        Assert.Equal(0m, FactorAjusteMoService.ResolverMoRealClp(data, data.Proyecto));
    }

    // ── Días-hombre (Excel: MANO DE OBRA!U23) ──────────────────────────────────

    /// <summary>
    /// Dotación total 182 personas-mes × 22 jornadas = 4.004 DD/HH: el mismo número que la celda
    /// H192 del Excel original (MANO DE OBRA!U23), con el que se lee la tarifa por día-hombre.
    /// </summary>
    [Fact]
    public void ResolverDiasHombre_SumaDotacionMensualPorJornada()
    {
        var data = Proyecto();
        data.MoDirecta.Add(new MoDirectaRow { Dotacion = 100, DotacionMensual = [100] });
        data.MoDirecta.Add(new MoDirectaRow { Dotacion = 82, DotacionMensual = [82] });

        Assert.Equal(4_004m, FactorAjusteMoService.ResolverDiasHombre(data, data.Proyecto));
    }

    /// <summary>La M.O. indirecta aporta solo su fracción de obra a los días-hombre.</summary>
    [Fact]
    public void ResolverDiasHombre_AplicaFraccionALaMoIndirecta()
    {
        var data = Proyecto();
        data.MoIndirecta.Add(new MoIndirectaRow { Dotacion = 10, FraccionObra = 0.5m, DotacionMensual = [10] });

        // 10 × 0,5 = 5 personas × 22 = 110
        Assert.Equal(110m, FactorAjusteMoService.ResolverDiasHombre(data, data.Proyecto));
    }

    [Fact]
    public void ResolverDiasHombre_SinPlan_DevuelveCero()
    {
        var data = Proyecto();
        Assert.Equal(0m, FactorAjusteMoService.ResolverDiasHombre(data, data.Proyecto));
    }

    // ── Líneas del itemizado ──────────────────────────────────────────────────

    [Fact]
    public void ConstruirLineas_MaterialPorcentaje_AportaSoloBaseSinFa()
    {
        var material = new MaterialRow
        {
            Codigo = "TAB-001",
            MatClp = 100_000m,
            MoClp = 999_999m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        };
        var fila = new CubicacionRow { Codigo = "TAB-001", Cantidad = 4m };
        var data = Proyecto();
        data.Materiales.Add(material);
        data.Cubicacion.Add(fila);

        var linea = Assert.Single(FactorAjusteMoService.ConstruirLineas(data.Cubicacion, data.Materiales, data.Proyecto));

        Assert.Equal(0m, linea.MoFijaClp);
        Assert.Equal(2_500m, linea.BasePorcentajeClp); // 100.000 × 2,5 %
        Assert.Equal(4m, linea.Cantidad);
    }

    [Fact]
    public void ConstruirLineas_MaterialDirecto_AportaMoFijaSinBase()
    {
        var data = Proyecto();
        data.Materiales.Add(new MaterialRow { Codigo = "INT-01", MatClp = 585m, MoClp = 1_170m });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "INT-01", Cantidad = 10m });

        var linea = Assert.Single(FactorAjusteMoService.ConstruirLineas(data.Cubicacion, data.Materiales, data.Proyecto));

        Assert.Equal(1_170m, linea.MoFijaClp);
        Assert.Equal(0m, linea.BasePorcentajeClp);
    }

    [Fact]
    public void ConstruirLineas_ModoRendimiento_UsaTarifaSobreRendimientoYNoElFa()
    {
        var data = Proyecto();
        data.Proyecto.ModoMoInstalacion = MaterialMoResolver.ModoRendimiento;
        data.Proyecto.TarifaDhClp = 42_955m;
        data.Materiales.Add(new MaterialRow
        {
            Codigo = "CON-25",
            MoClp = 999m,
            RendimientoDh = 464m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "CON-25", Cantidad = 100m });

        var linea = Assert.Single(FactorAjusteMoService.ConstruirLineas(data.Cubicacion, data.Materiales, data.Proyecto));

        Assert.Equal(92.58m, linea.MoFijaClp); // 42.955 ÷ 464, el rendimiento manda sobre el %
        Assert.Equal(0m, linea.BasePorcentajeClp);
    }

    [Fact]
    public void ConstruirLineas_FilaSinMaterialUsaSuMoResuelta()
    {
        var data = Proyecto();
        data.Cubicacion.Add(new CubicacionRow { Codigo = "NO-EXISTE", MoClp = 3_000m, Cantidad = 2m });

        var linea = Assert.Single(FactorAjusteMoService.ConstruirLineas(data.Cubicacion, data.Materiales, data.Proyecto));

        Assert.Equal(3_000m, linea.MoFijaClp);
        Assert.Equal(0m, linea.BasePorcentajeClp);
    }

    [Fact]
    public void ConstruirLineas_SinCantidad_NoAportaNada()
    {
        var data = Proyecto();
        data.Materiales.Add(new MaterialRow { Codigo = "X", MoClp = 1_000m });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "X", Cantidad = 0m });

        Assert.Empty(FactorAjusteMoService.ConstruirLineas(data.Cubicacion, data.Materiales, data.Proyecto));
    }

    // ── End to end ────────────────────────────────────────────────────────────

    [Fact]
    public void CalcularParaProyecto_FaDejaLaMoDelItemizadoIgualAlCostoReal()
    {
        var data = Proyecto(moTotalClp: 10_000_000m);
        // Ya cubierta por catálogo: 1.000 × 100 = 100.000
        data.Materiales.Add(new MaterialRow { Codigo = "DIR-01", MoClp = 1_000m });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "DIR-01", Cantidad = 100m });
        // Base de porcentaje: (50.000 × 2,5 % = 1.250) × 100 = 125.000
        data.Materiales.Add(new MaterialRow
        {
            Codigo = "PCT-01",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "PCT-01", Cantidad = 100m });

        var resultado = FactorAjusteMoService.CalcularParaProyecto(data, data.Proyecto);

        // FA = (10.000.000 − 100.000) ÷ 125.000 = 79,20
        Assert.Equal(79.20m, resultado.Fa);

        // Con ese FA, la M.O. del itemizado cuadra con el costo real: 100.000 + 125.000 × 79,20 = 10.000.000
        decimal moItemizado = resultado.MoCubiertaClp + resultado.BasePorcentajeClp * resultado.Fa!.Value;
        Assert.Equal(resultado.MoRealClp, moItemizado);
    }

    [Fact]
    public void CalcularParaProyecto_SinPlanDeMo_NoDerivaFa()
    {
        var data = Proyecto();
        data.Materiales.Add(new MaterialRow
        {
            Codigo = "PCT-01",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        data.Cubicacion.Add(new CubicacionRow { Codigo = "PCT-01", Cantidad = 10m });

        var resultado = FactorAjusteMoService.CalcularParaProyecto(data, data.Proyecto);

        Assert.False(resultado.TienePlanMo);
        Assert.Null(resultado.Fa);
    }
}
