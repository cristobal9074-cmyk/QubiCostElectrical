using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionServiceTests
{
    private readonly CubicacionService _service = new();

    private static CubicacionRow Row(string itemId, string codigo, decimal cantidad, decimal matUf = 1m)
        => new() { Id = Guid.NewGuid().ToString("N"), ItemId = itemId, Codigo = codigo,
            Descripcion = codigo, Cantidad = cantidad, MatUf = matUf };

    [Fact]
    public void GetRowsForItem_ReturnsMatching()
    {
        var cub = new[] { Row("P1", "PVC20", 50m), Row("P2", "THHN10", 100m) };
        var rows = _service.GetRowsForItem(cub, "P1");
        Assert.Single(rows);
    }

    [Fact]
    public void GetRowsForItem_WithOnlyCode_FiltersEmpty()
    {
        var cub = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "", Cantidad = 0m },
            new() { ItemId = "P1", Codigo = "PVC20", Cantidad = 50m }
        };
        var rows = _service.GetRowsForItem(cub, "P1", onlyWithCode: true);
        Assert.Single(rows);
    }

    [Fact]
    public void GetVisualRow_ValidIndex_ReturnsRow()
    {
        var cub = new[] { Row("P1", "PVC20", 50m), Row("P1", "THHN10", 100m) };
        var row = _service.GetVisualRow(cub, "P1", 1);
        Assert.NotNull(row);
        Assert.Equal("THHN10", row.Codigo);
    }

    [Fact]
    public void GetVisualRow_NegativeIndex_ReturnsNull()
    {
        Assert.Null(_service.GetVisualRow(new[] { Row("P1", "PVC20", 50m) }, "P1", -1));
    }

    [Fact]
    public void TryGetOrCreateVisualRow_Fails_NullList()
    {
        var result = _service.TryGetOrCreateVisualRow(null!, "P1", 0);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TryGetOrCreateVisualRow_Fails_EmptyItemId()
    {
        var result = _service.TryGetOrCreateVisualRow(new List<CubicacionRow>(), "", 0);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TryGetOrCreateVisualRow_Fails_NegativeIndex()
    {
        var result = _service.TryGetOrCreateVisualRow(new List<CubicacionRow>(), "P1", -1);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TryGetOrCreateVisualRow_CreatesNew()
    {
        var cub = new List<CubicacionRow>();
        var result = _service.TryGetOrCreateVisualRow(cub, "P1", 0);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("P1", result.Value.ItemId);
        Assert.Single(cub);
    }

    [Fact]
    public void ClearRow_ResetsFields()
    {
        var row = Row("P1", "PVC20", 50m);
        _service.ClearRow(row);
        Assert.Equal("", row.Codigo);
        Assert.Equal(0m, row.Cantidad);
        Assert.Equal(0m, row.MatUf);
    }

    [Fact]
    public void RemoveEmptyRowsForItem_ClearsEmpty()
    {
        var cub = new List<CubicacionRow>
        {
            new() { ItemId = "P1", Codigo = "", Cantidad = 0m },
            Row("P1", "PVC20", 50m)
        };
        _service.RemoveEmptyRowsForItem(cub, "P1");
        Assert.Single(cub);
    }

    [Fact]
    public void ApplyMaterial_SetsFields()
    {
        var row = new CubicacionRow();
        var mat = new MaterialRow { Codigo = "PVC20", Descripcion = "PVC 20mm", Unidad = "ML", MatClp = 350m, MoClp = 100m };
        _service.ApplyMaterial(row, mat, valorUfClp: 1000m);
        Assert.Equal("PVC20", row.Codigo);
        Assert.Equal(0.35m, row.MatUf);
    }

    [Fact]
    public void CloneRowForItem_CopiesFields()
    {
        var source = Row("P1", "PVC20", 50m);
        source.Unidad = "ML";
        source.MatClp = 350m;
        source.MoClp = 100m;
        source.MatUf = 0.35m;
        source.MoUf = 0.1m;
        var clone = _service.CloneRowForItem(source, "P2");
        Assert.Equal("P2", clone.ItemId);
        Assert.Equal("PVC20", clone.Codigo);
        Assert.Equal(50m, clone.Cantidad);
        Assert.Equal(350m, clone.MatClp);
        Assert.Equal(100m, clone.MoClp);
        Assert.Equal(0.35m, clone.MatUf);
        Assert.Equal(0.1m, clone.MoUf);
    }

    [Fact]
    public void CloneRowForItem_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _service.CloneRowForItem(null!, "P2"));
    }

    [Fact]
    public void SyncPricesFromCatalog_UpdatesRowAndValorizacionClp()
    {
        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "PVC20",
            MatClp = 1000m,
            MoClp = 200m
        };
        var materiales = new List<MaterialRow>
        {
            new() { Codigo = "PVC20", MatClp = 15000m, MoClp = 4500m }
        };
        var valorizaciones = new List<CubicacionValorizacionRow>
        {
            new() { RecursoId = "R1", MatClp = 1000m, MoClp = 200m, MatUf = 1000m, MoUf = 200m }
        };

        int updated = _service.SyncPricesFromCatalog(
            new[] { row },
            materiales,
            valorUfClp: 39000m,
            valorizaciones);

        Assert.Equal(1, updated);
        Assert.Equal(15000m, row.MatClp);
        Assert.Equal(4500m, row.MoClp);
        Assert.Equal(15000m, valorizaciones[0].MatClp);
        Assert.Equal(4500m, valorizaciones[0].MoClp);
    }

    [Fact]
    public void CloneItemCubicacion_ClonesRows()
    {
        var cub = new List<CubicacionRow> { Row("P1", "PVC20", 50m) };
        var result = _service.CloneItemCubicacion(cub, "P1", "P2");
        Assert.Equal(1, result.RowsCopied);
        Assert.Equal(2, cub.Count);
        Assert.Contains(cub, r => r.ItemId == "P2");
    }

    [Fact]
    public void CloneItemCubicacion_SameSourceAndTarget_Throws()
    {
        var cub = new List<CubicacionRow> { Row("P1", "PVC20", 50m) };
        Assert.Throws<InvalidOperationException>(() =>
            _service.CloneItemCubicacion(cub, "P1", "P1"));
    }

    [Fact]
    public void CloneItemCubicacion_EmptySource_NoClones()
    {
        var cub = new List<CubicacionRow>();
        var result = _service.CloneItemCubicacion(cub, "P1", "P2");
        Assert.Equal(0, result.RowsCopied);
    }

    [Fact]
    public void DefaultQtyHeaderLabel_ReturnsZona()
    {
        Assert.Equal("Zona 1", _service.DefaultQtyHeaderLabel(0));
        Assert.Equal("Zona 3", _service.DefaultQtyHeaderLabel(2));
    }

    [Fact]
    public void NormalizeQtyHeaderLabel_TrimsAndCollapses()
    {
        Assert.Equal("Piso 1", _service.NormalizeQtyHeaderLabel(0, "  Piso  1  "));
    }

    [Fact]
    public void NormalizeQtyHeaderLabel_Empty_FallsBack()
    {
        Assert.Equal("Zona 1", _service.NormalizeQtyHeaderLabel(0, ""));
    }

    [Fact]
    public void GetHeadersForItem_ReturnsDefaultsForMissing()
    {
        var dict = new Dictionary<string, string[]>();
        var headers = _service.GetHeadersForItem(dict, "P1", 3);
        Assert.Equal(new[] { "Zona 1", "Zona 2", "Zona 3" }, headers);
    }

    [Fact]
    public void SaveHeadersForItem_StoresValues()
    {
        var dict = new Dictionary<string, string[]>();
        var result = _service.SaveHeadersForItem(dict, "P1", new[] { "Piso 1", "Piso 2" }, 3);
        Assert.True(result.Changed);
        Assert.True(result.Stored);
    }

    [Fact]
    public void SameQtyHeaders_Compares()
    {
        var a = new[] { "Zona 1", "Zona 2" };
        var b = new[] { "Zona 1", "Zona 2" };
        Assert.True(_service.SameQtyHeaders(a, b, 2));
    }

    [Fact]
    public void AllQtyHeadersAreDefaults_DetectsCustom()
    {
        var custom = new[] { "Piso 1", "Zona 2" };
        Assert.False(_service.AllQtyHeadersAreDefaults(custom, 2));
    }

    [Fact]
    public void AllQtyHeadersAreDefaults_TrueForDefaults()
    {
        var defaults = new[] { "Zona 1", "Zona 2" };
        Assert.True(_service.AllQtyHeadersAreDefaults(defaults, 2));
    }
}
