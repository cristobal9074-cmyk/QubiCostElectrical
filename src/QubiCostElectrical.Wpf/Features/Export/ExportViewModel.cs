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

public sealed class ExportActionVm
{
    private static readonly System.Windows.Media.Brush BgBlue   = MakeFrozen("#EAF1FF");
    private static readonly System.Windows.Media.Brush BgGreen  = MakeFrozen("#EAF8EF");
    private static readonly System.Windows.Media.Brush BgAmber  = MakeFrozen("#FFF5DD");
    private static readonly System.Windows.Media.Brush BgCanvas = MakeFrozen("#F3F5F8");
    private static readonly System.Windows.Media.Brush FgBlue   = MakeFrozen("#2563EB");
    private static readonly System.Windows.Media.Brush FgGreen  = MakeFrozen("#15803D");
    private static readonly System.Windows.Media.Brush FgAmber  = MakeFrozen("#B45309");
    private static readonly System.Windows.Media.Brush FgSlate  = MakeFrozen("#66758A");

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
            "PDF" => BgBlue,
            "XLS" => BgGreen,
            "DOC" => BgAmber,
            _     => BgCanvas
        };
        IconForeground = IconText switch
        {
            "PDF" => FgBlue,
            "XLS" => FgGreen,
            "DOC" => FgAmber,
            _     => FgSlate
        };
    }

    public string Title { get; }
    public string Description { get; }
    public string ButtonText { get; }
    public ICommand Command { get; }
    public bool Primary { get; }
    public string IconText { get; }
    public System.Windows.Media.Brush IconBackground { get; }
    public System.Windows.Media.Brush IconForeground { get; }

    private static System.Windows.Media.Brush MakeFrozen(string hex)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
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

    private readonly WpfAppHost host;
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
}
