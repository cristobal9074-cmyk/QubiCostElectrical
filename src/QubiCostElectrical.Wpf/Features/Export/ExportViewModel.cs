using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed class ExportActionVm
{
    public ExportActionVm(string title, string description, string buttonText, ICommand command, bool primary = false, string iconText = "")
    {
        Title = title;
        Description = description;
        ButtonText = buttonText;
        Command = command;
        Primary = primary;
        IconText = string.IsNullOrWhiteSpace(iconText) ? DeriveIcon(buttonText) : iconText;
        IconBackground = IconText switch
        {
            "PDF" => ThemeOrFallback("Brush.BlueSoft", "#EAF1FF"),
            "XLS" => ThemeOrFallback("Brush.GreenSoft", "#EAF8EF"),
            "DOC" => ThemeOrFallback("Brush.AmberSoft", "#FFF5DD"),
            _     => ThemeOrFallback("Brush.Canvas", "#F3F5F8")
        };
        IconForeground = IconText switch
        {
            "PDF" => ThemeOrFallback("Brush.Blue", "#2563EB"),
            "XLS" => ThemeOrFallback("Brush.Green", "#15803D"),
            "DOC" => ThemeOrFallback("Brush.Amber", "#B45309"),
            _     => ThemeOrFallback("Brush.Ink3", "#66758A")
        };
    }

    public string Title { get; }
    public string Description { get; }
    public string ButtonText { get; }
    public ICommand Command { get; }
    public bool Primary { get; }
    public string IconText { get; }
    public Brush IconBackground { get; }
    public Brush IconForeground { get; }

    private static Brush ThemeOrFallback(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }

    private static string DeriveIcon(string buttonText) => buttonText.ToUpperInvariant() switch
    {
        var s when s.Contains("PDF") => "PDF",
        var s when s.Contains("EXCEL") || s.Contains("XLS") => "XLS",
        var s when s.Contains("DOCX") || s.Contains("DOC") => "DOC",
        var s when s.Contains("ZIP") => "ZIP",
        _ => "EXP"
    };
}

public sealed class ExportPackageItemVm
{
    public ExportPackageItemVm(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }
    public string Description { get; }
}

public sealed partial class ExportViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private const string ScopeAreaLabel = "\u00c1rea";

    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly IPermissionService permissions;
    private readonly DashboardPresenter dashboard;
    private readonly ExportPresenter exportPresenter;
    private readonly ProjectAuditRecorder audit;
    private readonly IFileDialogService fileDialog;
    private readonly IFileOpenService fileOpener;
    private readonly IPdfProjectExporter pdfExporter;
    private readonly IExcelProjectExporter excelExporter;
    private readonly IPresentationLetterDocxExporter wordLetterExporter;
    private readonly ICubicacionReportExcelExporter cubReportExcel;
    private readonly ICubicacionReportPdfExporter cubReportPdf;
    private readonly ICubicacionSCReportExcelExporter cubScReportExcel;
    private readonly IProjectRepository projectRepo;
    private readonly IShellNavigationHost? shell;
    private readonly ObservableCollection<ExportActionVm> actions = new();
    private readonly ObservableCollection<ExportPackageItemVm> packageItems = new();
    private readonly ObservableCollection<string> scopeModes = new() { "Completo", "Partida", ScopeAreaLabel };
    private readonly ObservableCollection<ExportPartidaOptionVm> partidaOptions = new();
    private readonly ObservableCollection<string> areaOptions = new();
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand exportOfferPdfCommand;
    private readonly RelayCommand exportExcelCommand;
    private readonly RelayCommand exportLetterCommand;
    private readonly RelayCommand exportMemoriaExcelCommand;
    private readonly RelayCommand exportMemoriaPdfCommand;
    private readonly RelayCommand exportScTechnicalReportCommand;
    private readonly RelayCommand exportJsonCommand;
    private readonly RelayCommand exportPackageCommand;
    private readonly RelayCommand previewOfferCommand;
    private readonly DeliveryPreviewService previewService;
    private readonly PdfPreviewViewModel pdfPreview = new();

    private string summaryText = "Sin proyecto abierto.";
    private string statusMessage = "El paquete de exportación aparece aquí cuando hay un proyecto cargado.";
    private string projectTitle = "Proyecto sin nombre";
    private string clientName = "Cliente sin nombre";
    private string stateText = "Borrador";
    private string versionText = "Rev 0";
    private string exportReadinessText = "Bloqueado";
    private string corporateThemeText = "Tema corporativo: Corporativo";
    private string reportTemplateText = "Plantilla de informe: Oferta profesional";
    private string deliveryPackageText = "PDF de oferta, Excel interno, carta DOCX, memorias de cubicación y respaldo JSON.";
    private string exportScopeSummaryText = "Exportación completa del proyecto.";
    private string previewStatusText = "Vista previa de oferta lista para revisar antes de exportar.";
    private string previewFileText = "Sin vista previa generada.";
    private string selectedScopeMode = "Completo";
    private ExportPartidaOptionVm? selectedPartida;
    private string selectedArea = "";
    private decimal totalUf;
    private decimal confidencePct;
    private int blockers;
    private int warnings;
    private bool showEmptyState;
    private bool isLoading;
}
