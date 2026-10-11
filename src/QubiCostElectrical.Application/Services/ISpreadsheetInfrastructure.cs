namespace QubiCostElectrical;

public sealed record PurchaseListExcelRequest(
    string ProjectTitle,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows);

public sealed record PurchaseListRfqRequest(
    string ProjectTitle,
    string CompanyName,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows);

public interface IPurchaseListExcelExporter
{
    void Export(string path, PurchaseListExcelRequest request);
    void ExportRfq(string path, PurchaseListRfqRequest request);
}
