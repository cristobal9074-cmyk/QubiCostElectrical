using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ApuEngineTests
{
    [Fact]
    public void CreateFromItem_UsesSafeQuantityAndCopiesIdentity()
    {
        var engine = new ApuEngine();
        var item = new ItemizadoRow { Id = "I-1", Descripcion = "Canalización PVC", Unidad = "ML", Cantidad = 0m };

        var apu = engine.CreateFromItem(item);

        Assert.Equal("I-1", apu.ItemId);
        Assert.Equal("Canalización PVC", apu.Descripcion);
        Assert.Equal("ML", apu.Unidad);
        Assert.Equal(0m, apu.CantidadPartida);
    }

    [Fact]
    public void Validate_DetectsInvalidResourceAndMissingPrice()
    {
        var engine = new ApuEngine();
        var apu = new ApuRow
        {
            Descripcion = "APU prueba",
            Unidad = "UN",
            Recursos =
            {
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Material", Rendimiento = 0m, PrecioUf = -1m }
            }
        };

        var errors = engine.Validate(apu);

        Assert.Contains(errors, x => x.Contains("rendimiento", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, x => x.Contains("precio", StringComparison.OrdinalIgnoreCase));
    }
}
