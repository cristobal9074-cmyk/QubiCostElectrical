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

    [Fact]
    public void CompareWithIncomingItemizado_DetectsMayorMetraje_MenorMetraje_Adicional_Deductivo()
    {
        var currentProject = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Edificio Torre", ValorUfClp = 38000m },
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización EMT 25mm", CantidadOriginal = 100m, Cantidad = 100m, Unidad = "m" },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Cable EVA 2.5mm2", CantidadOriginal = 200m, Cantidad = 200m, Unidad = "m" },
                new ItemizadoRow { Id = "P3", Item = "1.3", Descripcion = "Tablero Distribución TDF", CantidadOriginal = 2m, Cantidad = 2m, Unidad = "un" }
            ],
            Cubicacion =
            [
                // P1: 100m @ 0.5 UF = 50 UF
                new CubicacionRow { ItemId = "P1", Descripcion = "Tubo EMT", Cantidad = 100m, MatUf = 0.5m },
                // P2: 200m @ 0.2 UF = 40 UF
                new CubicacionRow { ItemId = "P2", Descripcion = "Conductor EVA", Cantidad = 200m, MatUf = 0.2m },
                // P3: 2un @ 15 UF = 30 UF
                new CubicacionRow { ItemId = "P3", Descripcion = "Gabinete TDF", Cantidad = 2m, MatUf = 15m }
            ]
        };

        // Incoming Addenda from mandante:
        // - Item 1.1: subió de 100 a 150m (Mayor metraje)
        // - Item 1.2: bajó de 200 a 120m (Menor metraje)
        // - Item 1.3: omitido / eliminado (Deductivo)
        // - Item 1.4: nuevo grupo generador (Adicional)
        var incomingAddenda = new List<ItemizadoRow>
        {
            new ItemizadoRow { Item = "1.1", Descripcion = "Canalización EMT 25mm", CantidadOriginal = 150m, Unidad = "m" },
            new ItemizadoRow { Item = "1.2", Descripcion = "Cable EVA 2.5mm2", CantidadOriginal = 120m, Unidad = "m" },
            new ItemizadoRow { Item = "1.4", Descripcion = "Generador de Respaldo 50kVA", CantidadOriginal = 1m, Unidad = "un" }
        };

        var summary = service.CompareWithIncomingItemizado(
            incomingAddenda,
            currentProject,
            incomingLabel: "Aclaración N°2",
            currentLabel: "Oferta Base");

        Assert.Equal("Aclaración N°2", summary.CurrentLabel);
        Assert.Equal("Oferta Base", summary.BaselineLabel);

        // P1: Base = 50 UF, Addenda = 150 * 0.5 = 75 UF -> Delta +25 UF (Mayor metraje)
        var p1 = summary.Rows.First(r => r.ItemId == "P1");
        Assert.Equal(100m, p1.CantidadBase);
        Assert.Equal(150m, p1.CantidadActual);
        Assert.Equal(50m, p1.DeltaCantidad);
        Assert.Equal(50m, p1.BaselineUf);
        Assert.Equal(75m, p1.CurrentUf);
        Assert.Equal(25m, p1.DeltaUf);
        Assert.Equal(RevisionDiffKind.Modificado, p1.DiffKind);
        Assert.Equal("MAYOR METRAJE", p1.DisplayBadge);

        // P2: Base = 40 UF, Addenda = 120 * 0.2 = 24 UF -> Delta -16 UF (Menor metraje)
        var p2 = summary.Rows.First(r => r.ItemId == "P2");
        Assert.Equal(200m, p2.CantidadBase);
        Assert.Equal(120m, p2.CantidadActual);
        Assert.Equal(-80m, p2.DeltaCantidad);
        Assert.Equal(40m, p2.BaselineUf);
        Assert.Equal(24m, p2.CurrentUf);
        Assert.Equal(-16m, p2.DeltaUf);
        Assert.Equal(RevisionDiffKind.Modificado, p2.DiffKind);
        Assert.Equal("MENOR METRAJE", p2.DisplayBadge);

        // P3: Omitida en Addenda -> Deductivo
        var p3 = summary.Rows.First(r => r.ItemId == "P3");
        Assert.Equal(2m, p3.CantidadBase);
        Assert.Equal(0m, p3.CantidadActual);
        Assert.Equal(30m, p3.BaselineUf);
        Assert.Equal(0m, p3.CurrentUf);
        Assert.Equal(-30m, p3.DeltaUf);
        Assert.Equal(RevisionDiffKind.Deductivo, p3.DiffKind);
        Assert.Equal("- DEDUCTIVO", p3.DisplayBadge);

        // P4: Nueva en Addenda -> Adicional
        var p4 = summary.Rows.First(r => r.Item == "1.4");
        Assert.Equal(0m, p4.CantidadBase);
        Assert.Equal(1m, p4.CantidadActual);
        Assert.Equal(RevisionDiffKind.Adicional, p4.DiffKind);
        Assert.Equal("+ ADICIONAL", p4.DisplayBadge);

        Assert.Equal(1, summary.AddedCount);
        Assert.Equal(1, summary.RemovedCount);
        Assert.Equal(2, summary.ChangedCount);
    }

    [Fact]
    public void CompareWithIncomingItemizado_MatchesByDescription_WhenItemCodeDiffers()
    {
        var currentProject = new ProjectFileData
        {
            Itemizado =
            [
                new ItemizadoRow { Id = "P1", Item = "1.01", Descripcion = "Interruptor Termomagnético 1x16A", CantidadOriginal = 20m, Cantidad = 20m }
            ],
            Cubicacion =
            [
                new CubicacionRow { ItemId = "P1", Descripcion = "Disyuntor", Cantidad = 20m, MatUf = 1m }
            ]
        };

        // The mandante changed the numbering from "1.01" to "2.1", but description is the same
        var incoming = new List<ItemizadoRow>
        {
            new ItemizadoRow { Item = "2.1", Descripcion = "Interruptor Termomagnético 1x16A", CantidadOriginal = 30m }
        };

        var summary = service.CompareWithIncomingItemizado(incoming, currentProject);

        var match = summary.Rows.First(r => r.ItemId == "P1");
        Assert.Equal(20m, match.CantidadBase);
        Assert.Equal(30m, match.CantidadActual);
        Assert.Equal(10m, match.DeltaCantidad);
        Assert.Equal(RevisionDiffKind.Modificado, match.DiffKind);
        Assert.Equal("MAYOR METRAJE", match.DisplayBadge);
    }
}
