using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class CubicacionSectorNavigationTests
{
    [Fact]
    public void DesdeCeldaVacia_SaltaALaProximaConContenido()
    {
        var expressions = new[] { "100", "", "", "", "50", "" };
        Assert.Equal(4, CubicacionSectorNavigation.FindDataEdge(expressions, 1, 1, 300));
    }

    [Fact]
    public void DesdeCeldaConContenido_HaciaBloqueContiguo_LlegaAlFinalDelBloque()
    {
        var expressions = new[] { "1", "2", "3", "", "5" };
        Assert.Equal(2, CubicacionSectorNavigation.FindDataEdge(expressions, 0, 1, 300));
    }

    [Fact]
    public void SinMasContenidoEnEsaDireccion_VaAlExtremoDelRango()
    {
        var expressions = new[] { "100" };
        Assert.Equal(299, CubicacionSectorNavigation.FindDataEdge(expressions, 0, 1, 300));
    }

    [Fact]
    public void ConDatosEnAmbosExtremos_DesdeElUltimo_VuelveAlPrimero()
    {
        var expressions = new string[300];
        expressions[0] = "100";
        expressions[299] = "100";
        Assert.Equal(0, CubicacionSectorNavigation.FindDataEdge(expressions, 299, -1, 300));
    }

    [Fact]
    public void SinNingunContenido_VaAlExtremoOpuesto()
    {
        Assert.Equal(0, CubicacionSectorNavigation.FindDataEdge(null, 150, -1, 300));
        Assert.Equal(299, CubicacionSectorNavigation.FindDataEdge(null, 150, 1, 300));
    }

    [Fact]
    public void EnElBordeDelRango_NoSeSaleDeLosLimites()
    {
        var expressions = new[] { "1" };
        Assert.Equal(0, CubicacionSectorNavigation.FindDataEdge(expressions, 0, -1, 300));
        Assert.Equal(299, CubicacionSectorNavigation.FindDataEdge(expressions, 299, 1, 300));
    }
}
