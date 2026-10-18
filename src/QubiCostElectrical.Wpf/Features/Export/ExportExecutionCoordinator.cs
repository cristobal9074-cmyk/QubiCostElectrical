using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed record ExportPackageMetadata(
    string ProjectTitle,
    string ClientName,
    string StateText,
    string VersionText,
    decimal TotalClp,
    decimal TotalUf,
    bool MuestraUf);

/// <summary>
/// Orquesta la ejecución de exportación de documentos individuales, paquetes ZIP completos
/// y entrega de ofertas para el módulo de Exportación.
/// </summary>
public sealed class ExportExecutionCoordinator
{
    private readonly IFileDialogService fileDialog;
    private readonly IFileOpenService fileOpener;
    private readonly IPdfProjectExporter pdfExporter;
    private readonly IExcelProjectExporter excelExporter;
    private readonly IPresentationLetterDocxExporter wordLetterExporter;
    private readonly ICubicacionReportExcelExporter cubReportExcel;
    private readonly ICubicacionReportPdfExporter cubReportPdf;
    private readonly ICubicacionSCReportExcelExporter cubScReportExcel;
    private readonly IProjectRepository projectRepo;

    public ExportExecutionCoordinator(
        IFileDialogService fileDialog,
        IFileOpenService fileOpener,
        IPdfProjectExporter pdfExporter,
        IExcelProjectExporter excelExporter,
        IPresentationLetterDocxExporter wordLetterExporter,
        ICubicacionReportExcelExporter cubReportExcel,
        ICubicacionReportPdfExporter cubReportPdf,
        ICubicacionSCReportExcelExporter cubScReportExcel,
        IProjectRepository projectRepo)
    {
        this.fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
        this.fileOpener = fileOpener ?? throw new ArgumentNullException(nameof(fileOpener));
        this.pdfExporter = pdfExporter ?? throw new ArgumentNullException(nameof(pdfExporter));
        this.excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
        this.wordLetterExporter = wordLetterExporter ?? throw new ArgumentNullException(nameof(wordLetterExporter));
        this.cubReportExcel = cubReportExcel ?? throw new ArgumentNullException(nameof(cubReportExcel));
        this.cubReportPdf = cubReportPdf ?? throw new ArgumentNullException(nameof(cubReportPdf));
        this.cubScReportExcel = cubScReportExcel ?? throw new ArgumentNullException(nameof(cubScReportExcel));
        this.projectRepo = projectRepo ?? throw new ArgumentNullException(nameof(projectRepo));
    }

    public string? PromptSave(string title, string defaultName, string filter) =>
        fileDialog.PromptSaveFile(title, defaultName, filter);

    public string? PickImage() =>
        fileDialog.PromptOpenFile("Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Todos|*.*");

    public void OpenPreviewFolder(string? path)
    {
        if (path == null)
            return;

        var folder = Path.GetDirectoryName(path);
        if (folder != null)
            fileOpener.OpenFolder(folder);
    }

    public void ShowExportSuccessWithFolderAction(string title, string message, string path, bool pathIsFolder = false)
    {
        string? folder = pathIsFolder
            ? path
            : Path.GetDirectoryName(path);

        ShellNotificationService.Instance.ShowSuccess(
            title,
            message,
            actionLabel: string.IsNullOrWhiteSpace(folder) ? null : "Abrir carpeta",
            action: string.IsNullOrWhiteSpace(folder) ? null : () => fileOpener.OpenFolder(folder));
    }

    private static string SafeFileName(string value, string fallback)
    {
        var name = ProjectTextValidator.MakeSafeFileName(string.IsNullOrWhiteSpace(value) ? fallback : value);
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    public async Task ExportOfferPdfAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("PDF de oferta", $"{SafeFileName(projectTitle, "Oferta")}.pdf", "PDF de oferta (*.pdf)|*.pdf");
            if (path == null) return;
            await Task.Run(() => pdfExporter.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Exportar PDF", $"Oferta generada en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar PDF", ex);
            ShellNotificationService.Instance.ShowError("Exportar PDF", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportExcelAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Excel interno", $"{SafeFileName(projectTitle, "Control")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            await Task.Run(() => excelExporter.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Exportar Excel", $"Libro generado en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar Excel", ex);
            ShellNotificationService.Instance.ShowError("Exportar Excel", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportLetterAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Carta de presentación", $"{SafeFileName(projectTitle, "Carta")}.docx", "Word (*.docx)|*.docx");
            if (path == null) return;
            await Task.Run(() => wordLetterExporter.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Exportar DOCX", $"Carta generada en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar DOCX", ex);
            WpfDialogService.Error("Exportar DOCX", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportMemoriaExcelAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Memoria cubicación Excel", $"{SafeFileName(projectTitle, "Memoria-Cubicacion")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            await Task.Run(() => cubReportExcel.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Memoria Excel", $"Memoria generada en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Memoria Excel", ex);
            WpfDialogService.Error("Memoria Excel", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportMemoriaPdfAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Memoria cubicación PDF", $"{SafeFileName(projectTitle, "Memoria-Cubicacion")}.pdf", "PDF (*.pdf)|*.pdf");
            if (path == null) return;
            await Task.Run(() => cubReportPdf.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Memoria PDF", $"Memoria generada en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Memoria PDF", ex);
            WpfDialogService.Error("Memoria PDF", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportScTechnicalReportAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Reporte tecnico C.SEC", $"{SafeFileName(projectTitle, "Reporte-CSEC")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            await Task.Run(() => cubScReportExcel.Export(ctx, path));
            ShowExportSuccessWithFolderAction("Reporte tecnico C.SEC", $"Reporte generado en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Reporte tecnico C.SEC", ex);
            WpfDialogService.Error("Reporte tecnico C.SEC", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportJsonAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, string projectTitle)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Paquete JSON", $"{SafeFileName(projectTitle, "Proyecto")}.json", "JSON (*.json)|*.json");
            if (path == null) return;

            await projectRepo.SaveProjectFileAsync(path, ctx.Data, ProjectJsonSerializer.Options);
            ShowExportSuccessWithFolderAction("Exportar JSON", $"Snapshot generado en:{Environment.NewLine}{path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar JSON", ex);
            WpfDialogService.Error("Exportar JSON", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task ExportPackageAsync(Func<Task<bool>> ensureAllowed, ExportProjectContext ctx, ExportPackageMetadata metadata)
    {
        try
        {
            if (!await ensureAllowed())
                return;
            var path = PromptSave("Paquete final", $"{SafeFileName(metadata.ProjectTitle, "Paquete-Final")}.zip", "Paquete final (*.zip)|*.zip");
            if (path == null) return;

            string tempRoot = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var baseName = SafeFileName(metadata.ProjectTitle, "Proyecto");
                string offerPdf = Path.Combine(tempRoot, $"{baseName}-Oferta.pdf");
                string controlXlsx = Path.Combine(tempRoot, $"{baseName}-Control.xlsx");
                string letterDocx = Path.Combine(tempRoot, $"{baseName}-Carta.docx");
                string memoriaXlsx = Path.Combine(tempRoot, $"{baseName}-Memoria-Cubicacion.xlsx");
                string memoriaPdf = Path.Combine(tempRoot, $"{baseName}-Memoria-Cubicacion.pdf");
                string reporteScXlsx = Path.Combine(tempRoot, $"{baseName}-Reporte-CSEC.xlsx");
                string snapshotJson = Path.Combine(tempRoot, $"{baseName}.json");
                string manifestTxt = Path.Combine(tempRoot, "MANIFEST.txt");

                await Task.Run(() => pdfExporter.Export(ctx, offerPdf));
                await Task.Run(() => excelExporter.Export(ctx, controlXlsx));
                await Task.Run(() => wordLetterExporter.Export(ctx, letterDocx));
                await Task.Run(() => cubReportExcel.Export(ctx, memoriaXlsx));
                await Task.Run(() => cubReportPdf.Export(ctx, memoriaPdf));
                await Task.Run(() => cubScReportExcel.Export(ctx, reporteScXlsx));
                await projectRepo.SaveProjectFileAsync(snapshotJson, ctx.Data, ProjectJsonSerializer.Options);

                await File.WriteAllTextAsync(manifestTxt,
                    $"Proyecto: {metadata.ProjectTitle}{Environment.NewLine}" +
                    $"Cliente: {metadata.ClientName}{Environment.NewLine}" +
                    $"Estado: {metadata.StateText}{Environment.NewLine}" +
                    $"Revisión: {metadata.VersionText}{Environment.NewLine}" +
                    $"Total CLP: $ {metadata.TotalClp:N0}{Environment.NewLine}" +
                    (metadata.MuestraUf ? $"Total UF: {metadata.TotalUf:N3} UF{Environment.NewLine}" : "") +
                    $"Exportado: {AppClock.Now:yyyy-MM-dd HH:mm:ss}",
                    System.Text.Encoding.UTF8);

                if (File.Exists(path))
                    File.Delete(path);

                ZipFile.CreateFromDirectory(tempRoot, path, CompressionLevel.Optimal, includeBaseDirectory: false);
                ShowExportSuccessWithFolderAction("Paquete final", $"ZIP generado en:{Environment.NewLine}{path}", path);
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); }
                catch (Exception ex) { AppLog.Error("Export", "No se pudo limpiar la carpeta temporal del paquete final", ex); }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Paquete final", ex);
            WpfDialogService.Error("Paquete final", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public async Task DeliverPackageAsync(
        Func<Task<bool>> ensureAllowed,
        DeliveryPreviewService previewService,
        DeliveryDocumentSelectorVm documentSelector,
        DeliveryExportResultVm exportResult,
        ObservableCollection<string> documentErrors,
        BudgetProjectSession session,
        string projectTitle,
        Action saveOfferSettings)
    {
        if (!await ensureAllowed())
            return;

        var folder = fileDialog.PromptFolder("Selecciona la carpeta de destino para la exportación");
        if (folder == null)
            return;

        try
        {
            saveOfferSettings();
            var ctx = documentSelector.GetSelectedContext(session);
            var selectedIds = documentSelector.GetSelectedDocumentIds();
            var baseName = ProjectTextValidator.MakeSafeFileName(projectTitle);
            var results = await previewService.ExportAllAsync(ctx, folder, selectedIds, baseName);
            exportResult.SetResults(results);

            documentErrors.Clear();
            foreach (var fail in results.Where(r => !r.IsSuccess))
                documentErrors.Add($"{fail.DocumentType}: {fail.ErrorMessage}");

            var successCount = results.Count(r => r.IsSuccess);
            var failCount = results.Count(r => !r.IsSuccess);
            if (failCount == 0)
                ShowExportSuccessWithFolderAction(
                    "Oferta y exportación",
                    $"Todos los documentos ({successCount}) se generaron correctamente en:{Environment.NewLine}{folder}",
                    folder,
                    pathIsFolder: true);
            else
                ShellNotificationService.Instance.ShowWarning("Oferta y exportación", $"{successCount} documento(s) generado(s), {failCount} con errores.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló el asistente de oferta y exportación", ex);
            WpfDialogService.Error("Oferta y exportación", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
