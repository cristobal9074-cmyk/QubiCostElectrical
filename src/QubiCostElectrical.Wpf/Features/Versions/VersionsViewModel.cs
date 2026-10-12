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
    private readonly VersionsCurrencyFormat? currency;

    public VersionRowVm(BudgetVersionRow source, bool isActive, decimal deltaUf, VersionsCurrencyFormat? currency = null)
    {
        Source = source;
        IsActive = isActive;
        DeltaUf = deltaUf;
        this.currency = currency;
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

    // MontoVentaUf/DeltaUf son pesos chilenos (nombre legado). Con el valor de moneda del
    // proyecto se muestra "1.234 UF" o "$ 48.000"; sin contexto (tests) se mantiene el N3.
    public string MontoVentaText => currency is null
        ? $"{MontoVentaUf:N3}"
        : currency.FromClp(MontoVentaUf);

    public string DeltaText => DeltaUf == 0m
        ? "—"
        : currency is null
            ? $"{(DeltaUf > 0m ? "+" : string.Empty)}{DeltaUf:N3}"
            : currency.FromClpSigned(DeltaUf);

    public string DeltaKind => DeltaUf > 0m ? "Aumenta" : DeltaUf < 0m ? "Reduce" : "Neutro";
    public string RevBadge => string.IsNullOrWhiteSpace(Codigo) ? "—" : Codigo.Length <= 3 ? Codigo : Codigo[^3..];

    // Iniciales del autor (avatar de la tabla de historial): "C. Rojas" → "CR".
    public string Iniciales
    {
        get
        {
            var partes = (CreadoPor ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length == 0) return "—";
            if (partes.Length == 1) return partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant();
            return $"{partes[0][0]}{partes[^1][0]}".ToUpperInvariant();
        }
    }
}

public enum RevisionDiffFilterMode
{
    Todos = 0,
    SoloAdicionales = 1,
    SoloModificados = 2,
    SoloDeductivos = 3
}

public sealed class VersionComparisonRowVm
{
    private readonly VersionsCurrencyFormat? currency;

    public VersionComparisonRowVm(BudgetVersionComparisonRow source, VersionsCurrencyFormat? currency = null)
    {
        Source = source;
        this.currency = currency;
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

    public string Unidad => Source.Unidad;
    public decimal CantidadBase => Source.CantidadBase;
    public decimal CantidadActual => Source.CantidadActual;
    public decimal DeltaCantidad => Source.DeltaCantidad;
    public string DeltaCantidadText => DeltaCantidad == 0m ? "—" : (DeltaCantidad > 0m ? $"+{DeltaCantidad:N2}" : $"{DeltaCantidad:N2}");

    public decimal PuUfBase => Source.PuUfBase;
    public decimal PuUfActual => Source.PuUfActual;
    public decimal DeltaPuUf => Source.DeltaPuUf;
    public string DeltaPuUfText => DeltaPuUf == 0m ? "—" : (DeltaPuUf > 0m ? $"+{DeltaPuUf:N3}" : $"{DeltaPuUf:N3}");

    // Baseline/Current/Delta y P.U. vienen en UF reales del motor (CubicacionRow.UfTotal).
    // Con CLP se presentan en pesos y la UF desaparece de la grilla.
    public string PuBaseText => currency is null ? $"{PuUfBase:N3}" : currency.FromUf(PuUfBase);
    public string PuActualText => currency is null ? $"{PuUfActual:N3}" : currency.FromUf(PuUfActual);
    public string TotalBaseText => currency is null ? $"{BaselineUf:N3}" : currency.FromUf(BaselineUf);
    public string TotalActualText => currency is null ? $"{CurrentUf:N3}" : currency.FromUf(CurrentUf);
    public string DeltaUfText => currency is null
        ? $"{DeltaUf:N3}"
        : currency.FromUfSigned(DeltaUf);

    public decimal BaselineClp => Source.BaselineClp;
    public decimal CurrentClp => Source.CurrentClp;
    public decimal DeltaClp => Source.DeltaClp;
    public string DeltaClpText => DeltaClp == 0m ? "—" : (DeltaClp > 0m ? $"+$ {DeltaClp:N0}" : $"$ {DeltaClp:N0}");

    public RevisionDiffKind DiffKind => Source.DiffKind;
    public string DisplayBadge => Source.DisplayBadge;
    public string DetalleCambio => Source.DetalleCambio;
    public IReadOnlyList<BudgetVersionResourceDiffRow> ResourceDiffs => Source.ResourceDiffs;
    public bool HasResourceDiffs => ResourceDiffs != null && ResourceDiffs.Count > 0;
}

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly DashboardPresenter dashboard;
    private readonly IVersionComparisonService versionComparison;
    private readonly IBudgetRevisionDiffPdfExporter? pdfExporter;
    private readonly IBudgetRevisionDiffExcelExporter? excelExporter;
    private readonly IFileDialogService? fileDialog;
    private readonly IFileOpenService? fileOpener;

    private readonly IItemizadoSpreadsheetReader? spreadsheetReader;
    private readonly ImportarItemizadoUseCase? importItemizadoUseCase;

    private readonly ObservableCollection<VersionRowVm> versions = new();
    private readonly ObservableCollection<VersionComparisonRowVm> comparisonRows = new();
    private readonly ICollectionView versionsView;
    private readonly ICollectionView comparisonRowsView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand newCommand;
    private readonly RelayCommand activateCommand;
    private readonly RelayCommand renameCommand;
    private readonly RelayCommand deleteCommand;
    private readonly RelayCommand refreshComparisonCommand;
    private readonly RelayCommand goToItemCommand;
    private readonly RelayCommand exportPdfCommand;
    private readonly RelayCommand exportExcelCommand;
    private readonly RelayCommand createChangeOrderFromDiffCommand;
    private readonly RelayCommand compareWithExcelCommand;
    private readonly RelayCommand applyAddendaQuantitiesCommand;
    private readonly RelayCommand saveAddendaAsVersionCommand;

    private VersionRowVm? selectedVersion;
    private VersionRowVm? selectedComparisonVersion;
    private VersionComparisonRowVm? selectedComparisonRow;
    private BudgetVersionComparisonSummary? currentSummary;
    private RevisionDiffFilterMode activeFilterMode = RevisionDiffFilterMode.Todos;
    private bool compareAgainstCurrent = true;
    private bool isAddendaComparison;
    private string addendaFileName = "";
    private IReadOnlyList<ItemizadoRow>? lastIncomingAddendaItems;
    private bool showEmptyState = true;
    private bool isLoading;
    private string summaryText = "0 versiones";
    private string activeVersionText = "Sin versión activa";
    private string statusMessage = "Gestión de versiones no calculada.";
    private string comparisonSummaryText = "Sin comparación";
    private string comparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
    private string comparisonTotalsText = "—";
    private string comparisonAddedText = "0 nuevas";
    private string comparisonChangedText = "0 modificadas";
    private string comparisonRemovedText = "0 eliminadas";
    private string comparisonUnchangedText = "0 sin cambio";
    private string comparisonAdicionalesText = "+0.000 UF";
    private string comparisonDeductivosText = "0.000 UF";
    private string comparisonNetVariationText = "0.000 UF";
    private decimal comparisonDeltaUf;
    private decimal comparisonDeltaPct;
    private string comparisonModeHint = "Versión A = seleccionada · Versión B = estado actual";
    private string searchText = "";
}
