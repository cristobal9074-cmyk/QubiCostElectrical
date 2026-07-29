using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Rfq;

public sealed class RfqWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<RequestForQuotation> _items = new();
    private readonly ObservableCollection<Supplier> _suppliers = new();
    private RequestForQuotation? _selected;
    private Supplier? _inviteSupplier;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _title = "RFQ materiales";
    private string _responseComment = string.Empty;
    private bool _isBusy;

    public RfqWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !_isBusy && !IsReadOnly);
        InviteCommand = new RelayCommand(async () => await InviteAsync(), () => Selected != null && InviteSupplier != null && !_isBusy && !IsReadOnly);
        ApproveCommand = new RelayCommand(async () => await ApproveAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        IssueCommand = new RelayCommand(async () => await IssueAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        RegisterResponseCommand = new RelayCommand(async () => await RegisterResponseAsync(), () => Selected != null && InviteSupplier != null && !_isBusy && !IsReadOnly);
        CancelCommand = new RelayCommand(async () => await CancelAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => Selected != null && !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<RequestForQuotation> Items => _items;
    public ObservableCollection<Supplier> Suppliers => _suppliers;
    public RequestForQuotation? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) RaiseCmds(); }
    }
    public Supplier? InviteSupplier
    {
        get => _inviteSupplier;
        set { if (SetProperty(ref _inviteSupplier, value)) RaiseCmds(); }
    }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string ResponseComment { get => _responseComment; set => SetProperty(ref _responseComment, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand InviteCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand IssueCommand { get; }
    public ICommand RegisterResponseCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        if (_host.Services.RfqService is null) { ErrorMessage = "Servicio RFQ no disponible."; return; }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var path = ProjectPath();
            var list = await _host.Services.RfqService.SearchAsync(path, null, 0, 200);
            _items.Clear();
            foreach (var r in list) _items.Add(r);
            _suppliers.Clear();
            if (_host.Services.SupplierService is not null)
                foreach (var s in await _host.Services.SupplierService.SearchAsync(null, SupplierStatus.Active, 0, 100))
                    _suppliers.Add(s);
            StatusMessage = $"{list.Count} RFQ.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); OnPropertyChanged(nameof(IsReadOnly)); }
    }

    private async Task CreateAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var prs = await _host.Services.PurchaseRequisitionService.SearchAsync(ProjectPath(), PurchaseRequisitionStatus.Approved, 0, 50);
            if (prs.Count == 0)
            {
                ErrorMessage = "No hay requerimientos aprobados para crear RFQ.";
                return;
            }
            var created = await _host.Services.RfqService.CreateFromRequisitionsAsync(
                ProjectPath(), prs.Take(1).Select(p => p.Id).ToList(), Title, Actor());
            StatusMessage = $"Creada {created.Number}.";
            await LoadAsync();
            Selected = _items.FirstOrDefault(x => x.Id.Value == created.Id.Value);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task InviteAsync()
    {
        if (Selected is null || InviteSupplier is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            await _host.Services.RfqService.InviteSupplierAsync(Selected.Id, InviteSupplier.Id, null, Selected.RowVersion, Actor());
            Selected = await _host.Services.RfqService.GetAsync(Selected.Id);
            StatusMessage = "Proveedor invitado.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task ApproveAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            Selected = await _host.Services.RfqService.ApproveAsync(Selected.Id, Selected.RowVersion, Actor());
            StatusMessage = "RFQ aprobada.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task IssueAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            Selected = await _host.Services.RfqService.IssueAsync(Selected.Id, Selected.RowVersion, Actor());
            StatusMessage = "RFQ emitida.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task RegisterResponseAsync()
    {
        if (Selected is null || InviteSupplier is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            await _host.Services.RfqService.RegisterResponseAsync(new QuotationResponse
            {
                ResponseId = Guid.NewGuid().ToString("N"),
                RfqId = Selected.Id,
                SupplierId = InviteSupplier.Id,
                ReceivedUtc = DateTime.UtcNow,
                CurrencyCode = Selected.ComparisonCurrency,
                DeliveryLeadDays = 14,
                Comment = ResponseComment,
                RecordedBy = Actor()
            }, Actor());
            StatusMessage = "Respuesta registrada.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task CancelAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            await _host.Services.RfqService.CancelAsync(Selected.Id, Selected.RowVersion, Actor(), "Cancelada");
            StatusMessage = "RFQ cancelada.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task ExportAsync()
    {
        if (Selected is null) return;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"rfq-{Selected.Number}.csv");
            await File.WriteAllTextAsync(path, ProcurementExportHelper.RfqToCsv(Selected)
                + Environment.NewLine + ProcurementExportHelper.InvitationsToCsv(Selected.Invitations));
            StatusMessage = $"Exportado: {path}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private string ProjectPath() => _host.Session.CurrentProjectPath ?? "project";
    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void RaiseCmds()
    {
        foreach (var c in new[] { RefreshCommand, CreateCommand, InviteCommand, ApproveCommand, IssueCommand, RegisterResponseCommand, CancelCommand, ExportCommand })
            ((RelayCommand)c).RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        _ = LoadAsync();
    }
}
