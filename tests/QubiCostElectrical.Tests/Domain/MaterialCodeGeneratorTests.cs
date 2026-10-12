using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Código automático del alta rápida: el usuario escribe familia y descripción, no el código.
/// Formato PREFIJO-NNN con correlativo libre, sin colisionar con lo que ya existe en el banco.
/// </summary>
public sealed class MaterialCodeGeneratorTests
{
    [Fact]
    public void Siguiente_FamiliaDeUnaPalabra_UsaLasTresPrimerasLetras()
    {
        Assert.Equal("TAB-001", MaterialCodeGenerator.Siguiente("Tableros", "Tablero eléctrico", []));
    }

    [Fact]
    public void Siguiente_FamiliaDeVariasPalabras_TomaInicialesSignificativas()
    {
        // "Canalización" y "Bandejas" → CB; "de" no aporta nada.
        Assert.Equal("CB-001", MaterialCodeGenerator.Siguiente("Canalización de Bandejas", "", []));
    }

    [Fact]
    public void Siguiente_FamiliaConAcento_QuitaAcentoAntesDeArmarElPrefijo()
    {
        Assert.Equal("ELE-001", MaterialCodeGenerator.Siguiente("Eléctricos", "", []));
    }

    [Fact]
    public void Siguiente_YaExisteElMismoPrefijo_ContinuaElCorrelativo()
    {
        var existentes = new[] { "TAB-001", "TAB-002", "TAB-007" };
        Assert.Equal("TAB-008", MaterialCodeGenerator.Siguiente("Tableros", "", existentes));
    }

    [Fact]
    public void Siguiente_CodigoSinGuion_TambienCuenta()
    {
        Assert.Equal("TAB-004", MaterialCodeGenerator.Siguiente("Tableros", "", new[] { "TAB3" }));
    }

    [Fact]
    public void Siguiente_PrefijoDeOtraFamilia_NoAfectaElCorrelativo()
    {
        var existentes = new[] { "CAB-099", "LUM-050" };
        Assert.Equal("TAB-001", MaterialCodeGenerator.Siguiente("Tableros", "", existentes));
    }

    [Fact]
    public void Siguiente_SinFamilia_UsaLaDescripcion()
    {
        Assert.Equal("TE-001", MaterialCodeGenerator.Siguiente("", "Tablero eléctrico", []));
    }

    [Fact]
    public void Siguiente_SinFamiliaNiDescripcion_CaeAlPrefijoDeEmergencia()
    {
        Assert.Equal("MAT-001", MaterialCodeGenerator.Siguiente(null, "   ", []));
    }

    [Fact]
    public void Siguiente_DescripcionQueEmpiezaConNumero_IgnoraLaPalabraYUsaLaSiguiente()
    {
        Assert.Equal("TE-001", MaterialCodeGenerator.Siguiente("", "12 Tablero eléctrico", []));
    }

    [Fact]
    public void Prefijo_SeLimitaATresLetras()
    {
        Assert.Equal("ABC", MaterialCodeGenerator.Prefijo("Alumbrado Bandejas Conductores", ""));
    }

    [Fact]
    public void Prefijo_SoloPalabrasVacias_CaeAlPrefijoDeEmergencia()
    {
        Assert.Equal("MAT", MaterialCodeGenerator.Prefijo("de la para", ""));
    }

    [Fact]
    public void Siguiente_ListaNula_TambienDevuelveElPrimerCodigo()
    {
        Assert.Equal("TAB-001", MaterialCodeGenerator.Siguiente("Tableros", "", null));
    }

    [Fact]
    public void Siguiente_DosVecesSeguidas_NoRepiteCodigo()
    {
        var codigos = new List<string>();
        for (int i = 0; i < 3; i++)
            codigos.Add(MaterialCodeGenerator.Siguiente("Tableros", "", codigos));

        Assert.Equal(new[] { "TAB-001", "TAB-002", "TAB-003" }, codigos);
        Assert.Equal(3, codigos.Distinct().Count());
    }
}
