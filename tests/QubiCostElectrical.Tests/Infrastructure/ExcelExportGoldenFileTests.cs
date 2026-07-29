using System.IO;
using ClosedXML.Excel;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Golden-file: estructura estable del Excel interno de control (hojas, totales, datos clave).
/// </summary>
public sealed class ExcelExportGoldenFileTests : IDisposable
{
    private readonly string outputPath;

    public ExcelExportGoldenFileTests()
    {
        outputPath = Path.Combine(Path.GetTempPath(), $"pe_golden_{Guid.NewGuid():N}.xlsx");
    }

    public void Dispose()
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private static XLWorkbook ExportGoldenWorkbook(string path)
    {
        var ctx = ExcelExportGoldenFixture.Create();
        new ExcelExportInfrastructure().Export(ctx, path);
        return new XLWorkbook(path);
    }

    [Fact]
    public void GoldenFile_ContainsExpectedSheetsInOrder()
    {
        using var wb = ExportGoldenWorkbook(outputPath);

        var names = wb.Worksheets.Select(ws => ws.Name).ToArray();
        Assert.Equal(ExcelExportGoldenFixture.ExpectedSheetNames, names);
    }

    [Fact]
    public void GoldenFile_ProjectMetadata_OnControlAndDatosSheets()
    {
        using var wb = ExportGoldenWorkbook(outputPath);

        var control = wb.Worksheet("00_CONTROL");
        Assert.Equal(ExcelExportGoldenFixture.ProjectName, control.Cell(4, 2).GetString());
        Assert.Equal(ExcelExportGoldenFixture.ClientName, control.Cell(5, 2).GetString());
        Assert.Equal(ExcelExportGoldenFixture.FixedExportDate.Date, control.Cell(7, 2).GetDateTime().Date);

        var datos = wb.Worksheet("01_DATOS_PROYECTO");
        Assert.Equal(ExcelExportGoldenFixture.ProjectName, datos.Cell(11, 2).GetString());
        Assert.Equal(ExcelExportGoldenFixture.ClientName, datos.Cell(12, 2).GetString());
        Assert.Equal("OF-2026-001", datos.Cell(14, 2).GetString());
        Assert.Equal("Rev A", datos.Cell(15, 2).GetString());
    }

    [Fact]
    public void GoldenFile_ItemizadoCliente_HasExpectedPartidaRows()
    {
        using var wb = ExportGoldenWorkbook(outputPath);

        var ws = wb.Worksheet("02_ITEMIZADO_CLIENTE");
        Assert.Equal("ÍTEM", ws.Cell(3, 1).GetString());
        Assert.Equal("1", ws.Cell(4, 1).GetString());
        Assert.Equal("1.1", ws.Cell(5, 1).GetString());
        Assert.Equal("Canalización PVC 20mm", ws.Cell(5, 2).GetString());
        Assert.Equal("ML", ws.Cell(5, 3).GetString());
    }

    [Fact]
    public void GoldenFile_HojaCosto_MatchesBudgetEngine()
    {
        var ctx = ExcelExportGoldenFixture.Create();
        var expected = new BudgetCalculationService().CalculateBudget(
            ctx.Data.Cubicacion,
            BudgetSettings.FromProject(ctx.Data.Proyecto),
            ctx.Data.Apus,
            ctx.Data.Itemizado,
            ctx.Data.OrdenesCambio);

        using var wb = ExportGoldenWorkbook(outputPath);
        var ws = wb.Worksheet("06_HOJA_COSTO");

        Assert.Equal(expected.MaterialesUf, ReadDecimal(ws.Cell(4, 2)), 4);
        Assert.Equal(expected.ManoObraUf, ReadDecimal(ws.Cell(5, 2)), 4);
        Assert.Equal(expected.CostoDirectoUf, ReadDecimal(ws.Cell(11, 2)), 4);
        Assert.Equal(expected.TotalVentaUf, ReadDecimal(ws.Cell(19, 2)), 4);
    }

    [Fact]
    public void GoldenFile_Structure_HasMinimumRowsPerSheet()
    {
        using var wb = ExportGoldenWorkbook(outputPath);

        Assert.True(wb.Worksheet("00_CONTROL").LastRowUsed()!.RowNumber() >= 20);
        Assert.True(wb.Worksheet("01_DATOS_PROYECTO").LastRowUsed()!.RowNumber() >= 18);
        Assert.True(wb.Worksheet("02_ITEMIZADO_CLIENTE").LastRowUsed()!.RowNumber() >= 4);
        Assert.True(wb.Worksheet("03_CUBICACION_DETALLE").LastRowUsed()!.RowNumber() >= 4);
        Assert.True(wb.Worksheet("06_HOJA_COSTO").LastRowUsed()!.RowNumber() >= 19);
        Assert.True(wb.Worksheet("09_OFERTA_CLIENTE").LastRowUsed()!.RowNumber() >= 10);
    }

    [Fact]
    public void GoldenFile_OfertaCliente_MatchesNetAmount()
    {
        var ctx = ExcelExportGoldenFixture.Create();
        var settings = BudgetSettings.FromProject(ctx.Data.Proyecto);
        var expected = new BudgetCalculationService().CalculateBudget(
            ctx.Data.Cubicacion,
            settings,
            ctx.Data.Apus,
            ctx.Data.Itemizado,
            ctx.Data.OrdenesCambio);

        using var wb = ExportGoldenWorkbook(outputPath);
        var ws = wb.Worksheet("09_OFERTA_CLIENTE");

        decimal netAmount = ReadDecimal(ws.Cell(8, 2));
        // La oferta siempre muestra neto (sin IVA); el IVA se indica en línea separada.
        Assert.Equal(expected.TotalVentaNetoUf, netAmount, 4);
    }
    private static decimal ReadDecimal(IXLCell cell)
    {
        if (cell.TryGetValue(out decimal value))
            return value;
        if (cell.TryGetValue(out double d))
            return (decimal)d;
        return 0m;
    }
}
