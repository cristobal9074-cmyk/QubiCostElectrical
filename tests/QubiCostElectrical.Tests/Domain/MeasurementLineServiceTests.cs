using System.Collections.Generic;
using System.Globalization;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Líneas de medición estilo Presto/Arquímedes: parcial Nº×L×A×H con signo, fórmulas,
/// total por partida y agrupación por zona.
/// </summary>
public sealed class MeasurementLineServiceTests
{
    [Fact]
    public void ComputeParcial_MultipliesNonZeroFactors()
    {
        var line = new MeasurementLineRow { Unidades = 3m, Longitud = 4.5m, Anchura = 0.3m, Altura = 0m };
        // Altura 0 se ignora: 3 × 4.5 × 0.3 = 4.05
        Assert.Equal(4.05m, MeasurementLineService.ComputeParcial(line));
    }

    [Fact]
    public void ComputeParcial_NegativeSign_Deducts()
    {
        var line = new MeasurementLineRow { Signo = -1, Unidades = 2m, Longitud = 1m };
        Assert.Equal(-2m, MeasurementLineService.ComputeParcial(line));
    }

    [Fact]
    public void ComputeParcial_AllZeroFactors_IsZero()
    {
        Assert.Equal(0m, MeasurementLineService.ComputeParcial(new MeasurementLineRow()));
    }

    [Fact]
    public void ComputeParcial_UsesFormulaWhenPresent()
    {
        var line = new MeasurementLineRow { FormulaParcial = "2*3", Unidades = 99m };
        Assert.Equal(6m, MeasurementLineService.ComputeParcial(line));
    }

    [Fact]
    public void ComputeTotal_SumsParcialsWithSign()
    {
        var lines = new[]
        {
            new MeasurementLineRow { Unidades = 3m, Longitud = 4.5m, Anchura = 0.3m }, // 4.05
            new MeasurementLineRow { Signo = -1, Unidades = 2m, Longitud = 1m }        // -2
        };
        Assert.Equal(2.05m, MeasurementLineService.ComputeTotal(lines));
    }

    [Fact]
    public void TotalsByArea_GroupsByZone()
    {
        var lines = new[]
        {
            new MeasurementLineRow { Area = "Piso 1", Unidades = 2m },
            new MeasurementLineRow { Area = "Piso 1", Unidades = 3m },
            new MeasurementLineRow { Area = "Piso 2", Unidades = 10m }
        };

        var totals = MeasurementLineService.TotalsByArea(lines);

        Assert.Equal(5m, totals["Piso 1"]);
        Assert.Equal(10m, totals["Piso 2"]);
    }

    [Fact]
    public void FormatPresto_RendersFactorsAndResult()
    {
        var line = new MeasurementLineRow { Comentario = "Sala", Unidades = 3m, Longitud = 4.5m };
        string text = MeasurementLineService.FormatPresto(line, culture: CultureInfo.InvariantCulture);
        Assert.Equal("Sala  3×4.5 = 13.5", text);
    }
}
