using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Contrato CLP: el motor no debe tratar un UF real (fracción) como pesos.
/// </summary>
public sealed class PriceClpFallbackTests
{
    [Fact]
    public void MoClpConFallback_PrefersMoClp()
    {
        var row = new CubicacionRow { MoClp = 15_000m, MoUf = 0.38m };
        Assert.Equal(15_000m, row.MoClpConFallback);
    }

    [Fact]
    public void MoClpConFallback_DoesNotTreatRealUfAsPesos()
    {
        var row = new CubicacionRow { MoClp = 0m, MoUf = 0.38m };
        Assert.Equal(0m, row.MoClpConFallback);
    }

    [Fact]
    public void MoClpConFallback_UsesContaminatedUfColumnAsClp()
    {
        var row = new CubicacionRow { MoClp = 0m, MoUf = 15_000m };
        Assert.Equal(15_000m, row.MoClpConFallback);
    }

    [Fact]
    public void MatClpConFallback_DoesNotTreatSmallUfAsPesos()
    {
        var row = new CubicacionRow { MatClp = 0m, MatUf = 1.2m };
        Assert.Equal(0m, row.MatClpConFallback);
    }

    [Fact]
    public void LegacyUfFieldToClp_MoEnteroUno_EsClpNoMultiplicaValorUf()
    {
        // Regresión: MO=1 en el banco + UF=40500 no debe producir MO=40500 en cubicación.
        Assert.Equal(1m, PriceConversion.LegacyUfFieldToClp(1m, 40500m, isMo: true));
    }

    [Fact]
    public void LegacyUfFieldToClp_MoFraccion_ConvierteUfReal()
    {
        Assert.Equal(15390m, PriceConversion.LegacyUfFieldToClp(0.38m, 40500m, isMo: true));
    }

    [Fact]
    public void LegacyUfFieldToClp_MatEntero_EsClpDualWrite()
    {
        Assert.Equal(273m, PriceConversion.LegacyUfFieldToClp(273m, 40500m, isMo: false));
    }

    [Fact]
    public void EnsureCubicacionClp_ConMonedaUf_NoMultiplicaMoUfUno()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { MonedaPresupuesto = "UF", ValorUfClp = 40500m },
            Cubicacion =
            {
                new CubicacionRow { Id = "R1", Codigo = "H07", MatClp = 273m, MoClp = 1m, MatUf = 1m, MoUf = 1m }
            },
            CubicacionValorizaciones =
            {
                new CubicacionValorizacionRow { RecursoId = "R1", MatClp = 273m, MoClp = 1m, MatUf = 1m, MoUf = 1m }
            }
        };

        ProjectSnapshotIntegrity.Ensure(data);

        Assert.Equal(273m, data.Cubicacion[0].MatClp);
        Assert.Equal(1m, data.Cubicacion[0].MoClp);
        Assert.Equal(1m, data.CubicacionValorizaciones[0].MoClp);
        Assert.True(data.Cubicacion[0].MoUf < 1m); // derivado, no 40500
    }

    [Fact]
    public void BudgetEngine_SumsMoClpTimesQuantity()
    {
        var engine = new BudgetEngine();
        var rows = new[]
        {
            new CubicacionRow { Codigo = "A", Tipo = "MAT", Cantidad = 10m, MatClp = 1_000m, MoClp = 500m },
            new CubicacionRow { Codigo = "B", Tipo = "MAT", Cantidad = 2m, MatClp = 0m, MoClp = 3_000m }
        };

        var result = engine.CalculateDirectCosts(rows);

        Assert.Equal(10_000m, result.MaterialesUf);
        Assert.Equal(5_000m + 6_000m, result.ManoObraUf);
    }
}
