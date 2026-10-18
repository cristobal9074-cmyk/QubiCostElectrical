using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

public sealed class ItemizadoSpreadsheetReader : IItemizadoSpreadsheetReader
{
    public IReadOnlyList<IReadOnlyList<string>> ReadFirstWorksheet(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Ruta de archivo requerida.", nameof(filePath));

        return SpreadsheetOperationGuards.Execute(
            ProjectOperationKind.ExcelImport,
            () => ReadBestWorksheetCore(filePath),
            "No se pudo leer el Excel. Verifica que el archivo no esté abierto en otra aplicación y que tenga al menos una hoja con datos.");
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadBestWorksheetCore(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);

        IXLWorksheet? bestSheet = null;
        int bestScore = -1;
        IXLWorksheet? fallbackSheet = null;
        int fallbackRowCount = -1;

        foreach (var worksheet in workbook.Worksheets)
        {
            int rowEstimate = worksheet.RangeUsed()?.RowCount() ?? 0;
            if (rowEstimate == 0)
                continue;

            if (fallbackSheet == null || rowEstimate > fallbackRowCount)
            {
                fallbackSheet = worksheet;
                fallbackRowCount = rowEstimate;
            }

            int score = ScoreWorksheetForItemizado(worksheet);
            if (score > bestScore)
            {
                bestScore = score;
                bestSheet = worksheet;
            }
        }

        if (bestSheet != null)
            return ReadWorksheetTable(bestSheet);

        if (fallbackSheet != null)
            return ReadWorksheetTable(fallbackSheet);

        throw new InvalidOperationException("No se encontró ninguna hoja con datos en el Excel.");
    }

    internal static int ScoreWorksheetForItemizado(IXLWorksheet worksheet)
    {
        var sample = ReadWorksheetTable(worksheet, maxRows: 80);
        if (sample.Count == 0)
            return -1;

        int headerRow = ItemizadoImportParser.FindHeaderRow(sample, "itemizado");
        if (headerRow < 0)
            return -1;

        int estimatedRows = worksheet.RangeUsed()?.RowCount() ?? sample.Count;
        return Math.Max(0, estimatedRows - headerRow);
    }

    private static List<List<string>> ReadWorksheetTable(IXLWorksheet worksheet, int? maxRows = null)
    {
        var range = worksheet.RangeUsed();
        if (range == null)
            return [];

        var table = new List<List<string>>();
        int rowLimit = maxRows ?? int.MaxValue;
        int count = 0;

        foreach (var row in range.RowsUsed())
        {
            if (count >= rowLimit)
                break;

            var values = new List<string>();
            foreach (var cell in row.Cells(1, range.ColumnCount()))
                values.Add(cell.GetFormattedString().Trim());
            table.Add(values);
            count++;
        }

        return table;
    }
}
