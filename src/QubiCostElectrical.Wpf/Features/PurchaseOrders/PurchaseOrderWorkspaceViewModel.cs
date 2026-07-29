using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.PurchaseOrders;

public sealed class PurchaseOrderWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<AwardDecision> _awards = new();
    private readonly ObservableCollection<PurchaseOrder> _orders = new();
    private AwardDecision? _selectedAward;
    private PurchaseOrder? _selected;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _exportPreview = string.Empty;
    private bool _isBusy;

    public PurchaseOrderWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        GenerateCommand = new RelayCommand(async () => await GenerateAsync(), () => SelectedAward != null && !_isBusy && !IsReadOnly);
        SubmitCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.SubmitAsync(po.Id, po.RowVersion, Actor())), () => Selected != null && !_isBusy && !IsReadOnly);
        ApproveCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.ApproveAsync(po.Id, po.RowVersion, Actor())), () => Selected != null && !_isBusy && !IsReadOnly);
        IssueCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.IssueAsync(po.Id, po.RowVersion, Actor())), () => Selected != null && !_isBusy && !IsReadOnly);
        ConfirmCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.ConfirmSupplierAsync(po.Id, po.RowVersion, Actor(), "CONF-" + DateTime.UtcNow.Ticks, DateTime.Today.AddDays(14))), () => Selected != null && !_isBusy && !IsReadOnly);
        CancelCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.CancelAsync(po.Id, po.RowVersion, Actor(), "Cancelada")), () => Selected != null && !_isBusy && !IsReadOnly);
        CloseCommand = new RelayCommand(async () => await Run(po => _host.Services.PurchaseOrderService.CloseAsync(po.Id, po.RowVersion, Actor(), "Cierre admin")), () => Selected != null && !_isBusy && !IsReadOnly);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<AwardDecision> Awards => _awards;
    public ObservableCollection<PurchaseOrder> Orders => _orders;
    public AwardDecision? SelectedAward { get => _selectedAward; set { if (SetProperty(ref _selectedAward, value)) Raise(); } }
    public PurchaseOrder? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) Raise(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string ExportPreview { get => _exportPreview; private set => SetProperty(ref _exportPreview, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand IssueCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.RfqService is null || _host.Services.AwardService is null || _host.Services.PurchaseOrderService is null) return;
        _isBusy = true;
        try
        {
            _awards.Clear();
            _orders.Clear();
            var path = _host.Session.CurrentProjectPath ?? "project";
            foreach (var rfq in await _host.Services.RfqService.SearchAsync(path, null, 0, 100))
            {
                foreach (var a in await _host.Services.AwardRepository.GetByRfqAsync(rfq.Id))
                    if (a.Status == AwardStatus.Approved) _awards.Add(a);
            }
            foreach (var o in await _host.Services.PurchaseOrderService.ListByProjectAsync(path)) _orders.Add(o);
            StatusMessage = $"{_orders.Count} OC · {_awards.Count} adjudicación(es) aprobada(s).";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task GenerateAsync()
    {
        if (SelectedAward is null || _host.Services.PurchaseOrderService is null) return;
        _isBusy = true;
        try
        {
            var created = await _host.Services.PurchaseOrderService.GenerateFromAwardAsync(SelectedAward.Id, Actor());
            foreach (var o in created) _orders.Insert(0, o);
            Selected = created.FirstOrDefault();
            StatusMessage = $"{created.Count} OC generada(s). Compromiso inicial {Selected?.CommittedCostBase:N0}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task Run(Func<PurchaseOrder, Task<PurchaseOrder>> action)
    {
        if (Selected is null || _host.Services.PurchaseOrderService is null) return;
        _isBusy = true;
        try
        {
            Selected = await action(Selected);
            StatusMessage = $"OC {Selected.Number}: {Selected.Status}";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task ExportAsync()
    {
        if (_host.Services.PurchaseOrderService is null) return;
        try
        {
            ExportPreview = await _host.Services.PurchaseOrderService.ExportCsvAsync(_host.Session.CurrentProjectPath ?? "project");
            StatusMessage = "Exportación CSV lista.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "user";
    private void Raise()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GenerateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SubmitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApproveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (IssueCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ConfirmCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CloseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        Raise();
        _ = LoadAsync();
    }
}
