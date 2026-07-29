using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionMutationServiceTests
{
    private readonly CubicacionMutationService _service = new();

    private static ProjectFileData ProjectWithNormalizedLayers()
    {
        var data = new ProjectFileData
        {
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "P1", Item = "1.1", Descripcion = "Partida A" },
                new() { Id = "P2", Item = "1.2", Descripcion = "Partida B" }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "R1", ItemId = "P1", Codigo = "A1", Descripcion = "Recurso A", Cantidad = 2m },
                new() { Id = "R2", ItemId = "P2", Codigo = "B1", Descripcion = "Recurso B", Cantidad = 3m }
            },
            CubicacionRecursos = new List<CubicacionRecursoRow>
            {
                new() { Id = "R1", ItemId = "P1", Codigo = "A1", Descripcion = "Recurso A" },
                new() { Id = "R2", ItemId = "P2", Codigo = "B1", Descripcion = "Recurso B" }
            },
            CubicacionZonaCantidades = new List<CubicacionZonaCantidadRow>
            {
                new() { RecursoId = "R1", ItemId = "P1", ZonaOffset = 0, Cantidad = 2m, Expresion = "2" },
                new() { RecursoId = "R2", ItemId = "P2", ZonaOffset = 0, Cantidad = 3m, Expresion = "3" }
            },
            CubicacionValorizaciones = new List<CubicacionValorizacionRow>
            {
                new() { Id = "V1", RecursoId = "R1", ItemId = "P1", MatUf = 1m },
                new() { Id = "V2", RecursoId = "R2", ItemId = "P2", MatUf = 2m }
            },
            Mediciones = new List<MeasurementLineRow>
            {
                new() { ItemId = "P1", RecursoId = "R1", CubicacionRowId = "R1", ZonaOffset = 0, Longitud = 2m },
                new() { ItemId = "P2", RecursoId = "R2", CubicacionRowId = "R2", ZonaOffset = 0, Longitud = 3m }
            },
            Proyecto = new ProjectInfoRow
            {
                CubicacionCellComments = new List<CubicacionCellCommentRow>
                {
                    new() { CubicacionRowId = "R1", Comment = "Nota A" },
                    new() { CubicacionRowId = "R2", Comment = "Nota B" }
                }
            }
        };
        return data;
    }

    [Fact]
    public void RemoveRowsByIds_ClearsAllLayersForRow()
    {
        var data = ProjectWithNormalizedLayers();

        _service.RemoveRowsByIds(data, ["R1"]);

        Assert.DoesNotContain(data.Cubicacion, r => r.Id == "R1");
        Assert.DoesNotContain(data.CubicacionRecursos, r => r.Id == "R1");
        Assert.DoesNotContain(data.CubicacionZonaCantidades, z => z.RecursoId == "R1");
        Assert.DoesNotContain(data.CubicacionValorizaciones, v => v.RecursoId == "R1");
        Assert.DoesNotContain(data.Mediciones, m => m.RecursoId == "R1");
        Assert.DoesNotContain(data.Proyecto.CubicacionCellComments, c => c.CubicacionRowId == "R1");

        Assert.Single(data.Cubicacion);
        Assert.Equal("R2", data.Cubicacion[0].Id);
    }

    [Fact]
    public void RemoveForItemIds_KeepsOtherPartidasIntact()
    {
        var data = ProjectWithNormalizedLayers();

        _service.RemoveForItemIds(data, ["P1"]);

        Assert.DoesNotContain(data.Cubicacion, r => r.ItemId == "P1");
        Assert.DoesNotContain(data.CubicacionRecursos, r => r.ItemId == "P1");
        Assert.DoesNotContain(data.CubicacionZonaCantidades, z => z.ItemId == "P1");
        Assert.DoesNotContain(data.CubicacionValorizaciones, v => v.ItemId == "P1");
        Assert.DoesNotContain(data.Mediciones, m => m.ItemId == "P1");

        Assert.Single(data.Cubicacion);
        Assert.Equal("P2", data.Cubicacion[0].ItemId);
        Assert.Single(data.CubicacionRecursos);
        Assert.Equal("R2", data.CubicacionRecursos[0].Id);
    }
}
