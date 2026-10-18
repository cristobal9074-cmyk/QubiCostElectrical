using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class OfferSummaryPresentationModeTests : IDisposable
{
    private readonly List<string> tempFiles = new();

    public void Dispose()
    {
        foreach (var file in tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private string GetTempPath(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"offer_mode_test_{Guid.NewGuid():N}.{extension.TrimStart('.')}");
        tempFiles.Add(path);
        return path;
    }

    private static ExportProjectContext CreateContext(bool ocultarGgUt)
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Edificio Solar Test",
                Cliente = "Constructora Horizonte",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 38000m
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "TIT01", Item = "1", Descripcion = "TABLEROS Y ALIMENTADORES", EsTitulo = true },
                new() { Id = "P01", Item = "1.1", Descripcion = "Tablero General TDF", Unidad = "UN", Cantidad = 1m },
                new() { Id = "P02", Item = "1.2", Descripcion = "Alimentador Cu 3x50mm2", Unidad = "ML", Cantidad = 80m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C01", ItemId = "P01", Tipo = "MAT", Codigo = "M01", Descripcion = "Gabinete TDF", Unidad = "UN", Cantidad = 1m, MatUf = 500000m },
                new() { Id = "C02", ItemId = "P01", Tipo = "MO", Codigo = "MO01", Descripcion = "Montaje TDF", Unidad = "UN", Cantidad = 1m, MoUf = 200000m },
                new() { Id = "C03", ItemId = "P02", Tipo = "MAT", Codigo = "M02", Descripcion = "Cable 50mm2", Unidad = "ML", Cantidad = 80m, MatUf = 10000m },
                new() { Id = "C04", ItemId = "P02", Tipo = "MO", Codigo = "MO02", Descripcion = "Tendido Cable", Unidad = "ML", Cantidad = 80m, MoUf = 4000m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "M01", Descripcion = "Gabinete TDF", Unidad = "UN", Categoria = "Tableros", MatUf = 500000m },
                new() { Codigo = "MO01", Descripcion = "Montaje TDF", Unidad = "UN", Categoria = "MO", MoUf = 200000m },
                new() { Codigo = "M02", Descripcion = "Cable 50mm2", Unidad = "ML", Categoria = "Conductores", MatUf = 10000m },
                new() { Codigo = "MO02", Descripcion = "Tendido Cable", Unidad = "ML", Categoria = "MO", MoUf = 4000m }
            }
        };

        data.Proyecto.OfertaPdf.OcultarGgYUtilidadEnResumen = ocultarGgUt;

        var budgetCalc = new BudgetCalculationService();
        var budget = budgetCalc.CalculateBudget(
            data.Cubicacion,
            BudgetSettings.FromProject(data.Proyecto),
            data.Apus,
            data.Itemizado,
            data.OrdenesCambio);

        return new ExportProjectContext
        {
            Data = data,
            ExportedBy = "TestAgent",
            OfferNetAmountUf = budget.TotalVentaNetoUf
        };
    }

    [Fact]
    public void ExcelExporter_ModeOcultarGgUt_DoesNotContainGgNorUtilidadRows()
    {
        var ctx = CreateContext(ocultarGgUt: true);
        var path = GetTempPath("xlsx");
        var exporter = new IngelsamOfferSummaryExcelExporter();

        exporter.Export(ctx, path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("-=RESUMEN INGELSAM=-");
        Assert.NotNull(ws);

        // Buscar celdas en columna B o C que digan "Gastos Generales" o "Utilidad"
        var allCellTexts = ws.CellsUsed().Select(c => c.GetString().Trim()).ToList();
        Assert.DoesNotContain(allCellTexts, t => t.Equals("Gastos Generales", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allCellTexts, t => t.Equals("Utilidad", StringComparison.OrdinalIgnoreCase));

        // Debe contener TOTAL PRESUPUESTO NETO e IVA (19%)
        Assert.Contains(allCellTexts, t => t.StartsWith("TOTAL PRESUPUESTO NETO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allCellTexts, t => t.Equals("IVA (19%)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allCellTexts, t => t.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExcelExporter_ModeDesglosado_ContainsSubtotalGgAndUtilidadRows()
    {
        var ctx = CreateContext(ocultarGgUt: false);
        var path = GetTempPath("xlsx");
        var exporter = new IngelsamOfferSummaryExcelExporter();

        exporter.Export(ctx, path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("-=RESUMEN INGELSAM=-");
        Assert.NotNull(ws);

        var allCellTexts = ws.CellsUsed().Select(c => c.GetString().Trim()).ToList();
        Assert.Contains(allCellTexts, t => t.Equals("Subtotal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allCellTexts, t => t.Equals("Gastos Generales", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allCellTexts, t => t.Equals("Utilidad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allCellTexts, t => t.StartsWith("TOTAL PRESUPUESTO NETO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void QuestPdfOfferSummaryExporter_BothModes_GenerateValidPdfs()
    {
        var exporter = new QuestPdfOfferSummaryExporter();

        // 1. Ocultar GG y UT (Modo Precios Enteros)
        var ctx1 = CreateContext(ocultarGgUt: true);
        var path1 = GetTempPath("pdf");
        exporter.Export(ctx1, path1);
        Assert.True(File.Exists(path1));
        var bytes1 = File.ReadAllBytes(path1);
        Assert.True(bytes1.Length > 1000);
        Assert.Equal(0x25, bytes1[0]); // %PDF

        // 2. Desglosar GG y UT (Modo Desglosado)
        var ctx2 = CreateContext(ocultarGgUt: false);
        var path2 = GetTempPath("pdf");
        exporter.Export(ctx2, path2);
        Assert.True(File.Exists(path2));
        var bytes2 = File.ReadAllBytes(path2);
        Assert.True(bytes2.Length > 1000);
        Assert.Equal(0x25, bytes2[0]); // %PDF
    }

    [Fact]
    public void ProjectWorkspaceService_PreservesOcultarGgYUtilidadEnResumen()
    {
        var session = new BudgetProjectSession();
        var workspace = new ProjectEditWorkspaceService(session);

        // Default should be true
        var snapshot = workspace.Load();
        Assert.True(snapshot.OcultarGgYUtilidadEnResumen);

        // Change to false and apply
        snapshot.OcultarGgYUtilidadEnResumen = false;
        workspace.Apply(snapshot);

        Assert.False(session.ProjectInfo.OcultarGgYUtilidadEnResumen);
        Assert.False(session.ProjectInfo.OfertaPdf.OcultarGgYUtilidadEnResumen);

        // Reload
        var reloaded = workspace.Load();
        Assert.False(reloaded.OcultarGgYUtilidadEnResumen);

        // Change back to true
        reloaded.OcultarGgYUtilidadEnResumen = true;
        workspace.Apply(reloaded);
        Assert.True(session.ProjectInfo.OcultarGgYUtilidadEnResumen);
    }
}
