using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using DomainBidComparison = QubiCostElectrical.BidComparison;

namespace QubiCostElectrical.Wpf.Features.BidComparison;

public sealed class ComparisonCellVm
{
    public string Supplier { get; init; } = string.Empty;
    public string UnitPriceBase { get; init; } = string.Empty;
    public string TotalAcquisition { get; init; } = string.Empty;
    public string LeadDays { get; init; } = string.Empty;
    public string Technical { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public string Score { get; init; } = string.Empty;
    public string RecommendedLabel { get; init; } = string.Empty;
}

public sealed class ComparisonRowVm
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ObservableCollection<ComparisonCellVm> Cells { get; } = new();
}

public sealed class BidComparisonWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<RequestForQuotation> _rfqs = new();
    private readonly ObservableCollection<ComparisonRowVm> _rows = new();
    private RequestForQuotation? _selectedRfq;
    private DomainBidComparison? _comparison;
    private string _baseCurrency = "CLP";
    private decimal _exchangeRate = 1m;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _recommendation = string.Empty;
    private bool _isBusy;

    public BidComparisonWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CompareCommand = new RelayCommand(async () => await CompareAsync(), () => SelectedRfq != null && !_isBusy && !IsReadOnly);
        _ = LoadAsync();
    }

    public ObservableCollection<RequestForQuotation> Rfqs => _rfqs;
    public ObservableCollection<ComparisonRowVm> Rows => _rows;
    public RequestForQuotation? SelectedRfq { get => _selectedRfq; set { if (SetProperty(ref _selectedRfq, value)) Raise(); } }
    public string BaseCurrency { get => _baseCurrency; set => SetProperty(ref _baseCurrency, value); }
    public decimal ExchangeRate { get => _exchangeRate; set => SetProperty(ref _exchangeRate, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string Recommendation { get => _recommendation; private set => SetProperty(ref _recommendation, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand CompareCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.RfqService is null) return;
        _isBusy = true;
        try
        {
            _rfqs.Clear();
            foreach (var r in await _host.Services.RfqService.SearchAsync(_host.Session.CurrentProjectPath ?? "project", null, 0, 100))
                _rfqs.Add(r);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task CompareAsync()
    {
        if (SelectedRfq is null || _host.Services.BidComparisonService is null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            _comparison = await _host.Services.BidComparisonService.CompareAsync(
                SelectedRfq.Id, BaseCurrency, ExchangeRate, DateTime.Today, Actor());
            _rows.Clear();
            foreach (var row in _comparison.Rows)
            {
                var vm = new ComparisonRowVm { Code = row.Code, Description = row.Description };
                foreach (var cell in row.Cells.OrderByDescending(c => c.RankScore))
                {
                    vm.Cells.Add(new ComparisonCellVm
                    {
                        Supplier = cell.SupplierId.Value[..Math.Min(8, cell.SupplierId.Value.Length)],
                        UnitPriceBase = cell.UnitPriceBase.ToString("N2"),
                        TotalAcquisition = cell.TotalAcquisitionCostBase.ToString("N2"),
                        LeadDays = cell.DeliveryLeadDays.ToString(),
                        Technical = cell.TechnicalCompliance.ToString(),
                        Risk = cell.Risk.ToString(),
                        Score = cell.RankScore.ToString("N1"),
                        RecommendedLabel = cell.IsRecommended ? "RECOMENDADA" : ""
                    });
                }
                _rows.Add(vm);
            }
            Recommendation = _comparison.Recommendation?.Summary ?? string.Empty;
            StatusMessage = $"Comparación {_comparison.Id.Value[..8]} · tasa {ExchangeRate} ({BaseCurrency}) al {_comparison.ExchangeRateDate:yyyy-MM-dd}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void Raise()
    {
        ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CompareCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        Raise();
        _ = LoadAsync();
    }
}
