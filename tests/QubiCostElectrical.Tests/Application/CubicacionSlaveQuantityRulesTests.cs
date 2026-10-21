using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CubicacionSlaveQuantityRulesTests
{
    [Fact]
    public void ClearWithoutMaster_BorraCantidadYCasillasDelEsclavo()
    {
        var maestro = new CubicacionRow { Id = "M", ItemId = "P1", Codigo = "TUB", Cantidad = 10m, QtyExpressions = ["10"] };
        var esclavo = new CubicacionRow
        {
            Id = "E",
            ItemId = "P1",
            Codigo = "TAPA",
            Cantidad = 4m,
            QtyExpressions = ["4"],
            TotalExpression = "4",
            Esclavo = new CubicacionEsclavoVinculo { MaestroCodigo = "TUB", Factor = 0.4m }
        };
        var rows = new List<CubicacionRow> { maestro, esclavo };

        var cleared = CubicacionSlaveQuantityRules.ClearWithoutMaster(rows, "P1", [maestro]);

        Assert.Same(esclavo, Assert.Single(cleared));
        Assert.Equal(0m, esclavo.Cantidad);
        Assert.Empty(esclavo.QtyExpressions!);
        Assert.Equal("", esclavo.TotalExpression);
        Assert.Equal(10m, maestro.Cantidad);
    }

    [Fact]
    public void ClearWithoutMaster_ConservaElEsclavoSiQuedaOtroMaestro()
    {
        var quitado = new CubicacionRow { Id = "M1", ItemId = "P1", Codigo = "TUB", Cantidad = 10m };
        var queda = new CubicacionRow { Id = "M2", ItemId = "P1", Codigo = "TUB", Cantidad = 8m };
        var esclavo = new CubicacionRow
        {
            Id = "E",
            ItemId = "P1",
            Codigo = "TAPA",
            Cantidad = 4m,
            QtyExpressions = ["4"],
            Esclavo = new CubicacionEsclavoVinculo { MaestroCodigo = "TUB" }
        };

        var cleared = CubicacionSlaveQuantityRules.ClearWithoutMaster(new List<CubicacionRow> { quitado, queda, esclavo }, "P1", [quitado]);

        Assert.Empty(cleared);
        Assert.Equal(4m, esclavo.Cantidad);
    }
}
