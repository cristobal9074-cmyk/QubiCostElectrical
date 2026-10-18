using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoServiceTests
{
    private readonly ItemizadoService _service = new();

    private static ItemizadoRow Item(string id, string item, string desc, bool esTitulo = false)
        => new() { Id = id, Item = item, Descripcion = desc, EsTitulo = esTitulo, Cantidad = 1 };

    [Fact]
    public void CountPartidas_ExcludesTitles()
    {
        var items = new[] { Item("1.0", "1", "Título", true), Item("1.1", "1.1", "Partida") };
        Assert.Equal(1, _service.CountPartidas(items));
        Assert.Equal(1, _service.CountTitulos(items));
    }

    [Fact]
    public void FirstEditableItemId_ReturnsFirstNonTitle()
    {
        var items = new[] { Item("1.0", "1", "Título", true), Item("1.1", "1.1", "Partida") };
        Assert.Equal("1.1", _service.FirstEditableItemId(items));
    }

    [Fact]
    public void FirstEditableItemId_Empty_ReturnsEmpty()
    {
        Assert.Equal("", _service.FirstEditableItemId(Enumerable.Empty<ItemizadoRow>()));
    }

    [Fact]
    public void AddBelow_EmptyList_CreatesTitleAndPartida()
    {
        var items = new List<ItemizadoRow>();
        var result = _service.AddBelow(items, -1, "Nueva", "UN");
        Assert.Equal(2, items.Count);
        Assert.True(items[0].EsTitulo);
        Assert.False(items[1].EsTitulo);
        Assert.Equal("Nueva", items[1].Descripcion);
        Assert.Equal(1, result.SelectedIndex);
        Assert.False(string.IsNullOrWhiteSpace(items[1].Item));
    }

    [Fact]
    public void AddBelow_AfterTitle_AddsChild()
    {
        var items = new List<ItemizadoRow> { Item("1", "1", "Título", true) };
        var result = _service.AddBelow(items, 0, "Canalización", "ML");
        Assert.Equal(2, items.Count);
        Assert.False(items[1].EsTitulo);
        Assert.StartsWith("1.", items[1].Id);
    }

    [Fact]
    public void AddBelow_AfterPartida_SameParent()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1", "1", "Título", true),
            Item("1.1", "1.1", "Partida 1"),
            Item("1.2", "1.2", "Partida 2")
        };
        var result = _service.AddBelow(items, 1, "Nueva", "ML");
        Assert.Equal(4, items.Count);
        Assert.NotEqual("", items[2].Id);
    }

    [Fact]
    public void AddTitle_AutoNumbers()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1.0", "1", "Título 1", true),
            Item("1.1", "1.1", "Partida"),
            Item("2.0", "2", "Título 2", true)
        };
        var result = _service.AddTitle(items);
        Assert.Equal(4, items.Count);
        Assert.Equal("3,0", items[3].Item);
        Assert.True(items[3].EsTitulo);
    }

    [Fact]
    public void AddTitle_WithSelectedIndex_InsertsBelowClickedRow()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1.0", "1", "Título 1", true),
            Item("1.1", "1.1", "Partida"),
            Item("2.0", "2", "Título 2", true)
        };
        var result = _service.AddTitle(items, selectedIndex: 1);
        Assert.Equal(4, items.Count);
        Assert.Equal(2, result.SelectedIndex);
        Assert.True(items[2].EsTitulo);
        Assert.Equal("3,0", items[2].Item);
        // El título original que estaba al final se mantiene después del nuevo.
        Assert.Equal("Título 2", items[3].Descripcion);
    }

    [Fact]
    public void Move_Up_SwapsWithPrevious()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "A"), Item("1.2", "1.2", "B") };
        var result = _service.Move(items, 1, -1);
        Assert.Equal("A", items[1].Descripcion);
        Assert.Equal("B", items[0].Descripcion);
    }

    [Fact]
    public void Move_Down_SwapsWithNext()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "A"), Item("1.2", "1.2", "B") };
        var result = _service.Move(items, 0, 1);
        Assert.Equal("B", items[0].Descripcion);
        Assert.Equal("A", items[1].Descripcion);
    }

    [Fact]
    public void Move_OutOfBounds_NoChange()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "A") };
        var result = _service.Move(items, 0, -1);
        Assert.Single(items);
        Assert.Equal(0, result.SelectedIndex);
    }

    [Fact]
    public void DeleteAt_RemovesPartidaAndCubicacion()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "Partida") };
        var cub = new List<CubicacionRow>
        {
            new() { ItemId = "1.1", Codigo = "PVC20", Cantidad = 50m }
        };
        var qtyHeaders = new Dictionary<string, string[]> { ["1.1"] = new[] { "Zona 1" } };

        var result = _service.DeleteAt(items, cub, qtyHeaders, 0);

        Assert.Empty(items);
        Assert.Empty(cub);
        Assert.Empty(qtyHeaders);
    }

    [Fact]
    public void DeleteAt_Title_RemovesAllChildren()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1", "1", "Título", true),
            Item("1.1", "1.1", "Partida"),
            Item("1.2", "1.2", "Partida 2")
        };
        var result = _service.DeleteAt(items, new List<CubicacionRow>(), new Dictionary<string, string[]>(), 0);

        Assert.Empty(items);
    }

    [Fact]
    public void DeleteAt_SubTitle_RemovesOnlyItsChildrenAndPreservesSiblings()
    {
        var items = new List<ItemizadoRow>
        {
            new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Capítulo 1", EsTitulo = true, NivelJerarquico = 1 },
            new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "Partida 1.1", ParentId = "1.0" },
            new ItemizadoRow { Id = "1.2.0", Item = "1,2", Descripcion = "Subtítulo 1.2", EsTitulo = true, ParentId = "1.0", NivelJerarquico = 2 },
            new ItemizadoRow { Id = "1.2.0.00001", Item = "1,2,1", Descripcion = "Partida 1.2.1", ParentId = "1.2.0" }
        };

        // Borrar el subtítulo 1.2 (índice 2)
        var result = _service.DeleteAt(items, new List<CubicacionRow>(), new Dictionary<string, string[]>(), 2);

        // Debe conservar Capítulo 1 y Partida 1.1, eliminando solo Subtítulo 1.2 y Partida 1.2.1
        Assert.Equal(2, items.Count);
        Assert.Contains(items, x => x.Id == "1.0");
        Assert.Contains(items, x => x.Id == "1.0.00001");
        Assert.DoesNotContain(items, x => x.Id == "1.2.0");
        Assert.DoesNotContain(items, x => x.Id == "1.2.0.00001");
    }

    [Fact]
    public void DeleteAt_InvalidIndex_ReturnsMinusOne()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "A") };
        var result = _service.DeleteAt(items, new List<CubicacionRow>(), new Dictionary<string, string[]>(), 5);
        Assert.Equal(-1, result.SelectedIndex);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        var items = new List<ItemizadoRow> { Item("1.1", "1.1", "A") };
        var cub = new List<CubicacionRow> { new() { ItemId = "1.1" } };
        var headers = new Dictionary<string, string[]> { ["1.1"] = new[] { "Z1" } };

        _service.Clear(items, cub, headers);
        Assert.Empty(items);
        Assert.Empty(cub);
        Assert.Empty(headers);
    }

    [Fact]
    public void ParentOf_ReturnsTopLevel()
    {
        Assert.Equal("1.1", _service.ParentOf("1.1.1"));
        Assert.Equal("1.1", _service.ParentOf("1.1.001"));
        Assert.Equal("1.0", _service.ParentOf("1.1"));
    }

    [Fact]
    public void ParentOf_uses_legacy_title_when_present()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1", "1", "Título", true),
            Item("1.1", "1.1", "Partida")
        };
        Assert.Equal("1", _service.ParentOf("1.1", items));
    }

    [Fact]
    public void DuplicateAt_assigns_unique_item_code()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1.0", "1,0", "Cap", true),
            Item("1.0.00001", "1,1", "Partida")
        };
        var result = _service.DuplicateAt(items, 1);
        Assert.Equal(3, items.Count);
        Assert.NotEqual(items[1].Item, items[2].Item);
        Assert.Equal(2, result.SelectedIndex);
    }

    [Fact]
    public void NextChildId_Increments()
    {
        var items = new List<ItemizadoRow>
        {
            Item("1", "1", "Título", true),
            Item("1.1", "1.1", "Partida"),
            Item("1.2", "1.2", "Partida 2")
        };
        var next = _service.NextChildId(items, "1");
        Assert.Equal("1.0.00003", next);
    }

    [Fact]
    public void FilterForCubicacion_FiltersByText()
    {
        var items = new[] { Item("1.1", "1.1", "Canalización"), Item("1.2", "1.2", "Conductores") };
        var result = _service.FilterForCubicacion(items, "Canal", "");
        Assert.Single(result);
        Assert.Contains("Canal", result[0].DisplayDescription);
    }

    [Fact]
    public void FilterForCubicacion_FiltersByItem()
    {
        var items = new[] { Item("1.1", "1.1", "Uno"), Item("2.1", "2.1", "Otro") };
        var result = _service.FilterForCubicacion(items, "", "2.1");
        Assert.Single(result);
    }

    [Fact]
    public void ReplaceAll_ClearsCubicacionAndReplacesItemizado()
    {
        var itemizado = new List<ItemizadoRow> { Item("1.1", "1.1", "Vieja") };
        var cubicacion = new List<CubicacionRow> { new() { ItemId = "1.1", Codigo = "A" } };
        var headers = new Dictionary<string, string[]> { ["1.1"] = new[] { "A", "B", "C" } };
        var nuevas = new[] { Item("2.1", "2.1", "Nueva") };

        _service.ReplaceAll(itemizado, cubicacion, headers, nuevas);

        Assert.Single(itemizado);
        Assert.Equal("Nueva", itemizado[0].Descripcion);
        Assert.Empty(cubicacion);
        Assert.Empty(headers);
    }

    [Fact]
    public void SyncEditableRows_UpdatesExistingAndAppendsRows()
    {
        var itemizado = new List<ItemizadoRow> { Item("1.1", "1.1", "Antes", esTitulo: false) };
        itemizado[0].Cantidad = 2m;

        _service.SyncEditableRows(itemizado, new[]
        {
            new ItemizadoEditableCell("1.1", "1.1", "Después", "ML", 2m),
            new ItemizadoEditableCell("1.2", "1.2", "Nueva", "UN", 1m)
        });

        Assert.Equal(2, itemizado.Count);
        Assert.Equal("Después", itemizado[0].Descripcion);
        Assert.Equal("ML", itemizado[0].Unidad);
        Assert.Equal("Nueva", itemizado[1].Descripcion);
    }
}
