using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// ApuService: punto de entrada de la Aplicación para creación y validación de APU.
/// </summary>
public sealed class ApuServiceTests
{
    private static readonly ApuService Service = new();

    [Fact]
    public void CrearDesdeItem_DelegatesToEngine_CarryingItemFields()
    {
        var item = new ItemizadoRow { Id = "1.1", Descripcion = "Circuito", Unidad = "ml" };

        var apu = Service.CrearDesdeItem(item);

        Assert.Equal("1.1", apu.ItemId);
        Assert.Equal("Circuito", apu.Descripcion);
        Assert.Equal("ml", apu.Unidad);
    }

    [Fact]
    public void Validar_ReturnsEngineValidationMessages()
    {
        var apu = new ApuRow { Recursos = new List<ApuRecursoRow>() };

        var errores = Service.Validar(apu);

        Assert.NotEmpty(errores);
    }
}
