using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public class FamilyPresentationDefaultsTests
{
    [Theory]
    [InlineData("CANALIZADO", "CANALIZACION")]
    [InlineData("Canalizaciones", "CANALIZACION")]
    [InlineData("Bandejas", "CANALIZACION")]
    [InlineData("Conduit", "CANALIZACION")]
    [InlineData("Cables", "CONDUCTORES")]
    [InlineData("Alimentadores", "CONDUCTORES")]
    [InlineData("Malla a tierra", "CONDUCTORES")]
    [InlineData("Tierras", "CONDUCTORES")]
    [InlineData("Generador", "EQUIPOS")]
    [InlineData("Transformador", "EQUIPOS")]
    [InlineData("Luminarias", "ILUMINACION")]
    [InlineData("Disyuntores", "TABLEROS")]
    [InlineData("Enchufes", "ENCHUFES Y TOMACORRIENTES")]
    [InlineData("Cajas", "ACCESORIOS")]
    [InlineData("Terminales", "ACCESORIOS")]
    public void NaturalCanonicalTarget_MapsSynonymsToCanonical(string input, string expectedCanonical)
    {
        var target = FamilyPresentationDefaults.NaturalCanonicalTarget(FamilyPresentationDefaults.Normalize(input));
        Assert.Equal(expectedCanonical, target);
    }

    [Fact]
    public void BudgetSettings_ResolvesCustomProjectFactor_ThroughSynonyms()
    {
        // En un proyecto particular, el usuario configura un factor personalizado (ej: 1.45 para Canalización)
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true
        };
        project.Variables["MarkupFactor_CANALIZACION"] = 1.45m;
        project.Variables["MarkupFactor_EQUIPOS"] = 1.25m;

        var settings = BudgetSettings.FromProject(project);

        // Si la partida se llama "CANALIZADO" o "BANDEJAS", debe tomar el 1.45 configurado por el usuario
        Assert.Equal(1.45m, settings.ResolvePresentationFactor("CANALIZADO"));
        Assert.Equal(1.45m, settings.ResolvePresentationFactor("BANDEJAS"));
        Assert.Equal(1.45m, settings.ResolvePresentationFactor("CANALIZACION"));

        // Si se llama "GENERADOR", debe tomar el 1.25 configurado por el usuario
        Assert.Equal(1.25m, settings.ResolvePresentationFactor("GENERADOR"));
        Assert.Equal(1.25m, settings.ResolvePresentationFactor("EQUIPOS"));
    }

    [Fact]
    public void ProyectoConEstandarPropio_UsaEseValorEnLasFamiliasSinConfigurar()
    {
        // Estandar 1,00 de los proyectos nuevos: la familia sin configurar vale 1,00.
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true,
            MarkupFactorPorDefecto = 1.00m
        };

        var settings = BudgetSettings.FromProject(project);

        Assert.Equal(1.00m, settings.DefaultPresentationFactor);
        Assert.Equal(1.00m, settings.ResolvePresentationFactor("Conductores"));
        Assert.Equal(1.00m, settings.ResolvePresentationFactor("CANALIZADO"));
    }

    [Fact]
    public void ProyectoSinEstandar_ConservaLaTablaHistoricaDelProducto()
    {
        // Los presupuestos ya presupuestados no cambian: sin estandar propio se usa la tabla historica.
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true
        };

        var settings = BudgetSettings.FromProject(project);

        Assert.Equal(1.36m, settings.DefaultPresentationFactor);
        Assert.Equal(1.72m, FamilyPresentationDefaults.DefaultMarkup("CONDUCTORES"));
        Assert.Equal(1.72m, FamilyPresentationDefaults.DefaultMarkup("CONDUCTORES", 0m));
    }

    [Fact]
    public void EstandarPropio_NoPisaUnFactorYaConfigurado()
    {
        var project = new ProjectInfoRow
        {
            PoliticaCostosExcel = true,
            MarkupFactorPorDefecto = 1.00m
        };
        project.Variables["MarkupFactor_CANALIZACION"] = 1.70m;

        var settings = BudgetSettings.FromProject(project);

        Assert.Equal(1.70m, settings.ResolvePresentationFactor("CANALIZACION"));
        Assert.Equal(1.70m, settings.ResolvePresentationFactor("CANALIZADO"));
        Assert.Equal(1.00m, settings.ResolvePresentationFactor("TABLEROS"));
    }
}
