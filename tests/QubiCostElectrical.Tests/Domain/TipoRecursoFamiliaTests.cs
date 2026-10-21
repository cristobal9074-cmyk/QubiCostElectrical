using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Clasificación automática equipo/material por familia: la app debe separar sola las líneas de
/// tableros y equipos (bloque EQUIPOS de la hoja de costos) y respetar la decisión manual del
/// estimador.
/// </summary>
public sealed class TipoRecursoFamiliaTests
{
    [Theory]
    [InlineData("Tableros", true)]
    [InlineData("TABLERO", true)]
    [InlineData("Gabinetes", true)]
    [InlineData("Grupo electrógeno", true)]
    [InlineData("Grupo generador", true)]
    [InlineData("Transformadores", true)]
    [InlineData("Trafo", true)]
    [InlineData("UPS", true)]
    [InlineData("Celdas", true)]
    [InlineData("Ducto de barra", true)]
    [InlineData("Media tensión", true)]
    [InlineData("Pararrayos", true)]
    [InlineData("Conductores", false)]
    [InlineData("Canalización", false)]
    [InlineData("Malla a tierra", false)]
    [InlineData("Artefactos", false)]
    [InlineData("Iluminación", false)]
    [InlineData("Luminarias", false)]
    [InlineData("Puesta a tierra", false)]
    [InlineData("Protecciones", false)]
    [InlineData("", false)]
    public void Clasificacion_SigueLaTaxonomiaDeFamilias(string familia, bool esEquipo)
        => Assert.Equal(esEquipo, TipoRecursoFamilia.EsFamiliaEquipo(familia));

    [Fact]
    public void Resolver_DevuelveEQParaEquipos_YMATParaElResto()
    {
        Assert.Equal("EQ", TipoRecursoFamilia.Resolver("Tableros"));
        Assert.Equal("EQ", TipoRecursoFamilia.Resolver("Grupo electrógeno"));
        Assert.Equal("MAT", TipoRecursoFamilia.Resolver("Conductores"));
    }
}
