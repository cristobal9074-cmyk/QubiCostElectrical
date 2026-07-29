using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Suppliers;

public sealed class SupplierWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<Supplier> _items = new();
    private Supplier? _selected;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private string _editCode = string.Empty;
    private string _editLegalName = string.Empty;
    private string _editTradeName = string.Empty;
    private string _editEmail = string.Empty;
    private string _editPhone = string.Empty;
    private string _editCity = string.Empty;
    private string _contactName = string.Empty;
    private string _contactEmail = string.Empty;
    private readonly ObservableCollection<SupplierContact> _contacts = new();

    public SupplierWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !_isBusy && !IsReadOnly);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        DisableCommand = new RelayCommand(async () => await DisableAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        AddContactCommand = new RelayCommand(async () => await AddContactAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => Selected != null && !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<Supplier> Items => _items;
    public ObservableCollection<SupplierContact> Contacts => _contacts;
    public Supplier? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                LoadEditor();
                _ = LoadContactsAsync();
                RaiseCmds();
            }
        }
    }
    public string SearchText { get => _search; set { if (SetProperty(ref _search, value)) _ = LoadAsync(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public string EditCode { get => _editCode; set => SetProperty(ref _editCode, value); }
    public string EditLegalName { get => _editLegalName; set => SetProperty(ref _editLegalName, value); }
    public string EditTradeName { get => _editTradeName; set => SetProperty(ref _editTradeName, value); }
    public string EditEmail { get => _editEmail; set => SetProperty(ref _editEmail, value); }
    public string EditPhone { get => _editPhone; set => SetProperty(ref _editPhone, value); }
    public string EditCity { get => _editCity; set => SetProperty(ref _editCity, value); }
    public string ContactName { get => _contactName; set => SetProperty(ref _contactName, value); }
    public string ContactEmail { get => _contactEmail; set => SetProperty(ref _contactEmail, value); }

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand AddContactCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        if (_host.Services.SupplierService is null) { ErrorMessage = "Servicio de proveedores no disponible."; return; }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _host.Services.SupplierService.SearchAsync(SearchText, null, 0, 200);
            _items.Clear();
            foreach (var s in list) _items.Add(s);
            StatusMessage = $"{list.Count} proveedor(es).";
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
            var created = await _host.Services.SupplierService.CreateAsync(new Supplier
            {
                Code = string.IsNullOrWhiteSpace(EditCode) ? "SUP-" + DateTime.UtcNow.ToString("HHmmss") : EditCode.Trim(),
                LegalName = string.IsNullOrWhiteSpace(EditLegalName) ? "Nuevo proveedor" : EditLegalName.Trim(),
                TradeName = EditTradeName,
                Email = EditEmail,
                Phone = EditPhone,
                City = EditCity,
                Status = SupplierStatus.Prospect
            }, Actor());
            Selected = created;
            StatusMessage = $"Creado {created.Code}.";
            await LoadAsync();
            Selected = _items.FirstOrDefault(x => x.Id.Value == created.Id.Value) ?? created;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task SaveAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            Selected.Code = EditCode.Trim();
            Selected.LegalName = EditLegalName.Trim();
            Selected.TradeName = EditTradeName;
            Selected.Email = EditEmail;
            Selected.Phone = EditPhone;
            Selected.City = EditCity;
            var saved = await _host.Services.SupplierService.UpdateAsync(Selected, Selected.RowVersion, Actor());
            Selected = saved;
            StatusMessage = "Proveedor guardado.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task DisableAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            await _host.Services.SupplierService.DisableAsync(Selected.Id, Actor());
            StatusMessage = "Proveedor desactivado.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task AddContactAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            await _host.Services.SupplierService.UpsertContactAsync(new SupplierContact
            {
                ContactId = Guid.NewGuid().ToString("N"),
                SupplierId = Selected.Id,
                Name = ContactName.Trim(),
                Email = ContactEmail.Trim(),
                IsPrimary = _contacts.Count == 0,
                IsCommercial = true,
                IsActive = true
            }, Actor());
            ContactName = string.Empty;
            ContactEmail = string.Empty;
            await LoadContactsAsync();
            StatusMessage = "Contacto agregado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task LoadContactsAsync()
    {
        _contacts.Clear();
        if (Selected is null || _host.Services.SupplierService is null) return;
        foreach (var c in await _host.Services.SupplierService.ListContactsAsync(Selected.Id))
            _contacts.Add(c);
    }

    private async Task ExportAsync()
    {
        if (Selected is null) return;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"supplier-{Selected.Code}.csv");
            await File.WriteAllTextAsync(path, ProcurementExportHelper.SupplierToCsv(Selected));
            StatusMessage = $"Exportado: {path}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void LoadEditor()
    {
        EditCode = Selected?.Code ?? string.Empty;
        EditLegalName = Selected?.LegalName ?? string.Empty;
        EditTradeName = Selected?.TradeName ?? string.Empty;
        EditEmail = Selected?.Email ?? string.Empty;
        EditPhone = Selected?.Phone ?? string.Empty;
        EditCity = Selected?.City ?? string.Empty;
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void RaiseCmds()
    {
        ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DisableCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddContactCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        _ = LoadAsync();
    }
}
