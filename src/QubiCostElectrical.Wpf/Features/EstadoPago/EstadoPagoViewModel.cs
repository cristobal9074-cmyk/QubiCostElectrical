using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.EstadoPago;

public sealed partial class EstadoPagoViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<EstadoPagoMonthVm> _months = new();
    private readonly ObservableCollection<EstadoPagoRowVm> _rows = new();
    private EstadoPagoMonthVm? _selectedMonth;
    private EstadoPagoRowVm? _selectedRow;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _contractAmountLabel = "$ 0";
    private string _accumulatedLabel = "$ 0";
    private string _periodAmountLabel = "$ 0";
    private string _balanceLabel = "$ 0";
    private string _progressPctLabel = "0%";
    private string _certNumber = "—";
    private string _periodLabel = "";
    private string _statusKind = "locked";
    private bool _isBusy;
    private int _activeRowCount;
    private int _totalRowCount;

    public EstadoPagoViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !_isBusy && !IsReadOnly);
        RecalcAllCommand = new RelayCommand(RecalcAll, () => !_isBusy);
        CreateNewCommand = new RelayCommand(CreateNew, () => !_isBusy && !IsReadOnly);
        SelectMonthCommand = new RelayCommand<EstadoPagoMonthVm?>(SelectMonth);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        _ = LoadAsync();
    }

    // ─── Collections ──────────────────────────────────────
    public ObservableCollection<EstadoPagoMonthVm> Months => _months;
    public ObservableCollection<EstadoPagoRowVm> Rows => _rows;

    // ─── Selected Month ────────────────────────────────────
    public EstadoPagoMonthVm? SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            if (SetProperty(ref _selectedMonth, value))
            {
                OnPropertyChanged(nameof(HasSelectedMonth));
                _ = LoadCertificateForMonth(value);
            }
        }
    }

    public bool HasSelectedMonth => _selectedMonth != null;

    // ─── Selected Row ─────────────────────────────────────
    public EstadoPagoRowVm? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                if (_selectedRow != null)
                {
                    foreach (var r in _rows) r.IsSelected = r == _selectedRow;
                }
            }
        }
    }

    // ─── Properties ────────────────────────────────────────
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ContractAmountLabel { get => _contractAmountLabel; private set => SetProperty(ref _contractAmountLabel, value); }
    public string AccumulatedLabel { get => _accumulatedLabel; private set => SetProperty(ref _accumulatedLabel, value); }
    public string PeriodAmountLabel { get => _periodAmountLabel; private set => SetProperty(ref _periodAmountLabel, value); }
    public string BalanceLabel { get => _balanceLabel; private set => SetProperty(ref _balanceLabel, value); }
    public string ProgressPctLabel { get => _progressPctLabel; private set => SetProperty(ref _progressPctLabel, value); }
    public string CertNumber { get => _certNumber; private set => SetProperty(ref _certNumber, value); }
    public string PeriodLabel { get => _periodLabel; private set => SetProperty(ref _periodLabel, value); }
    public string StatusKind { get => _statusKind; private set => SetProperty(ref _statusKind, value); }
    public int ActiveRowCount { get => _activeRowCount; private set => SetProperty(ref _activeRowCount, value); }
    public int TotalRowCount { get => _totalRowCount; private set => SetProperty(ref _totalRowCount, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    // ─── Commands ──────────────────────────────────────────
    public ICommand SaveCommand { get; }
    public ICommand RecalcAllCommand { get; }
    public ICommand CreateNewCommand { get; }
    public ICommand SelectMonthCommand { get; }
    public ICommand ClearSearchCommand { get; }

    // ─── Event handlers ────────────────────────────────────
    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        _ = LoadAsync();
    }
}
