using System.IO;
using ClosedXML.Excel;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class ItemizadoSpreadsheetReaderTests : IDisposable
{
    private readonly string _path;

    public ItemizadoSpreadsheetReaderTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"itemizado-reader-{Guid.NewGuid():N}.xlsx");
    }

    [Fact]
    public void ReadFirstWorksheet_SkipsCoverSheetAndFindsItemizadoSheet()
    {
        using (var wb = new XLWorkbook())
        {
            wb.AddWorksheet("Portada").Cell(1, 1).Value = "Proyecto";

            var ws = wb.AddWorksheet("Itemizado");
            ws.Cell(1, 1).Value = "ITEM";
            ws.Cell(1, 2).Value = "DESCRIPCION";
            ws.Cell(1, 3).Value = "UNIDAD";
            ws.Cell(1, 4).Value = "CANTIDAD";
            ws.Cell(2, 1).Value = "1";
            ws.Cell(2, 2).Value = "TABLERO GENERAL";
            ws.Cell(2, 3).Value = "GL";
            ws.Cell(2, 4).Value = 1;

            wb.SaveAs(_path);
        }

        var reader = new ItemizadoSpreadsheetReader();
        var table = reader.ReadFirstWorksheet(_path);

        Assert.NotEmpty(table);
        Assert.Equal("ITEM", table[0][0]);
        Assert.Equal("TABLERO GENERAL", table[1][1]);
    }

    [Fact]
    public void InspectWorksheets_IdentifiesMultipleSheetsAndDetectsItemizado()
    {
        using (var wb = new XLWorkbook())
        {
            var ws1 = wb.AddWorksheet("Resumen");
            ws1.Cell(1, 1).Value = "Proyecto General";
            ws1.Cell(2, 1).Value = "Total UF";

            var ws2 = wb.AddWorksheet("Itemizado Electrico");
            ws2.Cell(1, 1).Value = "ITEM";
            ws2.Cell(1, 2).Value = "DESCRIPCION";
            ws2.Cell(1, 3).Value = "UNIDAD";
            ws2.Cell(1, 4).Value = "CANTIDAD";
            ws2.Cell(2, 1).Value = "1.1";
            ws2.Cell(2, 2).Value = "Alimentador principal";
            ws2.Cell(2, 3).Value = "ML";
            ws2.Cell(2, 4).Value = 120;

            wb.SaveAs(_path);
        }

        var reader = new ItemizadoSpreadsheetReader();
        var summaries = reader.InspectWorksheets(_path);

        Assert.Equal(2, summaries.Count);

        var resumen = summaries.First(s => s.Name == "Resumen");
        Assert.False(resumen.HasHeaderMatch);

        var itemizado = summaries.First(s => s.Name == "Itemizado Electrico");
        Assert.True(itemizado.HasHeaderMatch);
        Assert.True(itemizado.EstimatedItemRows >= 1);

        var explicitTable = reader.ReadWorksheet(_path, "Itemizado Electrico");
        Assert.NotEmpty(explicitTable);
        Assert.Equal("1.1", explicitTable[1][0]);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch
        {
        }
    }
}
