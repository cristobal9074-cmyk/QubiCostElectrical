using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionPartidaOrderTests
{
    [Fact]
    public void TryMove_SubeDentroDeLaPartida_AunqueHayaOtraPartidaEnMedio()
    {
        var rows = Intercaladas();
        var b = rows[2];

        Assert.True(CubicacionPartidaOrder.TryMove(rows, "P1", b, -1));

        Assert.Equal(["B", "A"], Codigos(rows, "P1"));
        Assert.Equal(["X"], Codigos(rows, "P2"));
        Assert.Equal("10+2", b.QtyExpressions![0]);
        Assert.Equal("ml", b.Unidad);
        Assert.Equal(12m, b.Cantidad);
    }

    [Fact]
    public void TryMove_DiezVecesArribaYAbajo_VuelveAlMismoOrden()
    {
        var rows = Intercaladas();
        var b = rows[2];

        for (int i = 0; i < 10; i++)
        {
            Assert.True(CubicacionPartidaOrder.TryMove(rows, "P1", b, -1));
            Assert.False(CubicacionPartidaOrder.CanMove(rows, "P1", b, -1));
            Assert.True(CubicacionPartidaOrder.TryMove(rows, "P1", b, 1));
        }

        Assert.Equal(["A", "B"], Codigos(rows, "P1"));
        Assert.Equal(["A", "X", "B"], rows.ConvertAll(r => r.Codigo));
    }

    [Fact]
    public void InsertAtSlot_QuedaEnLaPartida_NoDespuesDelVecinoGlobal()
    {
        var rows = Intercaladas();
        var pegada = new CubicacionRow { Id = "R4", ItemId = "P1", Codigo = "C", Unidad = "m", Cantidad = 3m };

        CubicacionPartidaOrder.InsertAtSlot(rows, "P1", pegada, int.MaxValue);

        Assert.Equal(["A", "B", "C"], Codigos(rows, "P1"));
        Assert.Equal("X", rows[1].Codigo);
        Assert.Equal("B", rows[2].Codigo);
        Assert.Equal("C", rows[3].Codigo);
    }

    private static List<CubicacionRow> Intercaladas() =>
    [
        new() { Id = "R1", ItemId = "P1", Codigo = "A", Unidad = "m", Cantidad = 75m, QtyExpressions = ["4"] },
        new() { Id = "R2", ItemId = "P2", Codigo = "X", Unidad = "un", Cantidad = 1m },
        new() { Id = "R3", ItemId = "P1", Codigo = "B", Unidad = "ml", Cantidad = 12m, QtyExpressions = ["10+2"] }
    ];

    private static List<string> Codigos(IEnumerable<CubicacionRow> rows, string itemId)
    {
        var list = new List<string>();
        foreach (var row in rows)
        {
            if (string.Equals(row.ItemId, itemId, System.StringComparison.Ordinal))
                list.Add(row.Codigo);
        }

        return list;
    }
}
