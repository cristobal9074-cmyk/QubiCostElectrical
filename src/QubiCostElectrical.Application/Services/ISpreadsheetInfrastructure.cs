namespace QubiCostElectrical;

public sealed record MaterialSpreadsheetRow(
    string Codigo,
    string Descripcion,
    string Unidad,
    decimal MatValue,
    decimal MoValue);

public interface IMaterialSpreadsheetImporter
{
    IReadOnlyList<MaterialSpreadsheetRow> ReadRows(string filePath);
}

public interface ITemplateSpreadsheetService
{
    void Export(string path, IReadOnlyList<TemplateRow> templates, string sourceProject, string sourceClient);
    List<TemplateRow> Import(string path);
}

public sealed record PurchaseListExcelRequest(
    string ProjectTitle,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows);

public interface IPurchaseListExcelExporter
{
    void Export(string path, PurchaseListExcelRequest request);
}

public sealed record CubicacionSCExcelRow(
    string Categoria,
    string Material,
    decimal Cantidad,
    string Unidad,
    decimal UfUnit,
    decimal UfTotal);

public interface ICubicacionSCExcelExporter
{
    void Export(string path, string projectTitle, IReadOnlyList<CubicacionSCExcelRow> rows);
}
