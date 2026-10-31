using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ExcelSheetSummary(
    string Name,
    int RowCount,
    int ColumnCount,
    bool HasHeaderMatch,
    int EstimatedItemRows,
    IReadOnlyList<IReadOnlyList<string>> SampleRows);

public interface IItemizadoSpreadsheetReader
{
    IReadOnlyList<IReadOnlyList<string>> ReadFirstWorksheet(string filePath);
    IReadOnlyList<ExcelSheetSummary> InspectWorksheets(string filePath);
    IReadOnlyList<IReadOnlyList<string>> ReadWorksheet(string filePath, string sheetName);
}
