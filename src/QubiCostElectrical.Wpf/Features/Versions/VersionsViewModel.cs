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
}

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly DashboardPresenter dashboard;
    private readonly IVersionComparisonService versionComparison;
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

    private VersionRowVm? selectedVersion;
    private VersionRowVm? selectedComparisonVersion;
    private VersionComparisonRowVm? selectedComparisonRow;
    private bool compareAgainstCurrent = true;
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
    private decimal comparisonDeltaUf;
    private decimal comparisonDeltaPct;
    private string comparisonModeHint = "Versión A = seleccionada · Versión B = estado actual";
}
