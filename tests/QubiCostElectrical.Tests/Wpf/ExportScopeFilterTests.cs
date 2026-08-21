using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ExportScopeServiceTests
{
    [Fact]
    public void ApplyScope_PartidaKeepsHierarchyAndUsedMaterials()
    {
        var data = new ProjectFileData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "Título 1", EsTitulo = true },
                new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida 1" },
                new ItemizadoRow { Id = "P2", Item = "1.2", ParentId = "T1", Descripcion = "Partida 2" }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Descripcion = "Cable", Area = "Norte" },
                new CubicacionRow { ItemId = "P2", Codigo = "MAT02", Descripcion = "Tubería", Area = "Sur" }
            ],
            Materiales =
            [
                new MaterialRow { Codigo = "MAT01", Descripcion = "Cable", Unidad = "M", MatUf = 10m },
                new MaterialRow { Codigo = "MAT02", Descripcion = "Tubería", Unidad = "M", MatUf = 20m }
            ],
            Apus =
            [
                new ApuRow
                {
                    ItemId = "P1",
                    Recursos =
                    [
                        new ApuRecursoRow { CodigoMaterial = "MAT01", Descripcion = "Cable", Unidad = "M" }
                    ]
                }
            ],
            QtyHeadersByItem = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["P1"] = ["Zona 1"],
                ["P2"] = ["Zona 2"]
            }
        };

        var scoped = ExportScopeService.ApplyScope(data, ExportScopeMode.Partida, "P1", null);

        Assert.Equal(["T1", "P1"], scoped.Itemizado.Select(x => x.Id).ToArray());
        Assert.Single(scoped.Cubicacion);
        Assert.Equal("P1", scoped.Cubicacion[0].ItemId);
        Assert.Single(scoped.Materiales);
        Assert.Equal("MAT01", scoped.Materiales[0].Codigo);
        Assert.True(scoped.QtyHeadersByItem.ContainsKey("P1"));
        Assert.False(scoped.QtyHeadersByItem.ContainsKey("P2"));
    }

    [Fact]
    public void ApplyScope_AreaKeepsMatchingItemsAndAncestors()
    {
        var data = new ProjectFileData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "Título 1", EsTitulo = true },
                new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida 1" },
                new ItemizadoRow { Id = "P2", Item = "1.2", ParentId = "T1", Descripcion = "Partida 2" }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Descripcion = "Cable", Area = "Norte" },
                new CubicacionRow { ItemId = "P2", Codigo = "MAT02", Descripcion = "Tubería", Area = "Sur" }
            ],
            Materiales =
            [
                new MaterialRow { Codigo = "MAT01", Descripcion = "Cable", Unidad = "M", MatUf = 10m },
                new MaterialRow { Codigo = "MAT02", Descripcion = "Tubería", Unidad = "M", MatUf = 20m }
            ]
        };

        var scoped = ExportScopeService.ApplyScope(data, ExportScopeMode.Area, null, "Sur");

        Assert.Equal(["T1", "P2"], scoped.Itemizado.Select(x => x.Id).ToArray());
        Assert.Single(scoped.Cubicacion);
        Assert.Equal("P2", scoped.Cubicacion[0].ItemId);
        Assert.Single(scoped.Materiales);
        Assert.Equal("MAT02", scoped.Materiales[0].Codigo);
    }

    [Fact]
    public void BuildAreaOptions_ReturnsDistinctSortedAreas()
    {
        var data = new ProjectFileData
        {
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Area = "Sur" },
                new CubicacionRow { ItemId = "P2", Codigo = "MAT02", Area = "Norte" },
                new CubicacionRow { ItemId = "P3", Codigo = "MAT03", Area = "sur" }
            ]
        };

        var areas = ExportScopeService.BuildAreaOptions(data);

        Assert.Equal(["Norte", "Sur"], areas);
    }
}
