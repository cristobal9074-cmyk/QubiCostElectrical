using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ElectricalFamilyCatalogTests
{
    [Fact]
    public void FamiliasEstandar_ContieneFamiliasSolicitadas()
    {
        var estandar = ElectricalFamilyCatalog.FamiliasEstandar;

        Assert.Contains("Transformadores", estandar);
        Assert.Contains("Grupo electrógeno", estandar);
        Assert.Contains("Ducto de barra", estandar);
        Assert.Contains("Celdas", estandar);
        Assert.Contains("UPS", estandar);
        Assert.Contains("Pararrayos", estandar);
        Assert.Contains("Obras civiles", estandar);
        Assert.Contains("Sistema de remarcación", estandar);
        Assert.Contains("Documentación y trámites", estandar);
        Assert.Contains("Media tensión", estandar);
        Assert.Contains("Conductores", estandar);
        Assert.Contains("Tableros", estandar);
        Assert.Contains("Canalización", estandar);
    }

    [Fact]
    public void CombinarFamilias_SinFuentesExternas_DevuelveFamiliasEstandar()
    {
        var resultado = ElectricalFamilyCatalog.CombinarFamilias();

        Assert.NotEmpty(resultado);
        Assert.Contains("Transformadores", resultado);
        Assert.Contains("Grupo electrógeno", resultado);
        Assert.Contains("Ducto de barra", resultado);
        Assert.Contains("Media tensión", resultado);
    }

    [Fact]
    public void CombinarFamilias_ListaExternaConVarianteMayusculas_NoDuplicaYRespetaListaExterna()
    {
        var externa = new[] { "TRANSFORMADORES", "Ducto Barra" };

        var resultado = ElectricalFamilyCatalog.CombinarFamilias(externa);

        // No debe aparecer dos veces
        var countTransformadores = resultado.Count(f =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(f), "TRANSFORMADORES", System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, countTransformadores);

        // Debe respetar la forma de la lista externa
        Assert.Contains("TRANSFORMADORES", resultado);
    }

    [Fact]
    public void CombinarFamilias_SinonimoGeneradorYGrupoElectrogeno_NoDuplica()
    {
        var externa = new[] { "Generadores" };

        var resultado = ElectricalFamilyCatalog.CombinarFamilias(externa);

        // "Generadores" de la lista externa debe absorber "Grupo electrógeno" para no chocar
        var count = resultado.Count(f =>
            string.Equals(ElectricalFamilyCatalog.ClaveCanonica(f), "GRUPO ELECTROGENO", System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, count);
        Assert.Contains("Generadores", resultado);
    }

    [Fact]
    public void CombinarFamilias_UsuarioCreaNuevaFamilia_ApareceSinPerderLasEstandar()
    {
        var externa = new[] { "Control y Automatización", "Celdas de Media Tensión" };

        var resultado = ElectricalFamilyCatalog.CombinarFamilias(externa);

        Assert.Contains("Control y Automatización", resultado);
        Assert.Contains("Celdas de Media Tensión", resultado);
        Assert.DoesNotContain("Celdas", resultado);
        Assert.Contains("Transformadores", resultado);
        Assert.Contains("Grupo electrógeno", resultado);
    }

    [Fact]
    public void CombinarFamilias_TramitesDelExcel_NoDuplicaLaFamiliaInterna()
    {
        var externa = new[] { "TRAMITES CIA ELECTRICA , SEC Y PLANOS AS-BUILT", "Ducto de barra", "Pararrayos" };

        var resultado = ElectricalFamilyCatalog.CombinarFamilias(externa);

        Assert.Equal(1, resultado.Count(f => ElectricalFamilyCatalog.ClaveCanonica(f) == "DOCUMENTACION Y TRAMITES"));
        Assert.Contains("TRAMITES CIA ELECTRICA , SEC Y PLANOS AS-BUILT", resultado);
        Assert.DoesNotContain("Documentación y trámites", resultado);
        Assert.Equal(1, resultado.Count(f => ElectricalFamilyCatalog.ClaveCanonica(f) == "DUCTO BARRA"));
        Assert.Contains("Ducto de barra", resultado);
    }

    [Theory]
    [InlineData("Transformadores", "TRA-001")]
    [InlineData("Grupo Electrógeno", "GE-001")]
    [InlineData("Generadores", "GEN-001")]
    [InlineData("Ducto Barra", "DB-001")]
    [InlineData("Media Tensión", "MT-001")]
    public void MaterialCodeGenerator_GeneraPrefijosEsperados(string familia, string codigoEsperado)
    {
        string codigo = MaterialCodeGenerator.Siguiente(familia, "", []);
        Assert.Equal(codigoEsperado, codigo);
    }

    [Theory]
    [InlineData("TRANSFORMADORES", "EQUIPOS", 1.43)]
    [InlineData("Transformadores", "EQUIPOS", 1.43)]
    [InlineData("GRUPO ELECTROGENO", "EQUIPOS", 1.43)]
    [InlineData("Generadores", "EQUIPOS", 1.43)]
    [InlineData("DUCTO BARRA", "EQUIPOS", 1.43)]
    [InlineData("Ducto de barra", "EQUIPOS", 1.43)]
    [InlineData("Celdas de media tensión", "EQUIPOS", 1.43)]
    [InlineData("UPS", "EQUIPOS", 1.43)]
    [InlineData("Pararrayos", "EQUIPOS", 1.43)]
    [InlineData("Sistema de remarcación", "EQUIPOS", 1.43)]
    [InlineData("Obras civiles", "CONDUCTORES", 1.72)]
    [InlineData("TRAMITES CIA ELECTRICA , SEC Y PLANOS AS-BUILT", "ACCESORIOS", 1.36)]
    [InlineData("Media Tensión", "EQUIPOS", 1.43)]
    public void FamilyPresentationDefaults_MapeoNaturalCorrecto(string familia, string esperadoCanonica, double markupEsperado)
    {
        string norm = FamilyPresentationDefaults.Normalize(familia);
        string canonica = FamilyPresentationDefaults.NaturalCanonicalTarget(norm);
        Assert.Equal(esperadoCanonica, canonica);

        decimal markup = FamilyPresentationDefaults.DefaultMarkup(norm);
        Assert.Equal((decimal)markupEsperado, markup);
    }

    [Fact]
    public void FamilyPresentationDefaults_FollowAssignment_RespetaAsignacionExplicita()
    {
        var asignaciones = new Dictionary<string, string>
        {
            ["TRANSFORMADORES"] = "Tableros"
        };

        string resultado = FamilyPresentationDefaults.FollowAssignment(asignaciones, "Transformadores");

        Assert.Equal("TABLEROS", resultado);
    }

    [Fact]
    public void FamilyPresentationDefaults_FollowAssignment_SinAsignacionExplicita_CaeEnObjetivoNatural()
    {
        var asignaciones = new Dictionary<string, string>();

        string resultado = FamilyPresentationDefaults.FollowAssignment(asignaciones, "Transformadores");

        Assert.Equal("EQUIPOS", resultado);
    }
}
