using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

// Ctrl+Izquierda/Derecha entre sectores debe comportarse como Excel: saltar al borde del
// bloque de datos real (row.QtyExpressions), no paginar de a 15 columnas fijas. Cubre también
// el bug encontrado en uso real: el destino de un salto puede caer en la posición 0 de la
// ventana VISIBLE sin ser el sector absoluto 0 — eso no debe disparar el atajo a "Código".
public sealed class CubicacionSectorNavigationTests
{
    [Fact]
    public void DesdeCeldaVacia_SaltaALaProximaConContenido()
    {
        var expressions = new[] { "100", "", "", "", "50", "" };
        int target = CubicacionView.FindSectorDataEdge(expressions, current: 1, direction: 1, max: 300);
        Assert.Equal(4, target);
    }

    [Fact]
    public void DesdeCeldaConContenido_HaciaBloqueContiguo_LlegaAlFinalDelBloque()
    {
        var expressions = new[] { "1", "2", "3", "", "5" };
        int target = CubicacionView.FindSectorDataEdge(expressions, current: 0, direction: 1, max: 300);
        Assert.Equal(2, target);
    }

    [Fact]
    public void SinMasContenidoEnEsaDireccion_VaAlExtremoDelRango()
    {
        var expressions = new[] { "100" };
        int target = CubicacionView.FindSectorDataEdge(expressions, current: 0, direction: 1, max: 300);
        Assert.Equal(299, target);
    }

    // Caso reportado en uso real: fila con datos solo en el slot 0 y en el slot 299 (último).
    // Parado en 299 (con contenido), Ctrl+Izquierda debe aterrizar en el slot 0 (con contenido),
    // NO en el slot intermedio vacío ni saltar al extremo equivocado.
    [Fact]
    public void ConDatosEnAmbosExtremos_DesdeElUltimo_VuelveAlPrimero()
    {
        var expressions = new string[300];
        expressions[0] = "100";
        expressions[299] = "100";

        int target = CubicacionView.FindSectorDataEdge(expressions, current: 299, direction: -1, max: 300);
        Assert.Equal(0, target);
    }

    [Fact]
    public void SinNingunContenido_VaAlExtremoOpuesto()
    {
        var target = CubicacionView.FindSectorDataEdge(expressions: null, current: 150, direction: -1, max: 300);
        Assert.Equal(0, target);

        var targetRight = CubicacionView.FindSectorDataEdge(expressions: null, current: 150, direction: 1, max: 300);
        Assert.Equal(299, targetRight);
    }

    [Fact]
    public void EnElBordeDelRango_NoSeSaleDeLosLimites()
    {
        var expressions = new[] { "1" };
        Assert.Equal(0, CubicacionView.FindSectorDataEdge(expressions, current: 0, direction: -1, max: 300));
        Assert.Equal(299, CubicacionView.FindSectorDataEdge(expressions, current: 299, direction: 1, max: 300));
    }
}
