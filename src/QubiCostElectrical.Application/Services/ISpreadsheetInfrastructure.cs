namespace QubiCostElectrical;

public sealed record PurchaseListExcelRequest(
    string ProjectTitle,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows,
    bool MuestraUf = true,
    decimal ValorUfClp = 0m);

public sealed record PurchaseListRfqRequest(
    string ProjectTitle,
    string CompanyName,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows);

public sealed record PurchaseOrderExcelRequest(
    string ProjectTitle,
    string Proveedor,
    string CompanyName,
    decimal ToleranciaPct,
    IReadOnlyList<CompraMaterialViewModel> Rows,
    bool MuestraUf = false,
    decimal ValorUfClp = 0m,
    string? NumeroOc = null,
    string? Observaciones = null);

public interface IPurchaseListExcelExporter
{
    void Export(string path, PurchaseListExcelRequest request);
    void ExportRfq(string path, PurchaseListRfqRequest request);
    void ExportPurchaseOrder(string path, PurchaseOrderExcelRequest request);
}
