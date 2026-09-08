using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class MaterialBatchReplaceServiceTests
{
    [Fact]
    public void GetUsedMaterials_ExtractsUniqueMaterialsFromCubicacionAndApus()
    {
        var session = new BudgetProjectSession();
        session.NewProject("TestProject");

        session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "CBL-NYA-2.5",
            Descripcion = "Conductor NYA 2.5 mm²",
            Unidad = "m",
            Cantidad = 150m,
            MatClp = 400m
        });

        var apu = new ApuRow { ItemId = "P2", Descripcion = "Partida APU" };
        apu.Recursos.Add(new ApuRecursoRow
        {
            CodigoMaterial = "CBL-NYA-2.5",
            Descripcion = "Conductor NYA 2.5 mm²",
            Unidad = "m",
            Rendimiento = 1.05m,
            PrecioUf = 0.01m
        });
        session.Apus.Add(apu);

        var service = new MaterialBatchReplaceService();
        var materials = service.GetUsedMaterials(session);

        Assert.Single(materials);
        Assert.Equal("CBL-NYA-2.5", materials[0].Codigo);
    }

    [Fact]
    public void PreviewReplacements_CalculatesAccurateDeltaAndOccurrences()
    {
        var session = new BudgetProjectSession();
        session.NewProject("TestProject");
        session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.01", Descripcion = "Alumbrado" });

        session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "CBL-NYA-2.5",
            Descripcion = "Conductor NYA 2.5 mm²",
            Unidad = "m",
            Cantidad = 1000m,
            MatClp = 400m,
            MoClp = 100m // Unitario = $ 500 CLP -> Subtotal = $ 500.000 CLP
        });

        var targetMaterial = new MaterialRow
        {
            Codigo = "CBL-EVA-2.5",
            Descripcion = "Conductor H07Z1-K (EVA) 2.5 mm²",
            Unidad = "m",
            MatClp = 500m,
            MoClp = 100m // Unitario = $ 600 CLP -> Subtotal = $ 600.000 CLP
        };

        var service = new MaterialBatchReplaceService();
        var previews = service.PreviewReplacements(session, "CBL-NYA-2.5", targetMaterial);

        Assert.Single(previews);
        var p = previews[0];
        Assert.Equal("1.01", p.PartidaItem);
        Assert.Equal(1000m, p.Cantidad);
        Assert.Equal(500000m, p.SubtotalActualClp);
        Assert.Equal(600000m, p.NuevoSubtotalClp);
        Assert.Equal(100000m, p.DeltaClp);
    }

    [Fact]
    public void ExecuteReplacement_UpdatesCubicacion_PreservingQuantitiesAndFormulas()
    {
        var session = new BudgetProjectSession();
        session.NewProject("TestProject");
        session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.01", Descripcion = "Alumbrado" });

        var expressions = new[] { "15+20*7", "40", "12.5" };
        var row = new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "CBL-NYA-2.5",
            Descripcion = "Conductor NYA 2.5 mm²",
            Unidad = "m",
            Cantidad = 192.5m,
            QtyExpressions = expressions,
            TotalExpression = "SUM(15+20*7, 40, 12.5)",
            MatClp = 400m,
            MoClp = 150m,
            Area = "Piso 2"
        };
        session.Cubicacion.Add(row);

        var targetMaterial = new MaterialRow
        {
            Codigo = "CBL-EVA-2.5",
            Descripcion = "Conductor H07Z1-K (EVA) 2.5 mm²",
            Unidad = "m",
            MatClp = 550m,
            MoClp = 150m,
            MatUf = 0.015m,
            MoUf = 0.004m
        };

        var service = new MaterialBatchReplaceService();
        var undoLog = new List<string>();
        var result = service.ExecuteReplacement(session, (s, desc) => undoLog.Add(desc), "CBL-NYA-2.5", targetMaterial);

        Assert.Equal(1, result.PartidasActualizadas);
        Assert.Equal(1, result.FilasCubicacionModificadas);

        // Verify resource replacement
        Assert.Equal("CBL-EVA-2.5", row.Codigo);
        Assert.Equal("Conductor H07Z1-K (EVA) 2.5 mm²", row.Descripcion);
        Assert.Equal(550m, row.MatClp);

        // Verify measurements, expressions and areas are 100% PRESERVED
        Assert.Equal(192.5m, row.Cantidad);
        Assert.Equal(expressions, row.QtyExpressions);
        Assert.Equal("SUM(15+20*7, 40, 12.5)", row.TotalExpression);
        Assert.Equal("Piso 2", row.Area);

        // Verify Undo point was recorded
        Assert.Single(undoLog);
        Assert.Contains("Reemplazar material CBL-NYA-2.5", undoLog[0]);
    }

    [Fact]
    public void CubicacionShortcutCatalog_ContainsBatchReplace_CtrlH()
    {
        var shortcut = CubicacionShortcutCatalog.Find(CubicacionShortcutCatalog.BatchReplace);
        Assert.NotNull(shortcut);
        Assert.Equal("Ctrl+H", shortcut!.DefaultGesture);
        Assert.True(CubicacionShortcutCatalog.IsKnown(CubicacionShortcutCatalog.BatchReplace));
    }
}
