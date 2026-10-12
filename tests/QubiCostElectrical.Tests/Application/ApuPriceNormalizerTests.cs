using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ApuPriceNormalizerTests
{
    private static ProjectFileData DataWithMaterial(decimal matClp, decimal valorUfClp)
    {
        decimal matUf = PriceConversion.ClpToUf(matClp, valorUfClp);
        return new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { ValorUfClp = valorUfClp },
            Materiales = new List<MaterialRow>
            {
                new()
                {
                    Codigo = "CBL-25",
                    Descripcion = "Cable 2.5 mm2",
                    Unidad = "m",
                    MatClp = matClp,
                    MatUf = matUf
                }
            }
        };
    }

    private static ApuRow ApuWithResource(string codigo, decimal precio) => new()
    {
        Descripcion = "APU de prueba",
        Unidad = "un",
        Recursos = new List<ApuRecursoRow>
        {
            new()
            {
                Tipo = ApuTipoRecurso.MAT,
                CodigoMaterial = codigo,
                Descripcion = "Cable",
                Unidad = "m",
                Rendimiento = 1m,
                PrecioUf = precio
            }
        }
    };

    [Fact]
    public void Normalize_WhenPriceMatchesMaterialUf_ConvertsToCatalogClp()
    {
        var data = DataWithMaterial(matClp: 1250m, valorUfClp: 38000m);
        data.Apus.Add(ApuWithResource("CBL-25", PriceConversion.ClpToUf(1250m, 38000m)));

        int reparados = ApuPriceNormalizer.Normalize(data);

        Assert.Equal(1, reparados);
        Assert.Equal(1250m, data.Apus[0].Recursos[0].PrecioUf);
    }

    [Fact]
    public void Normalize_WhenPriceAlreadyClp_LeavesItUntouched()
    {
        var data = DataWithMaterial(matClp: 1250m, valorUfClp: 38000m);
        data.Apus.Add(ApuWithResource("CBL-25", 1250m));

        int reparados = ApuPriceNormalizer.Normalize(data);

        Assert.Equal(0, reparados);
        Assert.Equal(1250m, data.Apus[0].Recursos[0].PrecioUf);
    }

    [Fact]
    public void Normalize_WhenMaterialMissingAndPriceBelowOnePeso_TreatsAsUf()
    {
        var data = new ProjectFileData { Proyecto = new ProjectInfoRow { ValorUfClp = 38000m } };
        data.Apus.Add(ApuWithResource("SIN-CATALOGO", 0.05m));

        int reparados = ApuPriceNormalizer.Normalize(data);

        Assert.Equal(1, reparados);
        Assert.Equal(1900m, data.Apus[0].Recursos[0].PrecioUf);
    }

    [Fact]
    public void Normalize_WhenMaterialMissingAndPriceIsPlausibleClp_DoesNotGuess()
    {
        var data = new ProjectFileData { Proyecto = new ProjectInfoRow { ValorUfClp = 38000m } };
        data.Apus.Add(ApuWithResource("SIN-CATALOGO", 850m));

        int reparados = ApuPriceNormalizer.Normalize(data);

        Assert.Equal(0, reparados);
        Assert.Equal(850m, data.Apus[0].Recursos[0].PrecioUf);
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var data = DataWithMaterial(matClp: 1250m, valorUfClp: 38000m);
        data.Apus.Add(ApuWithResource("CBL-25", PriceConversion.ClpToUf(1250m, 38000m)));
        data.Apus.Add(ApuWithResource("SIN-CATALOGO", 0.05m));

        int first = ApuPriceNormalizer.Normalize(data);
        int second = ApuPriceNormalizer.Normalize(data);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(1250m, data.Apus[0].Recursos[0].PrecioUf);
        Assert.Equal(1900m, data.Apus[1].Recursos[0].PrecioUf);
    }
}
