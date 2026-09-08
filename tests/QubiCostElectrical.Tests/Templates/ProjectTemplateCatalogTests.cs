using System;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Templates;

public sealed class ProjectTemplateCatalogTests
{
    [Fact]
    public void Catalog_ContainsAllExpectedChileanTemplates()
    {
        var templates = ProjectTemplateCatalog.All;

        Assert.NotNull(templates);
        Assert.True(templates.Count >= 5);

        Assert.Contains(templates, t => t.Id == "blank" && t.EsEnBlanco);
        Assert.Contains(templates, t => t.Id == "residential_building" && t.NormaSec.Contains("RIC"));
        Assert.Contains(templates, t => t.Id == "industrial_warehouse" && t.NormaSec.Contains("RIC"));
        Assert.Contains(templates, t => t.Id == "solar_pv_plant" && t.NormaSec.Contains("RIC N° 19"));
        Assert.Contains(templates, t => t.Id == "commercial_retail" && t.NormaSec.Contains("RIC"));
    }

    [Fact]
    public void ResidentialBuildingTemplate_GeneratesValidStructuredRows()
    {
        var template = ProjectTemplateCatalog.All.First(t => t.Id == "residential_building");
        var rows = template.CreateInstanceRows();

        Assert.NotEmpty(rows);
        Assert.True(rows.Count > 15);

        // Cada fila debe tener PartidaId único
        var guids = rows.Select(r => r.PartidaId).Distinct().ToList();
        Assert.Equal(rows.Count, guids.Count);

        // Debe haber capítulos y partidas
        var titulos = rows.Where(r => r.EsTitulo).ToList();
        var partidas = rows.Where(r => !r.EsTitulo).ToList();
        Assert.True(titulos.Count >= 5);
        Assert.True(partidas.Count >= 10);

        // Todas las partidas deben tener un ParentId no vacío y cantidades válidas
        foreach (var p in partidas)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.ParentId), $"Partida {p.Item} debe tener ParentId");
            Assert.True(p.CantidadOriginal > 0, $"Partida {p.Item} debe tener cantidad > 0");
            Assert.False(string.IsNullOrWhiteSpace(p.Descripcion), $"Partida {p.Item} debe tener descripción");
        }
    }

    [Fact]
    public void SolarPvPlantTemplate_FollowsRic19Standards()
    {
        var template = ProjectTemplateCatalog.All.First(t => t.Id == "solar_pv_plant");
        var rows = template.CreateInstanceRows();

        Assert.Contains(rows, r => r.Descripcion.Contains("fotovoltaico", StringComparison.OrdinalIgnoreCase) ||
                                  r.Descripcion.Contains("solar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rows, r => r.Descripcion.Contains("TE4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BlankTemplate_ReturnsEmptyRows()
    {
        var template = ProjectTemplateCatalog.All.First(t => t.Id == "blank");
        var rows = template.CreateInstanceRows();

        Assert.Empty(rows);
    }
}
