using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ItemizadoHierarchyRulesTests
{
    [Fact]
    public void ParentIdOf_two_segment_partida_defaults_to_chapter_title()
    {
        Assert.Equal("1.0", ItemizadoHierarchyRules.ParentIdOf("1.1"));
    }

    [Fact]
    public void ParentIdOf_two_segment_partida_respects_legacy_title_id()
    {
        var rows = new[]
        {
            new ItemizadoRow { Id = "1", Item = "1", EsTitulo = true },
            new ItemizadoRow { Id = "1.1", Item = "1.1" }
        };
        Assert.Equal("1", ItemizadoHierarchyRules.ParentIdOf("1.1", rows));
    }

    [Fact]
    public void ParentIdOf_three_segments_drops_last()
    {
        Assert.Equal("1.1", ItemizadoHierarchyRules.ParentIdOf("1.1.001"));
        Assert.Equal("1.1", ItemizadoHierarchyRules.ParentIdOf("1.1.1"));
    }

    [Fact]
    public void NextUniqueDisplayItemCode_avoids_collision()
    {
        var rows = new[]
        {
            new ItemizadoRow { Item = "1,1" },
            new ItemizadoRow { Item = "1,1 (2)" }
        };
        Assert.Equal("1,1 (3)", ItemizadoHierarchyRules.NextUniqueDisplayItemCode(rows, "1,1"));
    }

    [Fact]
    public void NormalizePartidaQuantity_clamps_non_positive()
    {
        Assert.Equal(1m, ItemizadoHierarchyRules.NormalizePartidaQuantity(0m));
        Assert.Equal(1m, ItemizadoHierarchyRules.NormalizePartidaQuantity(-5m));
        Assert.Equal(3m, ItemizadoHierarchyRules.NormalizePartidaQuantity(3m));
    }

    [Fact]
    public void MatchesRemovedId_includes_descendants()
    {
        Assert.True(ItemizadoHierarchyRules.MatchesRemovedId("1.0.00002", "1.0"));
        Assert.False(ItemizadoHierarchyRules.MatchesRemovedId("2.0.00001", "1.0"));
    }
}
