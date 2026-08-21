namespace QubiCostElectrical;

public interface IItemizadoSpreadsheetReader
{
    IReadOnlyList<IReadOnlyList<string>> ReadFirstWorksheet(string filePath);
}
