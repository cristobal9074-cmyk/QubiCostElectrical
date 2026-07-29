using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionCompositionServiceOrphanTests
{
    private readonly CubicacionCompositionService _service = new();

    [Fact]
    public void EnsureProject_WithPartialNormalizedModel_DecomposesOrphanLegacyRows()
    {
        var data = new ProjectFileData
        {
            Cubicacion = new List<CubicacionRow>
            {
                new()
                {
                    Id = "R1",
                    ItemId = "P1",
                    Codigo = "CABLE",
                    Descripcion = "Cable",
                    Unidad = "ML",
                    Cantidad = 8m,
                    MatUf = 0.1m,
                    MoUf = 0.02m
                },
                new()
                {
                    Id = "R2",
                    ItemId = "P1",
                    Codigo = "TUBO",
                    Descripcion = "Tubo",
                    Unidad = "ML",
                    Cantidad = 4m,
                    MatUf = 0.05m,
                    MoUf = 0.01m
                }
            },
            CubicacionRecursos = new List<CubicacionRecursoRow>
            {
                new() { Id = "R1", ItemId = "P1", Codigo = "CABLE", Descripcion = "Cable", Unidad = "ML", Orden = 0 }
            },
            Zonas = new List<ZonaProyecto>
            {
                new() { Id = "Z1", Nombre = "Piso 1", Orden = 0, Activa = true }
            }
        };

        _service.EnsureProject(data);

        Assert.Equal(2, data.CubicacionRecursos.Count);
        Assert.Contains(data.CubicacionRecursos, r => r.Id == "R2" && r.Codigo == "TUBO");
        Assert.Equal(4m, data.Cubicacion.Single(r => r.Id == "R2").Cantidad);
    }
}
