using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ProcurementDashboard;

public sealed class ProcurementDashboardViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ProcurementAlert> _alerts = new();
    private readonly ObservableCollection<SupplierExposureRow> _suppliers = new();
    private ProcurementCommitmentSummary? _summary;
    private decimal _budget = 1000000m;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ProcurementDashboardViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        SnapshotCommand = new RelayCommand(async () => await SnapshotAsync(), () => !_isBusy && !IsReadOnly);
        _ = LoadAsync();
    }

    public ProcurementCommitmentSummary? Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public ObservableCollection<ProcurementAlert> Alerts => _alerts;
    public ObservableCollection<SupplierExposureRow> Suppliers => _suppliers;
    public decimal BudgetAmount { get => _budget; set => SetProperty(ref _budget, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand SnapshotCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.ProcurementCommitmentService is null) return;
        _isBusy = true;
        try
        {
            var path = _host.Session.CurrentProjectPath ?? "project";
            Summary = await _host.Services.ProcurementCommitmentService.BuildAsync(path, BudgetAmount);
            _alerts.Clear();
            foreach (var a in Summary.Alerts) _alerts.Add(a);
            _suppliers.Clear();
            foreach (var s in Summary.SupplierExposure) _suppliers.Add(s);
            StatusMessage = $"Comprometido {Summary.CommittedAmount:N0} · Saldo {Summary.BalanceAmount:N0} · Hash {Summary.SnapshotHash[..8]}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged(); (SnapshotCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    private async Task SnapshotAsync()
    {
        if (_host.Services.ProcurementCommitmentService is null) return;
        try
        {
            Summary = await _host.Services.ProcurementCommitmentService.SnapshotAsync(
                _host.Session.CurrentProjectPath ?? "project", BudgetAmount, _host.Session.ProjectInfo.UsuarioNombre ?? "user");
            StatusMessage = "Snapshot de compromiso guardado.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        _ = LoadAsync();
    }
}
