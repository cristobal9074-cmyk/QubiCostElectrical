using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed class VersionRowVm
{
    public VersionRowVm(BudgetVersionRow source, bool isActive, decimal deltaUf)
    {
        Source = source;
        IsActive = isActive;
        DeltaUf = deltaUf;

        if (source.Estado == "Aprobada")
        {
            bool ok = WpfAppHost.Current.Services.VersionWorkflow.VerificarIntegridad(source);
            IntegrityStatusText = ok ? "✅ Integridad válida" : "⚠️ ALTERADO";
        }
        else
        {
            IntegrityStatusText = "—";
        }
    }

    public BudgetVersionRow Source { get; }
    public bool IsActive { get; }
    public decimal DeltaUf { get; }
    public string Codigo => Source.Codigo;
    public string Nombre => Source.Nombre;
    public string Estado => Source.Estado;
    public string CreadoPor => Source.CreadoPor;
    public DateTime FechaCreacion => Source.FechaCreacion;
    public DateTime FechaModificacion => Source.FechaModificacion;
    public decimal MontoVentaUf => Source.MontoVentaUf;
    public string DeltaText => DeltaUf == 0m ? "—" : $"{(DeltaUf > 0m ? "+" : string.Empty)}{DeltaUf:N3}";
    public string DeltaKind => DeltaUf > 0m ? "Aumenta" : DeltaUf < 0m ? "Reduce" : "Neutro";
    public string RevBadge => string.IsNullOrWhiteSpace(Codigo) ? "—" : Codigo.Length <= 3 ? Codigo : Codigo[^3..];

    public bool IsDraft => string.Equals(Estado, "Borrador", StringComparison.OrdinalIgnoreCase) || string.Equals(Estado, "Preliminar", StringComparison.OrdinalIgnoreCase);
    public bool IsPendingReview => string.Equals(Estado, "Pendiente de revisión", StringComparison.OrdinalIgnoreCase);
    public bool IsInReview => string.Equals(Estado, "En revisión", StringComparison.OrdinalIgnoreCase);
    public bool IsApproved => string.Equals(Estado, "Aprobada", StringComparison.OrdinalIgnoreCase);
    public bool IsRejected => string.Equals(Estado, "Rechazada", StringComparison.OrdinalIgnoreCase);
    public bool IsObsolete => string.Equals(Estado, "Obsoleta", StringComparison.OrdinalIgnoreCase);
    public string IntegrityStatusText { get; }
}

public sealed class VersionComparisonRowVm
{
    public VersionComparisonRowVm(BudgetVersionComparisonRow source)
    {
        Source = source;
    }

    public BudgetVersionComparisonRow Source { get; }
    public string ItemId => Source.ItemId;
    public string Item => Source.Item;
    public string Descripcion => Source.Descripcion;
    public decimal BaselineUf => Source.BaselineUf;
    public decimal CurrentUf => Source.CurrentUf;
    public decimal DeltaUf => Source.DeltaUf;
    public decimal DeltaPct => Source.DeltaPct;
    public string Estado => Source.Estado;

    public bool EsTitulo => Source.EsTitulo;
    public int NivelJerarquico => Source.NivelJerarquico;
    public decimal BaselineQty => Source.BaselineQty;
    public decimal CurrentQty => Source.CurrentQty;
    public decimal BaselinePriceUf => Source.BaselinePriceUf;
    public decimal CurrentPriceUf => Source.CurrentPriceUf;
    public bool CambióCantidad => Source.CambióCantidad;
    public bool CambióPrecio => Source.CambióPrecio;
    public bool CambióMateriales => Source.CambióMateriales;
    public bool CambióManoObra => Source.CambióManoObra;
    public bool CambióEquipos => Source.CambióEquipos;
    public string DetalleCambios => Source.DetalleCambios;

    public System.Windows.Thickness IndentMargin => new System.Windows.Thickness(NivelJerarquico * 16, 0, 0, 0);
}

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly WpfAppHost host;
    private readonly IShellNavigationHost shell;
    private readonly IVersionComparisonService versionComparison;
    private readonly IVersionWorkflowService versionWorkflow;
    private readonly ObservableCollection<VersionRowVm> versions = new();
    private readonly ObservableCollection<VersionComparisonRowVm> comparisonRows = new();
    private readonly ObservableCollection<BudgetVersionStatusHistoryRow> selectedVersionHistory = new();
    private readonly ICollectionView versionsView;
    private readonly ICollectionView comparisonRowsView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand newCommand;
    private readonly RelayCommand activateCommand;
    private readonly RelayCommand renameCommand;
    private readonly RelayCommand deleteCommand;
    private readonly RelayCommand refreshComparisonCommand;
    private readonly RelayCommand goToItemCommand;
    private readonly RelayCommand exportComparisonCommand;
    private readonly RelayCommand enviarARevisionCommand;
    private readonly RelayCommand iniciarRevisionCommand;
    private readonly RelayCommand aprobarVersionCommand;
    private readonly RelayCommand rechazaronVersionCommand;

    private VersionRowVm? selectedVersion;
    private VersionRowVm? selectedComparisonVersion;
    private VersionComparisonRowVm? selectedComparisonRow;
    private string comentarioReview = "";
    private bool compareAgainstCurrent = true;
    private bool showEmptyState = true;
    private string summaryText = "0 versiones";
    private string activeVersionText = "Sin versión activa";
    private string statusMessage = "Gestión de versiones no calculada.";
    private string comparisonSummaryText = "Sin comparación";
    private string comparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
    private string comparisonTotalsText = "—";
    private string comparisonAddedText = "0 nuevas";
    private string comparisonChangedText = "0 modificadas";
    private string comparisonRemovedText = "0 eliminadas";
    private decimal comparisonDeltaUf;
    private decimal comparisonDeltaPct;
    private string comparisonModeHint = "Versión A = seleccionada · Versión B = estado actual";
    private bool canCurrentUserApprove;
    private string approvalBlockReason = "";
}
