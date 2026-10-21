using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Informe de familias de precio: que factor aplica a cada familia, de donde sale y cuanto
/// representa. Es la garantia de que la pantalla no muestre un numero distinto al que presupuesta
/// el motor, y de que toda familia usada quede a la vista.
/// </summary>
public sealed class PresentacionFamiliasInformeTests
{
    private static CubicacionRow Row(string familia, string codigo, decimal cantidad, decimal mat, decimal mo = 0m)
        => new()
        {
            Id = codigo,
            ItemId = "P1",
            Codigo = codigo,
            Descripcion = codigo,
            Categoria = familia,
            Cantidad = cantidad,
            MatClp = mat,
            MoClp = mo
        };

    private static ProjectInfoRow Proyecto(params (string familia, decimal valor)[] variables)
    {
        var p = new ProjectInfoRow
        {
            UsarFactoresPresentacionFamilia = true,
            PoliticaCostosExcel = true,
            Variables = new Dictionary<string, decimal>()
        };
        foreach (var (familia, valor) in variables)
            p.Variables[$"MarkupFactor_{familia}"] = valor;
        return p;
    }

    [Fact]
    public void FamiliaSinVariable_QuedaSinConfigurar_YElMotorAplicaElDefault()
    {
        var proyecto = Proyecto();
        var cubicacion = new[] { Row("Conductores", "C1", 1m, 1_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        var familia = informe.Familias.Single();
        Assert.Equal(OrigenFactorPresentacion.SinConfigurar, familia.Origen);
        Assert.False(familia.Configurada);
        Assert.Equal(1, informe.FamiliasSinConfigurar);

        var settings = BudgetSettings.FromProject(proyecto);
        Assert.Equal(settings.DefaultPresentationFactor, familia.FactorEfectivo);
    }

    [Fact]
    public void FamiliaConVariable_EsPropia()
    {
        var proyecto = Proyecto(("CONDUCTORES", 1.7m));
        var cubicacion = new[] { Row("Conductores", "C1", 1m, 1_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        var familia = informe.Familias.Single();
        Assert.Equal(OrigenFactorPresentacion.Propio, familia.Origen);
        Assert.True(familia.Configurada);
        Assert.Equal(1.7m, familia.FactorEfectivo);
        Assert.Equal(0, informe.FamiliasSinConfigurar);
    }

    [Fact]
    public void FamiliaAsignada_UsaElFactorDelDestino()
    {
        var proyecto = Proyecto(("CANALIZACION", 1.7m));
        proyecto.FamiliasPrecioAsignadas = new Dictionary<string, string> { ["CANALIZADO"] = "Canalización" };
        var cubicacion = new[] { Row("Canalizado", "P1", 1m, 1_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        var familia = informe.Familias.Single();
        Assert.Equal(OrigenFactorPresentacion.Asignada, familia.Origen);
        Assert.Equal("CANALIZACION", familia.ClaveObjetivo);
        Assert.Equal(1.7m, familia.FactorEfectivo);
    }

    [Fact]
    public void FamiliaAsignada_ConValorPropioInformeQueElPropioSeIgnora()
    {
        var proyecto = Proyecto(("CANALIZACION", 1.7m), ("CANALIZADO", 1.2m));
        proyecto.FamiliasPrecioAsignadas = new Dictionary<string, string> { ["CANALIZADO"] = "Canalización" };
        var cubicacion = new[] { Row("Canalizado", "P1", 1m, 1_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        var familia = informe.Familias.Single();
        Assert.Equal(OrigenFactorPresentacion.Asignada, familia.Origen);
        Assert.True(familia.TieneValorPropioIgnorado);
        Assert.Equal(1.7m, familia.FactorEfectivo);
    }

    [Fact]
    public void LasFilasOrdenanPorImpacto_YLaFamiliaUsadaQuedaEnSuFila()
    {
        var proyecto = Proyecto(("CONDUCTORES", 1.7m), ("CANALIZACION", 1.7m));
        var cubicacion = new[]
        {
            Row("Conductores", "C1", 1m, 5_000m),
            Row("Canalizado", "P1", 1m, 1_000m)
        };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        Assert.Equal(6_000m, informe.CostoTotalClp);
        var canalizacion = informe.Buckets.Single(b => b.Clave == "CANALIZACION");
        Assert.Contains(canalizacion.Familias, f => f.Nombre == "Canalizado");
        Assert.Equal(1_000m, canalizacion.CostoClp);
        Assert.Equal(16.6666666m, canalizacion.SharePct, 4);
        var conductores = informe.Buckets.Single(b => b.Clave == "CONDUCTORES");
        Assert.True(conductores.CostoClp > canalizacion.CostoClp);
        var orden = informe.Buckets.Select(b => b.Clave).ToList();
        Assert.True(orden.IndexOf("CONDUCTORES") < orden.IndexOf("CANALIZACION"));
    }

    [Fact]
    public void FilaPuestaATierraApareceCuandoElProyectoLaUsa()
    {
        var proyecto = Proyecto(("PUESTA A TIERRA", 1.7m));
        var cubicacion = new[] { Row("Malla a tierra", "M1", 1m, 2_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        var fila = informe.Buckets.Single(b => b.Clave == "PUESTA A TIERRA");
        Assert.Contains(fila.Familias, f => f.Nombre == "Malla a tierra");
        Assert.True(fila.Configurada);
        Assert.Equal(1.7m, fila.FactorAplicado);
    }

    [Fact]
    public void SinFactoresPorFamilia_NoArmaInforme()
    {
        var proyecto = new ProjectInfoRow { PoliticaCostosExcel = false, UsarFactoresPresentacionFamilia = false };
        var cubicacion = new[] { Row("Conductores", "C1", 1m, 1_000m) };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);

        Assert.False(informe.UsaFactoresPorFamilia);
        Assert.Empty(informe.Buckets);
        Assert.Empty(informe.Familias);
    }

    [Fact]
    public void ElFactorDelInformeCoincideConElDelMotorParaCadaFamilia()
    {
        var proyecto = Proyecto(("CONDUCTORES", 1.7m), ("CANALIZACION", 1.7m), ("PUESTA A TIERRA", 1.7m));
        proyecto.FamiliasPrecioAsignadas = new Dictionary<string, string> { ["CANALIZADO"] = "Canalización" };
        var cubicacion = new[]
        {
            Row("Conductores", "C1", 1m, 5_000m),
            Row("Canalizado", "P1", 1m, 1_000m),
            Row("Malla a tierra", "M1", 1m, 2_000m)
        };

        var informe = PresentacionFamiliasInforme.Construir(proyecto, cubicacion);
        var settings = BudgetSettings.FromProject(proyecto);

        foreach (var familia in informe.Familias)
        {
            var esperado = settings.TryResolvePresentationFactor(familia.Nombre, out var f) ? f : settings.DefaultPresentationFactor;
            Assert.Equal(esperado, familia.FactorEfectivo);
        }
    }
}
