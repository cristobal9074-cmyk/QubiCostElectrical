using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.GoodsReceipts;

public sealed class GoodsReceiptWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<PurchaseOrder> _orders = new();
    private readonly ObservableCollection<GoodsReceipt> _receipts = new();
    private readonly ObservableCollection<ProjectInventoryBalance> _inventory = new();
    private PurchaseOrder? _selectedOrder;
    private GoodsReceipt? _selectedReceipt;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _partial = true;

    public GoodsReceiptWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        ReceiveCommand = new RelayCommand(async () => await ReceiveAsync(), () => SelectedOrder != null && !_isBusy && !IsReadOnly);
        AcceptCommand = new RelayCommand(async () => await AcceptAsync(), () => SelectedReceipt != null && !_isBusy && !IsReadOnly);
        ReserveCommand = new RelayCommand(async () => await ReserveAsync(), () => !_isBusy && !IsReadOnly && _inventory.Count > 0);
        _ = LoadAsync();
    }

    public ObservableCollection<PurchaseOrder> Orders => _orders;
    public ObservableCollection<GoodsReceipt> Receipts => _receipts;
    public ObservableCollection<ProjectInventoryBalance> Inventory => _inventory;
    public PurchaseOrder? SelectedOrder { get => _selectedOrder; set { if (SetProperty(ref _selectedOrder, value)) Raise(); } }
    public GoodsReceipt? SelectedReceipt { get => _selectedReceipt; set { if (SetProperty(ref _selectedReceipt, value)) Raise(); } }
    public bool PartialReceipt { get => _partial; set => SetProperty(ref _partial, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand ReceiveCommand { get; }
    public ICommand AcceptCommand { get; }
    public ICommand ReserveCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.PurchaseOrderService is null || _host.Services.GoodsReceiptService is null) return;
        _isBusy = true;
        try
        {
            var path = _host.Session.CurrentProjectPath ?? "project";
            _orders.Clear();
            _receipts.Clear();
            _inventory.Clear();
            foreach (var o in await _host.Services.PurchaseOrderService.ListByProjectAsync(path))
                if (o.Status is PurchaseOrderStatus.Issued or PurchaseOrderStatus.ConfirmedBySupplier or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
                    _orders.Add(o);
            foreach (var r in await _host.Services.GoodsReceiptService.ListReceiptsAsync(path)) _receipts.Add(r);
            foreach (var i in await _host.Services.GoodsReceiptService.ListInventoryAsync(path)) _inventory.Add(i);
            StatusMessage = $"{_receipts.Count} recepción(es) · {_inventory.Count} saldo(s) inventario.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task ReceiveAsync()
    {
        if (SelectedOrder is null || _host.Services.GoodsReceiptService is null) return;
        _isBusy = true;
        try
        {
            var receipt = await _host.Services.GoodsReceiptService.CreateFromPurchaseOrderAsync(SelectedOrder.Id, Actor(), PartialReceipt);
            _receipts.Insert(0, receipt);
            SelectedReceipt = receipt;
            StatusMessage = $"Recepción {receipt.Number} creada ({(PartialReceipt ? "parcial" : "total")}).";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task AcceptAsync()
    {
        if (SelectedReceipt is null || _host.Services.GoodsReceiptService is null) return;
        _isBusy = true;
        try
        {
            SelectedReceipt = await _host.Services.GoodsReceiptService.AcceptAsync(SelectedReceipt.Id, SelectedReceipt.RowVersion, Actor());
            StatusMessage = $"Recepción aceptada: {SelectedReceipt.Status}";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task ReserveAsync()
    {
        if (_host.Services.GoodsReceiptService is null || _inventory.Count == 0) return;
        var bal = _inventory[0];
        try
        {
            await _host.Services.GoodsReceiptService.ReserveAsync(bal.ProjectPath, bal.Code, bal.Lot, bal.Location, 1m, Actor());
            StatusMessage = $"Reservada 1 u. de {bal.Code}";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "user";
    private void Raise()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReceiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AcceptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReserveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        Raise();
        _ = LoadAsync();
    }
}
