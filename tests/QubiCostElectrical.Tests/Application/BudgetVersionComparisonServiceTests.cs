using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetVersionComparisonServiceTests
{
    private readonly BudgetVersionComparisonService service = new();

    [Fact]
    public void Compare_ReportsAddedRemovedChangedRows()
    {
        var baseline = new BudgetVersionData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Conductores" }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Descripcion = "PVC 20", Cantidad = 10m, MatUf = 1m },
                new CubicacionRow { ItemId = "P2", Descripcion = "THHN 10", Cantidad = 5m, MatUf = 1m }
            ]
        };

        var current = new ProjectFileData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" },
                new ItemizadoRow { Id = "P3", Item = "1.3", Descripcion = "Tableros" }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Descripcion = "PVC 20", Cantidad = 12m, MatUf = 1m },
                new CubicacionRow { ItemId = "P3", Descripcion = "Tablero", Cantidad = 7m, MatUf = 1m }
            ]
        };

        var summary = service.Compare(baseline, current, "Corte base", "Estado actual");

        Assert.Equal("Corte base", summary.BaselineLabel);
        Assert.Equal("Estado actual", summary.CurrentLabel);
        Assert.Equal(1, summary.AddedCount);
        Assert.Equal(1, summary.RemovedCount);
        Assert.Equal(1, summary.ChangedCount);
        Assert.Equal(15m, summary.BaselineUf);
        Assert.Equal(19m, summary.CurrentUf);
        Assert.Equal(4m, summary.DeltaUf);
        Assert.True(summary.Rows.Count >= 3);

        var changed = summary.Rows.First(x => x.ItemId == "P1");
        Assert.Equal(10m, changed.BaselineUf);
        Assert.Equal(12m, changed.CurrentUf);
        Assert.Equal(2m, changed.DeltaUf);
        Assert.Equal("Aumentó", changed.Estado);
    }

    [Fact]
    public void Compare_VersionSnapshots_ReportsDifferences()
    {
        var baseline = new BudgetVersionData
        {
            Itemizado = [new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" }],
            Cubicacion = [new CubicacionRow { ItemId = "P1", Descripcion = "PVC 20", Cantidad = 10m, MatUf = 1m }]
        };

        var current = new BudgetVersionData
        {
            Itemizado = [new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización" }],
            Cubicacion = [new CubicacionRow { ItemId = "P1", Descripcion = "PVC 20", Cantidad = 12m, MatUf = 1m }]
        };

        var summary = service.Compare(baseline, current, "V1", "V2");

        Assert.Equal("V1", summary.BaselineLabel);
        Assert.Equal("V2", summary.CurrentLabel);
        Assert.Equal(10m, summary.BaselineUf);
        Assert.Equal(12m, summary.CurrentUf);
        Assert.Equal(1, summary.ChangedCount);
    }
}
