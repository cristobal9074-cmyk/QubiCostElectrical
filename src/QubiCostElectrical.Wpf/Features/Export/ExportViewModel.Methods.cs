using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed partial class ExportViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public ExportViewModel(WpfAppHost host) : base(host)
    {
        this.host = host;
        refreshCommand = new RelayCommand(Refresh);
        exportOfferPdfCommand = new RelayCommand(async () => await ExportOfferPdfAsync(), CanExport);
        exportExcelCommand = new RelayCommand(async () => await ExportExcelAsync(), CanExport);
        exportLetterCommand = new RelayCommand(async () => await ExportLetterAsync(), CanExport);
        exportMemoriaExcelCommand = new RelayCommand(async () => await ExportMemoriaExcelAsync(), CanExport);
        exportMemoriaPdfCommand = new RelayCommand(async () => await ExportMemoriaPdfAsync(), CanExport);
        exportScTechnicalReportCommand = new RelayCommand(async () => await ExportScTechnicalReportAsync(), CanExport);
        exportJsonCommand = new RelayCommand(async () => await ExportJsonAsync(), CanExport);
        exportPackageCommand = new RelayCommand(async () => await ExportPackageAsync(), CanExport);
        previewOfferCommand = new RelayCommand(async () => await PreviewOfferAsync(), CanPreview);
        previewService = new DeliveryPreviewService(host);
        InitializeOfferWizard();

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);

    partial void InitializeOfferWizard();
    partial void RefreshOfferWizard();
    partial void HookLivePreview();

    public PdfPreviewViewModel PdfPreview => pdfPreview;

    public ObservableCollection<ExportActionVm> Actions => actions;
    public ObservableCollection<ExportPackageItemVm> PackageItems => packageItems;
    public ObservableCollection<string> ScopeModes => scopeModes;
    public ObservableCollection<ExportPartidaOptionVm> PartidaOptions => partidaOptions;
    public ObservableCollection<string> AreaOptions => areaOptions;

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ProjectTitle
    {
        get => projectTitle;
        private set => SetProperty(ref projectTitle, value);
    }

    public string ClientName
    {
        get => clientName;
        private set => SetProperty(ref clientName, value);
    }

    public string StateText
    {
        get => stateText;
        private set => SetProperty(ref stateText, value);
    }

    public string VersionText
    {
        get => versionText;
        private set => SetProperty(ref versionText, value);
    }

    public string ExportReadinessText
    {
        get => exportReadinessText;
        private set => SetProperty(ref exportReadinessText, value);
    }

    public string CorporateThemeText
    {
        get => corporateThemeText;
        private set => SetProperty(ref corporateThemeText, value);
    }

    public string ReportTemplateText
    {
        get => reportTemplateText;
        private set => SetProperty(ref reportTemplateText, value);
    }

    public string DeliveryPackageText
    {
        get => deliveryPackageText;
        private set => SetProperty(ref deliveryPackageText, value);
    }

    public string PreviewStatusText
    {
        get => previewStatusText;
        private set => SetProperty(ref previewStatusText, value);
    }

    public string PreviewFileText
    {
        get => previewFileText;
        private set => SetProperty(ref previewFileText, value);
    }

    public string ExportScopeSummaryText
    {
        get => exportScopeSummaryText;
        private set => SetProperty(ref exportScopeSummaryText, value);
    }

    public string SelectedScopeMode
    {
        get => selectedScopeMode;
        set
        {
            if (!SetProperty(ref selectedScopeMode, value))
                return;

            OnPropertyChanged(nameof(IsPartidaScope));
            OnPropertyChanged(nameof(IsAreaScope));
            UpdateScopeSummary();
        }
    }

    public ExportPartidaOptionVm? SelectedPartida
    {
        get => selectedPartida;
        set
        {
            if (!SetProperty(ref selectedPartida, value))
                return;

            UpdateScopeSummary();
        }
    }

    public string SelectedArea
    {
        get => selectedArea;
        set
        {
            if (!SetProperty(ref selectedArea, value))
                return;

            UpdateScopeSummary();
        }
    }

    public bool IsPartidaScope => string.Equals(SelectedScopeMode, "Partida", StringComparison.OrdinalIgnoreCase);
    public bool IsAreaScope => string.Equals(SelectedScopeMode, ScopeAreaLabel, StringComparison.OrdinalIgnoreCase);

    public decimal TotalUf
    {
        get => totalUf;
        private set => SetProperty(ref totalUf, value);
    }

    public decimal ConfidencePct
    {
        get => confidencePct;
        private set => SetProperty(ref confidencePct, value);
    }

    public int Blockers
    {
        get => blockers;
        private set => SetProperty(ref blockers, value);
    }

    public int Warnings
    {
        get => warnings;
        private set => SetProperty(ref warnings, value);
    }

    public bool PreciosCongelados => host.Session.ProjectInfo.PreciosCongelados;

    public string FreezeBannerTitle => PreciosCongelados
        ? "Precios congelados para la oferta"
        : "Precios sin congelar";

    public string FreezeBannerDetail => PreciosCongelados
        ? $"Congelados el {host.Session.ProjectInfo.FechaCongelamientoPrecios:dd-MM-yyyy} · los cambios del banco de precios no afectan esta oferta."
        : "Los cambios del banco de precios todavía afectan esta oferta. Al exportar se ofrecerá congelarlos.";

    // TotalUf contiene CLP desde la migración (nombre legado)
    public string TotalOfertaDisplay => $"$ {TotalUf:N0} CLP";

    public ICommand RefreshCommand => refreshCommand;
    public ICommand ExportOfferPdfCommand => exportOfferPdfCommand;
    public ICommand ExportExcelCommand => exportExcelCommand;
    public ICommand ExportLetterCommand => exportLetterCommand;
    public ICommand ExportMemoriaExcelCommand => exportMemoriaExcelCommand;
    public ICommand ExportMemoriaPdfCommand => exportMemoriaPdfCommand;
    public ICommand ExportScTechnicalReportCommand => exportScTechnicalReportCommand;
    public ICommand ExportJsonCommand => exportJsonCommand;
    public ICommand ExportPackageCommand => exportPackageCommand;
    public ICommand PreviewOfferCommand => previewOfferCommand;

    public void Refresh()
    {
        var snapshot = host.Session.CreateSnapshot();
        var dashboard = host.Dashboard.BuildPageState(snapshot, host.Session.IsDirty, host.Session.StateVersion);
        var prefs = UserPreferencesStore.Load();
        var partidaOptionsSnapshot = host.Export.BuildPartidaOptions().Select(o => new ExportPartidaOptionVm(o)).ToList();
        var areaOptionsSnapshot = host.Export.BuildAreaOptions();

        ProjectTitle = host.Session.ProjectInfo.Nombre;
        ClientName = host.Session.ProjectInfo.Cliente;
        StateText = string.IsNullOrWhiteSpace(host.Session.ProjectInfo.EstadoPresupuesto) ? "Borrador" : host.Session.ProjectInfo.EstadoPresupuesto;
        VersionText = host.Session.ProjectInfo.OfertaPdf?.RevisionTag ?? "Rev 0";
        TotalUf = dashboard.Cost.TotalVentaUf;
        OnPropertyChanged(nameof(PreciosCongelados));
        OnPropertyChanged(nameof(FreezeBannerTitle));
        OnPropertyChanged(nameof(FreezeBannerDetail));
        OnPropertyChanged(nameof(TotalOfertaDisplay));
        ConfidencePct = dashboard.Alerts.ConfidencePct;
        Blockers = dashboard.Alerts.BlockingCount;
        Warnings = dashboard.Alerts.WarningCount;
        ExportReadinessText = dashboard.Alerts.ReadyToExport ? "Listo para exportar" : "Revisar antes de exportar";
        DeliveryPackageText = "PDF de oferta, Excel interno, carta DOCX, memorias de cubicación y respaldo JSON.";
        if (!host.ProjectWorkspace.HasOpenProject)
            ResetPreviewState();
        SummaryText = host.ProjectWorkspace.HasOpenProject
            ? $"$ {dashboard.Cost.TotalVentaUf:N0} CLP · {dashboard.Alerts.TotalIssues:N0} alertas · {dashboard.Alerts.WarningCount:N0} advertencias"
            : "Sin proyecto abierto.";
        LoadScopeOptions(partidaOptionsSnapshot, areaOptionsSnapshot);
        StatusMessage = host.ProjectWorkspace.HasOpenProject
            ? $"Usa las acciones de exportación para generar documentos finales sin salir del shell. {ExportScopeSummaryText}"
            : "Abre un proyecto para habilitar exportaciones.";

        actions.Clear();
        actions.Add(new ExportActionVm("Oferta comercial", "Genera el PDF de oferta con portada, totales y condiciones.", "Exportar PDF", ExportOfferPdfCommand, true));
        actions.Add(new ExportActionVm("Excel interno", "Exporta el libro de control y trazabilidad para uso interno.", "Exportar Excel", ExportExcelCommand));
        actions.Add(new ExportActionVm("Carta de presentación", "Crea la carta DOCX para acompañar la oferta.", "Exportar DOCX", ExportLetterCommand));
        actions.Add(new ExportActionVm("Memoria cubicación", "Exporta la memoria técnica de cubicación en Excel.", "Excel memoria", ExportMemoriaExcelCommand));
        actions.Add(new ExportActionVm("Memoria PDF", "Exporta la memoria técnica de cubicación en PDF.", "PDF memoria", ExportMemoriaPdfCommand));
        actions.Add(new ExportActionVm("Reporte tecnico C.SEC", "Detalle tecnico de tableros, circuitos, parametros y materiales.", "Excel C.SEC", ExportScTechnicalReportCommand));
        actions.Add(new ExportActionVm("Paquete JSON", "Guarda el snapshot actual como archivo JSON de respaldo.", "Exportar JSON", ExportJsonCommand));
        actions.Add(new ExportActionVm("Paquete final", "Agrupa la oferta y respaldos en un ZIP de exportación.", "Crear ZIP", ExportPackageCommand));

        packageItems.Clear();
        packageItems.Add(new ExportPackageItemVm("PDF de oferta", "Documento principal para el cliente, con portada, totales y condiciones."));
        packageItems.Add(new ExportPackageItemVm("Excel interno", "Libro de control con trazabilidad completa y hojas técnicas."));
        packageItems.Add(new ExportPackageItemVm("Carta DOCX", "Carta de presentación para envío formal."));
        packageItems.Add(new ExportPackageItemVm("Memoria de cubicación", "Detalle técnico para respaldo interno o revisión."));
        packageItems.Add(new ExportPackageItemVm("Reporte tecnico C.SEC", "Memoria tecnica de tableros, circuitos y parametros de servicios comunes."));
        packageItems.Add(new ExportPackageItemVm("Respaldo JSON", "Snapshot exportable del estado actual del proyecto."));
        packageItems.Add(new ExportPackageItemVm("ZIP final", "Paquete consolidado de exportación listo para compartir."));

        _corporateTheme = new ExportCorporateThemeSettings
        {
            Enabled = prefs.CorporateTheme.Enabled,
            Name = prefs.CorporateTheme.Name,
            CompanyName = prefs.CorporateTheme.CompanyName,
            LogoPath = prefs.CorporateTheme.LogoPath,
            PrimaryHex = prefs.CorporateTheme.PrimaryHex,
            AccentHex = prefs.CorporateTheme.AccentHex,
            BackgroundHex = prefs.CorporateTheme.BackgroundHex,
            FontFamily = prefs.CorporateTheme.FontFamily
        };
        _reportTemplate = new ExportReportTemplateSettings
        {
            TemplateName = prefs.ReportTemplate.TemplateName,
            CoverTitle = prefs.ReportTemplate.CoverTitle,
            CoverSubtitle = prefs.ReportTemplate.CoverSubtitle,
            ShowDraftWatermark = prefs.ReportTemplate.ShowDraftWatermark,
            ShowCover = prefs.ReportTemplate.ShowCover,
            ShowExecutiveSummary = prefs.ReportTemplate.ShowExecutiveSummary,
            ShowItemizedDetail = prefs.ReportTemplate.ShowItemizedDetail,
            ShowCubicacionDetail = prefs.ReportTemplate.ShowCubicacionDetail,
            ShowPriceValidity = prefs.ReportTemplate.ShowPriceValidity,
            ShowTermsAndConditions = prefs.ReportTemplate.ShowTermsAndConditions,
            ShowCoverTraceability = prefs.ReportTemplate.ShowCoverTraceability,
            OrderedFields = prefs.ReportTemplate.OrderedFields?.ToList() ?? new()
        };
        CorporateThemeText = string.IsNullOrWhiteSpace(prefs.CorporateTheme.CompanyName)
            ? $"Tema corporativo: {prefs.CorporateTheme.Name}"
            : $"Tema corporativo: {prefs.CorporateTheme.Name} · {prefs.CorporateTheme.CompanyName}";
        ReportTemplateText = string.IsNullOrWhiteSpace(prefs.ReportTemplate.TemplateName)
            ? "Plantilla de informe: Oferta profesional"
            : $"Plantilla de informe: {prefs.ReportTemplate.TemplateName}";

        UpdateScopeSummary();
        RefreshOfferWizard();
        RaiseCanExecuteChanged();
    }

    private void LoadScopeOptions(IReadOnlyList<ExportPartidaOptionVm> partidas, IReadOnlyList<string> areas)
    {
        var previousPartidaId = SelectedPartida?.Id;
        var previousArea = SelectedArea;

        partidaOptions.Clear();
        foreach (var option in partidas)
            partidaOptions.Add(option);

        areaOptions.Clear();
        foreach (var area in areas)
            areaOptions.Add(area);

        SelectedPartida = partidaOptions.FirstOrDefault(x => string.Equals(x.Id, previousPartidaId, StringComparison.OrdinalIgnoreCase))
            ?? partidaOptions.FirstOrDefault(x => !x.IsTitle)
            ?? partidaOptions.FirstOrDefault();

        SelectedArea = areaOptions.FirstOrDefault(x => string.Equals(x, previousArea, StringComparison.OrdinalIgnoreCase))
            ?? areaOptions.FirstOrDefault()
            ?? string.Empty;
    }

    private void UpdateScopeSummary()
    {
        ExportScopeSummaryText = SelectedScopeMode switch
        {
            "Partida" when SelectedPartida != null => $"Alcance parcial: partida {SelectedPartida.Label}.",
            "Partida" => "Alcance parcial: seleccionar una partida.",
            ScopeAreaLabel when !string.IsNullOrWhiteSpace(SelectedArea) => $"Alcance parcial: \u00e1rea {SelectedArea}.",
            ScopeAreaLabel => "Alcance parcial: seleccionar un \u00e1rea.",
            _ => "Exportación completa del proyecto."
        };
    }

    private bool CanExport() => host.ProjectWorkspace.HasOpenProject;

    private bool CanPreview() => host.ProjectWorkspace.HasOpenProject;

    private ExportCorporateThemeSettings _corporateTheme = new();
    private ExportReportTemplateSettings _reportTemplate = new();

    private void RaiseCanExecuteChanged()
    {
        previewOfferCommand.RaiseCanExecuteChanged();
        exportOfferPdfCommand.RaiseCanExecuteChanged();
        exportExcelCommand.RaiseCanExecuteChanged();
        exportLetterCommand.RaiseCanExecuteChanged();
        exportMemoriaExcelCommand.RaiseCanExecuteChanged();
        exportMemoriaPdfCommand.RaiseCanExecuteChanged();
        exportScTechnicalReportCommand.RaiseCanExecuteChanged();
        exportJsonCommand.RaiseCanExecuteChanged();
        exportPackageCommand.RaiseCanExecuteChanged();
    }

    private ExportProjectContext BuildContext()
    {
        var snapshot = host.Session.CreateSnapshot();
        var offerSnapshot = snapshot.Proyecto.OfertaPdf != null
            ? CloneOfferSettings(snapshot.Proyecto.OfertaPdf)
            : new OfferPdfSettings();
        if (OfferSettings != null)
            OfferSettings.CopyTo(offerSnapshot);
        var prefs = UserPreferencesStore.Load();
        LetterheadApplicator.Apply(prefs, offerSnapshot);
        return host.Export.BuildContext(
            ResolveScopeMode(),
            SelectedPartida?.Id,
            SelectedArea,
            offerSnapshot,
            _corporateTheme,
            _reportTemplate);
    }

    private static OfferPdfSettings CloneOfferSettings(OfferPdfSettings src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<OfferPdfSettings>(json) ?? new OfferPdfSettings();
    }

    private ExportScopeMode ResolveScopeMode()
    {
        return SelectedScopeMode switch
        {
            "Partida" => ExportScopeMode.Partida,
            ScopeAreaLabel => ExportScopeMode.Area,
            _ => ExportScopeMode.Complete
        };
    }

    private static string SafeFileName(string value, string fallback)
    {
        var name = ProjectTextValidator.MakeSafeFileName(string.IsNullOrWhiteSpace(value) ? fallback : value);
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private async Task ExportOfferPdfAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("PDF de oferta", $"{SafeFileName(ProjectTitle, "Oferta")}.pdf", "PDF de oferta (*.pdf)|*.pdf");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.PdfExporter.Export(ctx, path));
            WpfDialogService.Info("Exportar PDF", $"Oferta generada en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar PDF", ex);
            WpfDialogService.Error("Exportar PDF", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task PreviewOfferAsync()
    {
        try
        {
            if (!CanPreview())
                return;

            PreviewStatusText = "Generando vista previa temporal de la oferta...";
            var result = await previewService.GeneratePreviewAsync(BuildContext());

            if (result.IsSuccess)
            {
                PreviewFileText = string.IsNullOrWhiteSpace(result.FilePath)
                    ? "Vista previa generada."
                    : $"Archivo temporal: {Path.GetFileName(result.FilePath)}";
                PreviewStatusText = result.OpenResult == FileOpenResult.Success
                    ? "Vista previa abierta correctamente."
                    : "Vista previa generada; el visor externo no respondió como se esperaba.";
            }
            else
            {
                PreviewStatusText = result.ErrorMessage ?? "No se pudo generar la vista previa.";
                PreviewFileText = "Sin vista previa generada.";
                ExportResult.SetPreview(null);
            }
        }
        catch (Exception ex)
        {
            PreviewStatusText = OperationFailureMessages.ToUserMessage(ex);
            PreviewFileText = "Sin vista previa generada.";
            ExportResult.SetPreview(null);
        }
        finally
        {
            previewOfferCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ExportExcelAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Excel interno", $"{SafeFileName(ProjectTitle, "Control")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.ExcelExporter.Export(ctx, path));
            WpfDialogService.Info("Exportar Excel", $"Libro generado en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar Excel", ex);
            WpfDialogService.Error("Exportar Excel", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportLetterAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Carta de presentación", $"{SafeFileName(ProjectTitle, "Carta")}.docx", "Word (*.docx)|*.docx");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.WordLetterExporter.Export(ctx, path));
            WpfDialogService.Info("Exportar DOCX", $"Carta generada en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar DOCX", ex);
            WpfDialogService.Error("Exportar DOCX", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportMemoriaExcelAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Memoria cubicación Excel", $"{SafeFileName(ProjectTitle, "Memoria-Cubicacion")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.CubicacionReportExcelExporter.Export(ctx, path));
            WpfDialogService.Info("Memoria Excel", $"Memoria generada en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Memoria Excel", ex);
            WpfDialogService.Error("Memoria Excel", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportMemoriaPdfAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Memoria cubicación PDF", $"{SafeFileName(ProjectTitle, "Memoria-Cubicacion")}.pdf", "PDF (*.pdf)|*.pdf");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.CubicacionReportPdfExporter.Export(ctx, path));
            WpfDialogService.Info("Memoria PDF", $"Memoria generada en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Memoria PDF", ex);
            WpfDialogService.Error("Memoria PDF", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportScTechnicalReportAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Reporte tecnico C.SEC", $"{SafeFileName(ProjectTitle, "Reporte-CSEC")}.xlsx", "Excel (*.xlsx)|*.xlsx");
            if (path == null) return;
            var ctx = BuildContext();
            await Task.Run(() => host.Services.CubicacionSCReportExcelExporter.Export(ctx, path));
            WpfDialogService.Info("Reporte tecnico C.SEC", $"Reporte generado en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Reporte tecnico C.SEC", ex);
            WpfDialogService.Error("Reporte tecnico C.SEC", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportJsonAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Paquete JSON", $"{SafeFileName(ProjectTitle, "Proyecto")}.json", "JSON (*.json)|*.json");
            if (path == null) return;

            var ctx = BuildContext();
            await host.Services.ProjectRepository.SaveProjectFileAsync(path, ctx.Data, ProjectJsonSerializer.Options);
            WpfDialogService.Info("Exportar JSON", $"Snapshot generado en:{Environment.NewLine}{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Falló la exportación: Exportar JSON", ex);
            WpfDialogService.Error("Exportar JSON", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private async Task ExportPackageAsync()
    {
        try
        {
            if (!await EnsureExportAllowedAsync())
                return;
            var path = PromptSave("Paquete final", $"{SafeFileName(ProjectTitle, "Paquete-Final")}.zip", "Paquete final (*.zip)|*.zip");
            if (path == null) return;

            string tempRoot = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            // La carpeta temporal solo existe para armar el ZIP: sin este try/finally quedaba
            // huérfana en %TEMP%\QubiCostElectrical\ después de CADA exportación (éxito o
            // fallo), acumulando una carpeta con 8 archivos por cada "Paquete final" generado.
            try
            {
                var ctx = BuildContext();
                var baseName = SafeFileName(ProjectTitle, "Proyecto");
                string offerPdf = Path.Combine(tempRoot, $"{baseName}-Oferta.pdf");
                string controlXlsx = Path.Combine(tempRoot, $"{baseName}-Control.xlsx");
                string letterDocx = Path.Combine(tempRoot, $"{baseName}-Carta.docx");
                string memoriaXlsx = Path.Combine(tempRoot, $"{baseName}-Memoria-Cubicacion.xlsx");
                string memoriaPdf = Path.Combine(tempRoot, $"{baseName}-Memoria-Cubicacion.pdf");
                string reporteScXlsx = Path.Combine(tempRoot, $"{baseName}-Reporte-CSEC.xlsx");
                string snapshotJson = Path.Combine(tempRoot, $"{baseName}.json");
                string manifestTxt = Path.Combine(tempRoot, "MANIFEST.txt");

                await Task.Run(() => host.Services.PdfExporter.Export(ctx, offerPdf));
                await Task.Run(() => host.Services.ExcelExporter.Export(ctx, controlXlsx));
                await Task.Run(() => host.Services.WordLetterExporter.Export(ctx, letterDocx));
                await Task.Run(() => host.Services.CubicacionReportExcelExporter.Export(ctx, memoriaXlsx));
                await Task.Run(() => host.Services.CubicacionReportPdfExporter.Export(ctx, memoriaPdf));
                await Task.Run(() => host.Services.CubicacionSCReportExcelExporter.Export(ctx, reporteScXlsx));
                await host.Services.ProjectRepository.SaveProjectFileAsync(snapshotJson, ctx.Data, ProjectJsonSerializer.Options);

                await File.WriteAllTextAsync(manifestTxt,
                    $"Proyecto: {ProjectTitle}{Environment.NewLine}" +
                    $"Cliente: {ClientName}{Environment.NewLine}" +
                    $"Estado: {StateText}{Environment.NewLine}" +
                    $"Revisión: {VersionText}{Environment.NewLine}" +
                    $"Total CLP: $ {TotalUf:N0}{Environment.NewLine}" +
                    $"Exportado: {AppClock.Now:yyyy-MM-dd HH:mm:ss}",
                    System.Text.Encoding.UTF8);

                if (File.Exists(path))
                    File.Delete(path);

                ZipFile.CreateFromDirectory(tempRoot, path, CompressionLevel.Optimal, includeBaseDirectory: false);
                WpfDialogService.Info("Paquete final", $"ZIP generado en:{Environment.NewLine}{path}");
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

    private static string? PromptSave(string title, string defaultName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultName,
            DefaultExt = Path.GetExtension(defaultName),
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void ResetPreviewState()
    {
        ExportResult.SetPreview(null);
        PreviewStatusText = host.ProjectWorkspace.HasOpenProject
            ? "Vista previa de oferta lista para revisar antes de exportar."
            : "Abre un proyecto para ver la oferta antes de exportar.";
        PreviewFileText = "Sin vista previa generada.";
    }
}



