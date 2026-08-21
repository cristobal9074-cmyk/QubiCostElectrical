using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Delivery;

public sealed class DeliveryExportResult
{
    public DeliveryExportResult(string documentType, bool isSuccess, string? filePath = null, string? errorMessage = null, FileOpenResult openResult = FileOpenResult.Success)
    {
        DocumentType = documentType;
        IsSuccess = isSuccess;
        FilePath = filePath;
        ErrorMessage = errorMessage;
        OpenResult = openResult;
    }

    public string DocumentType { get; }
    public bool IsSuccess { get; }
    public string? FilePath { get; }
    public string? ErrorMessage { get; }
    public FileOpenResult OpenResult { get; }
}

public sealed class DeliveryPreviewService
{
    private readonly WpfAppHost host;
    private readonly IFileOpenService fileOpener;

    public DeliveryPreviewService(WpfAppHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        fileOpener = host.FileOpener;
    }

    public async Task<DeliveryExportResult> GeneratePreviewAsync(ExportProjectContext ctx, bool openExternally = true)
    {
        CleanupOldPreviews();

        string tempRoot = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", "Preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        string previewPath = Path.Combine(tempRoot, "Preview-Oferta.pdf");
        await Task.Run(() => host.Services.PdfExporter.Export(ctx, previewPath));

        if (File.Exists(previewPath))
        {
            var openResult = openExternally ? fileOpener.OpenFile(previewPath) : FileOpenResult.Success;
            return new DeliveryExportResult("OfferPdf", true, previewPath, openResult: openResult);
        }

        return new DeliveryExportResult("OfferPdf", false, errorMessage: "El archivo no se generó.");
    }

    public async Task<IReadOnlyList<DeliveryExportResult>> ExportAllAsync(ExportProjectContext ctx, string targetFolder, string[] selectedDocumentIds, string baseName)
    {
        var results = new List<DeliveryExportResult>();

        foreach (var docId in selectedDocumentIds)
        {
            var result = await ExportSingleAsync(docId, ctx, targetFolder, baseName);
            results.Add(result);
        }

        return results;
    }

    private async Task<DeliveryExportResult> ExportSingleAsync(string docId, ExportProjectContext ctx, string targetFolder, string baseName)
    {
        try
        {
            return docId switch
            {
                "OfferPdf" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Oferta.pdf",
                    (c, p) => host.Services.PdfExporter.Export(c, p)),
                "ControlExcel" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Control.xlsx",
                    (c, p) => host.Services.ExcelExporter.Export(c, p)),
                "LetterDocx" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Carta.docx",
                    (c, p) => host.Services.WordLetterExporter.Export(c, p)),
                "MemoriaExcel" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Memoria-Cubicacion.xlsx",
                    (c, p) => host.Services.CubicacionReportExcelExporter.Export(c, p)),
                "MemoriaPdf" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Memoria-Cubicacion.pdf",
                    (c, p) => host.Services.CubicacionReportPdfExporter.Export(c, p)),
                "ReporteCSEC" => await ExportFileAsync(ctx, targetFolder, $"{baseName}-Reporte-CSEC.xlsx",
                    (c, p) => host.Services.CubicacionSCReportExcelExporter.Export(c, p)),
                _ => new DeliveryExportResult(docId, false, errorMessage: "No se reconoció el tipo de documento solicitado.")
            };
        }
        catch (Exception ex)
        {
            return new DeliveryExportResult(docId, false, errorMessage: OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public static void CleanupOldPreviews()
    {
        try
        {
            var previewDir = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", "Preview");
            if (!Directory.Exists(previewDir))
                return;

            var cutoff = DateTime.UtcNow.AddHours(-24);

            foreach (var dir in Directory.GetDirectories(previewDir))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.CreationTimeUtc < cutoff)
                        Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeliveryPreviewService] No se pudo eliminar preview antigua {dir}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeliveryPreviewService] Error en limpieza de previews: {ex.Message}");
        }
    }

    private async Task<DeliveryExportResult> ExportFileAsync(ExportProjectContext ctx, string folder, string fileName, Action<ExportProjectContext, string> exportAction)
    {
        var path = Path.Combine(folder, fileName);

        await Task.Run(() => exportAction(ctx, path));

        if (!File.Exists(path))
            return new DeliveryExportResult(fileName, false, errorMessage: "El archivo no se generó.");

        return new DeliveryExportResult(fileName, true, path);
    }
}
