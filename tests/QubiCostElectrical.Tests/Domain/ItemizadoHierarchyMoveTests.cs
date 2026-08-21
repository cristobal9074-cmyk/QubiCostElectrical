using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ItemizadoHierarchyMoveTests
{
    private static List<ItemizadoRow> SampleHierarchy() =>
    [
        new() { Id = "1.0", Item = "1", Descripcion = "Cap 1", EsTitulo = true },
        new() { Id = "1.0.00001", Item = "1.1", Descripcion = "P1", Unidad = "UN", Cantidad = 1m },
        new() { Id = "1.0.00002", Item = "1.2", Descripcion = "P2", Unidad = "UN", Cantidad = 1m },
        new() { Id = "2.0", Item = "2", Descripcion = "Cap 2", EsTitulo = true },
        new() { Id = "2.0.00001", Item = "2.1", Descripcion = "P3", Unidad = "UN", Cantidad = 1m }
    ];

    [Fact]
    public void Move_title_block_down_swaps_with_next_chapter()
    {
        var items = SampleHierarchy();
        var result = ItemizadoHierarchyMove.Move(items, 0, 1);

        Assert.Equal(2, result.SelectedIndex);
        Assert.Equal("Cap 2", items[0].Descripcion);
        Assert.Equal("Cap 1", items[2].Descripcion);
        Assert.Equal("P1", items[3].Descripcion);
    }

    [Fact]
    public void Move_partida_within_chapter_swaps_siblings()
    {
        var items = SampleHierarchy();
        var result = ItemizadoHierarchyMove.Move(items, 1, 1);

        Assert.Equal(2, result.SelectedIndex);
        Assert.Equal("P2", items[1].Descripcion);
        Assert.Equal("P1", items[2].Descripcion);
    }

    [Fact]
    public void Move_partida_does_not_cross_chapter_boundary()
    {
        var items = SampleHierarchy();
        var result = ItemizadoHierarchyMove.Move(items, 2, 1);

        Assert.Equal(2, result.SelectedIndex);
        Assert.Equal("P2", items[2].Descripcion);
        Assert.Equal("Cap 2", items[3].Descripcion);
    }

    [Fact]
    public void GetTitleBlockLength_counts_title_and_children()
    {
        var items = SampleHierarchy();
        Assert.Equal(3, ItemizadoHierarchyMove.GetTitleBlockLength(items, 0));
        Assert.Equal(2, ItemizadoHierarchyMove.GetTitleBlockLength(items, 3));
    }
}
