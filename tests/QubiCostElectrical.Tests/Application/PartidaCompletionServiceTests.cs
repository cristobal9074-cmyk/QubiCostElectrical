using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class PartidaCompletionServiceTests
{
    private readonly PartidaCompletionService _service = new();

    private static ItemizadoRow Item(string id, string item, bool esTitulo = false, string parent = "")
        => new() { Id = id, Item = item, Descripcion = item, EsTitulo = esTitulo, ParentId = parent };

    private static CubicacionRow Cub(string itemId, string codigo, decimal cantidad, string? desc = null)
        => new() { ItemId = itemId, Codigo = codigo, Descripcion = desc ?? codigo, Cantidad = cantidad, MatUf = 0.35m };

    [Fact]
    public void GetPartidaState_NoRows_Empty()
    {
        Assert.Equal(PartidaCompletionState.Empty, _service.GetPartidaState("P1", Enumerable.Empty<CubicacionRow>()));
    }

    [Fact]
    public void GetPartidaState_CompletedLine_Completed()
    {
        var cub = new[] { Cub("P1", "PVC20", 50m) };
        Assert.Equal(PartidaCompletionState.Completed, _service.GetPartidaState("P1", cub));
    }

    [Fact]
    public void GetPartidaState_CodeWithZeroQty_Incomplete()
    {
        var cub = new[] { Cub("P1", "PVC20", 0m) };
        Assert.Equal(PartidaCompletionState.Incomplete, _service.GetPartidaState("P1", cub));
    }

    [Fact]
    public void GetPartidaState_NoCodeWithQty_Incomplete()
    {
        var cub = new[] { new CubicacionRow { ItemId = "P1", Cantidad = 10m, MatUf = 1m } };
        Assert.Equal(PartidaCompletionState.Incomplete, _service.GetPartidaState("P1", cub));
    }

    [Fact]
    public void GetPartidaState_EmptyRow_Empty()
    {
        var cub = new[] { new CubicacionRow { ItemId = "P1" } };
        Assert.Equal(PartidaCompletionState.Empty, _service.GetPartidaState("P1", cub));
    }

    [Fact]
    public void Build_SingleCompletedPartida()
    {
        var items = new[] { Item("P1", "1.1") };
        var cub = new[] { Cub("P1", "PVC20", 50m) };
        var result = _service.Build(items, cub);

        Assert.Equal(1, result.CompletedPartidas);
        Assert.Equal(0, result.IncompletePartidas);
        Assert.Equal(0, result.EmptyPartidas);
        Assert.Equal(100m, result.CompletedPct);
    }

    [Fact]
    public void Build_SingleEmptyPartida()
    {
        var items = new[] { Item("P1", "1.1") };
        var result = _service.Build(items, Enumerable.Empty<CubicacionRow>());

        Assert.Equal(0, result.CompletedPartidas);
        Assert.Equal(1, result.EmptyPartidas);
    }

    [Fact]
    public void Build_MultiplePartidas_MixedStates()
    {
        var items = new[] { Item("P1", "1.1"), Item("P2", "1.2") };
        var cub = new[] { Cub("P1", "PVC20", 50m) };
        var result = _service.Build(items, cub);

        Assert.Equal(1, result.CompletedPartidas);
        Assert.Equal(0, result.IncompletePartidas);
        Assert.Equal(1, result.EmptyPartidas);
    }

    [Fact]
    public void Build_TitleWithChildren_AggregatesState()
    {
        var items = new[]
        {
            Item("1", "1", esTitulo: true),
            Item("1.1", "1.1"),
            Item("1.2", "1.2")
        };
        var cub = new[] { Cub("1.1", "PVC20", 50m) };
        var result = _service.Build(items, cub);

        var detail = result.GetDetail("1");
        Assert.True(detail.IsGroup);
        Assert.Equal(2, detail.TotalPartidas);
        Assert.Equal(1, detail.CompletedPartidas);
        Assert.Equal(1, detail.EmptyPartidas);
    }

    [Fact]
    public void Build_TitleAllComplete_ShowsCompleted()
    {
        var items = new[]
        {
            Item("1", "1", esTitulo: true),
            Item("1.1", "1.1"),
            Item("1.2", "1.2")
        };
        var cub = new[] { Cub("1.1", "PVC20", 50m), Cub("1.2", "THHN10", 100m) };
        var result = _service.Build(items, cub);

        Assert.Equal(PartidaCompletionState.Completed, result.GetState("1"));
    }

    [Fact]
    public void Build_TitleWithNoChildren_Empty()
    {
        var items = new[] { Item("1", "1", esTitulo: true) };
        var result = _service.Build(items, Enumerable.Empty<CubicacionRow>());

        var detail = result.GetDetail("1");
        Assert.Equal(0, detail.TotalPartidas);
    }

    [Fact]
    public void GetState_UnknownId_ReturnsEmpty()
    {
        var result = _service.Build(Enumerable.Empty<ItemizadoRow>(), Enumerable.Empty<CubicacionRow>());
        Assert.Equal(PartidaCompletionState.Empty, result.GetState("NOEXISTE"));
    }

    [Fact]
    public void GetIcon_ReturnsCorrectSymbols()
    {
        Assert.Equal("✓", _service.GetIcon(PartidaCompletionState.Completed));
        Assert.Equal("⚠", _service.GetIcon(PartidaCompletionState.Incomplete));
        Assert.Equal("—", _service.GetIcon(PartidaCompletionState.Empty));
    }

    [Fact]
    public void GetTooltip_ReturnsDescriptions()
    {
        Assert.Contains("Cubicado", _service.GetTooltip(PartidaCompletionState.Completed));
        Assert.Contains("Incompleto", _service.GetTooltip(PartidaCompletionState.Incomplete));
        Assert.Contains("Vacío", _service.GetTooltip(PartidaCompletionState.Empty));
    }

    [Fact]
    public void Build_NullInputs_NoException()
    {
        var result = _service.Build(null, null);
        Assert.Equal(0, result.TotalPartidas);
    }

    [Fact]
    public void PartidaCompletionDetail_PendingPartidas_Sum()
    {
        var detail = new PartidaCompletionDetail("P1", false, 3, 1, 1, 1);
        Assert.Equal(2, detail.PendingPartidas);
    }

    [Fact]
    public void PartidaCompletionDetail_CompletedPct_Calculates()
    {
        var detail = new PartidaCompletionDetail("P1", false, 4, 2, 1, 1);
        Assert.Equal(50m, detail.CompletedPct);
    }

    [Fact]
    public void GetLineCompletionPct_uses_completed_lines_ratio()
    {
        var cub = new[]
        {
            Cub("P1", "PVC20", 50m),
            Cub("P1", "", 0m),
            Cub("P1", "THHN10", 10m)
        };

        Assert.Equal(67m, _service.GetLineCompletionPct("P1", cub));
    }
}
