using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoGridValidationTests
{
    [Fact]
    public void TryApplyVisibleItemEdit_partida_updates_parent_id_from_chapter()
    {
        var rows = new List<ItemizadoRow>
        {
            new() { Id = "1.0", Item = "1,0", Descripcion = "Cap", EsTitulo = true },
            new() { Id = "1.0.00001", Item = "1,1", Descripcion = "P1", EsTitulo = false, ParentId = "1.0" }
        };
        var partida = rows[1];

        var result = ItemizadoGridValidation.TryApplyVisibleItemEdit(partida, "1,2", rows);

        Assert.True(result.Success);
        Assert.Equal("1,2", partida.Item);
        Assert.Equal("1.2", partida.WBSCode);
        Assert.Equal("1.0", partida.ParentId);
        Assert.Equal("1.0.00001", partida.Id);
    }

    [Fact]
    public void TryApplyVisibleItemEdit_partida_rejects_duplicate_code()
    {
        var rows = new List<ItemizadoRow>
        {
            new() { Id = "1.0.00001", Item = "1,1", Descripcion = "A", EsTitulo = false },
            new() { Id = "1.0.00002", Item = "1,2", Descripcion = "B", EsTitulo = false }
        };

        var result = ItemizadoGridValidation.TryApplyVisibleItemEdit(rows[1], "1,1", rows);

        Assert.False(result.Success);
        Assert.Contains("ya existe", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("1,2", rows[1].Item);
    }

    [Fact]
    public void TryApplyVisibleItemEdit_title_rejects_chapter_change()
    {
        var title = new ItemizadoRow
        {
            Id = "1.0",
            Item = "1,0",
            Descripcion = "Capítulo",
            EsTitulo = true
        };

        var result = ItemizadoGridValidation.TryApplyVisibleItemEdit(title, "2,0", new[] { title });

        Assert.False(result.Success);
        Assert.Equal("1,0", title.Item);
    }

    [Fact]
    public void TryApplyVisibleItemEdit_title_allows_format_variant()
    {
        var title = new ItemizadoRow
        {
            Id = "1.0",
            Item = "1,0",
            Descripcion = "Capítulo",
            EsTitulo = true
        };

        var result = ItemizadoGridValidation.TryApplyVisibleItemEdit(title, "1.0", new[] { title });

        Assert.True(result.Success);
        Assert.Equal("1.0", title.Item);
        Assert.Equal("1.0", title.WBSCode);
    }
}
