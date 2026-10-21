using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Corrección de la clasificación de equipos: las líneas de familia Tableros/Equipos que
/// quedaron como material se detectan y se corrigen; las que el estimador marcó a mano se
/// respetan.
/// </summary>
public sealed class ClasificacionEquiposServiceTests
{
    private static CubicacionRow Row(string codigo, string familia, string tipo, bool manual = false)
        => new()
        {
            Id = codigo,
            ItemId = "P1",
            Codigo = codigo,
            Categoria = familia,
            Tipo = tipo,
            TipoManual = manual,
            Cantidad = 1m,
            MatClp = 1_000m
        };

    [Fact]
    public void Detectar_EncuentraLasLineasDeEquipoQueQuedaronComoMaterial()
    {
        var filas = new List<CubicacionRow>
        {
            Row("TAB-001", "Tableros", "MAT"),
            Row("GE-001", "Grupo electrógeno", "MAT"),
            Row("CAB-001", "Conductores", "MAT")
        };

        var detectadas = new ClasificacionEquiposService().Detectar(filas);

        Assert.Equal(2, detectadas.Count);
        Assert.Contains(detectadas, r => r.Codigo == "TAB-001");
        Assert.Contains(detectadas, r => r.Codigo == "GE-001");
        Assert.DoesNotContain(detectadas, r => r.Codigo == "CAB-001");
    }

    [Fact]
    public void Detectar_RespetaElTipoFijoAMano()
    {
        var filas = new List<CubicacionRow> { Row("TAB-001", "Tableros", "MAT", manual: true) };

        Assert.Empty(new ClasificacionEquiposService().Detectar(filas));
    }

    [Fact]
    public void Detectar_NoConfundeLineasDeManoDeObraConEquipos()
    {
        var filas = new List<CubicacionRow> { Row("MO-TAB", "Tableros", "MO") };

        Assert.Empty(new ClasificacionEquiposService().Detectar(filas));
    }

    [Fact]
    public void Corregir_MarcaComoEquipoYDejaLaLineaEnLaFamiliaCorrecta()
    {
        var filas = new List<CubicacionRow>
        {
            Row("TAB-001", "Tableros", "MAT"),
            Row("CAB-001", "Conductores", "MAT")
        };

        var corregidas = new ClasificacionEquiposService().Corregir(filas);

        Assert.Equal(1, corregidas);
        Assert.Equal("EQ", filas[0].Tipo);
        Assert.False(filas[0].TipoManual);
        Assert.Equal("MAT", filas[1].Tipo);
    }
}
