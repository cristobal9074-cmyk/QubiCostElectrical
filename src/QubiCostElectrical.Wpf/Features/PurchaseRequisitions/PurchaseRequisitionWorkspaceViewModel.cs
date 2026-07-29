using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.PurchaseRequisitions;

public sealed class PurchaseRequisitionWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<PurchaseRequisition> _items = new();
    private PurchaseRequisition? _selected;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _itemCode = "MAT-001";
    private string _itemDescription = "Material";
    private decimal _itemQty = 1m;
    private string _rejectReason = string.Empty;
    private bool _isBusy;

    public PurchaseRequisitionWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !_isBusy && !IsReadOnly);
        AddItemCommand = new RelayCommand(async () => await AddItemAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        SubmitCommand = new RelayCommand(async () => await SubmitAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ApproveCommand = new RelayCommand(async () => await ApproveAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        RejectCommand = new RelayCommand(async () => await RejectAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        CancelCommand = new RelayCommand(async () => await CancelAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ConsolidateCommand = new RelayCommand(async () => await ConsolidateAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ImportBudgetCommand = new RelayCommand(async () => await ImportBudgetAsync(), () => !_isBusy && !IsReadOnly);
        ImportScheduleCommand = new RelayCommand(async () => await ImportScheduleAsync(), () => !_isBusy && !IsReadOnly);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => Selected != null && !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<PurchaseRequisition> Items => _items;
    public PurchaseRequisition? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) RaiseCmds(); }
    }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public string ItemCode { get => _itemCode; set => SetProperty(ref _itemCode, value); }
    public string ItemDescription { get => _itemDescription; set => SetProperty(ref _itemDescription, value); }
    public decimal ItemQty { get => _itemQty; set => SetProperty(ref _itemQty, value); }
    public string RejectReason { get => _rejectReason; set => SetProperty(ref _rejectReason, value); }
    public int SelectedItemCount => Selected?.Items.Count ?? 0;
    public decimal SelectedBudgetCost => Selected?.Items.Sum(i => i.RequiredQuantity * i.BudgetedUnitPrice) ?? 0m;

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConsolidateCommand { get; }
    public ICommand ImportBudgetCommand { get; }
    public ICommand ImportScheduleCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        if (_host.Services.PurchaseRequisitionService is null) { ErrorMessage = "Servicio de requerimientos no disponible."; return; }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var path = ProjectPath();
            var list = await _host.Services.PurchaseRequisitionService.SearchAsync(path, null, 0, 200);
            _items.Clear();
            foreach (var r in list) _items.Add(r);
            StatusMessage = $"{list.Count} requerimiento(s).";
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
            var created = await _host.Services.PurchaseRequisitionService.CreateAsync(new PurchaseRequisition
            {
                ProjectPath = ProjectPath(),
                RequiredDate = DateTime.Today.AddDays(21),
                Priority = PurchasePriority.Normal,
                PrimaryOrigin = NeedOriginKind.Manual
            }, Actor());
            StatusMessage = $"Creado {created.Number}.";
            await LoadAsync();
            Selected = _items.FirstOrDefault(x => x.Id.Value == created.Id.Value);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task AddItemAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var updated = await _host.Services.PurchaseRequisitionService.AddItemAsync(Selected.Id, new PurchaseRequisitionItem
            {
                ItemId = Guid.NewGuid().ToString("N"),
                RequisitionId = Selected.Id,
                Code = ItemCode,
                Description = ItemDescription,
                RequiredQuantity = ItemQty,
                RequiredDate = Selected.RequiredDate,
                Unit = "UN",
                CurrencyCode = Selected.CurrencyCode
            }, Selected.RowVersion, Actor());
            Selected = updated;
            StatusMessage = "Ítem agregado.";
            await LoadAsync();
            Selected = _items.FirstOrDefault(x => x.Id.Value == updated.Id.Value);
            OnPropertyChanged(nameof(SelectedItemCount));
            OnPropertyChanged(nameof(SelectedBudgetCost));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task SubmitAsync() => await Mutate(s => s.SubmitAsync(Selected!.Id, Selected.RowVersion, Actor()), "Enviado a revisión.");
    private async Task ApproveAsync() => await Mutate(s => s.ApproveAsync(Selected!.Id, Selected.RowVersion, Actor(), null), "Aprobado.");
    private async Task RejectAsync() => await Mutate(s => s.RejectAsync(Selected!.Id, Selected.RowVersion, Actor(), string.IsNullOrWhiteSpace(RejectReason) ? "Rechazado" : RejectReason), "Rechazado.");
    private async Task CancelAsync() => await Mutate(s => s.CancelAsync(Selected!.Id, Selected.RowVersion, Actor(), "Cancelado"), "Cancelado.");

    private async Task ConsolidateAsync()
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var result = await _host.Services.PurchaseRequisitionService.ConsolidateAsync(Selected.Id, Actor());
            StatusMessage = $"Consolidación: {result.Lines.Count} línea(s), {result.Conflicts.Count} conflicto(s).";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task ImportBudgetAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var preview = _host.Services.PurchaseRequisitionService.PreviewFromBudget(new[]
            {
                new NeedGenerationPreviewItem
                {
                    Code = "BUD-1", Description = "Desde presupuesto", Unit = "UN", Quantity = 10,
                    RequiredDate = DateTime.Today.AddDays(30), Origin = NeedOriginKind.Budget, OriginId = "budget-1", Selected = true
                }
            });
            var created = await _host.Services.PurchaseRequisitionService.ConfirmGenerationAsync(ProjectPath(), preview, Actor(), NeedOriginKind.Budget);
            StatusMessage = $"Generado desde presupuesto: {created.Number}.";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task ImportScheduleAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var preview = _host.Services.PurchaseRequisitionService.PreviewFromSchedule(new[]
            {
                new NeedGenerationPreviewItem
                {
                    Code = "ACT-1", Description = "Desde planificación", Unit = "UN", Quantity = 5,
                    RequiredDate = DateTime.Today.AddDays(45), ActivityId = "act-1", Origin = NeedOriginKind.Activity, OriginId = "act-1", Selected = true
                }
            });
            var created = await _host.Services.PurchaseRequisitionService.ConfirmGenerationAsync(ProjectPath(), preview, Actor(), NeedOriginKind.Schedule);
            StatusMessage = $"Generado desde planificación: {created.Number} (solicitud sugerida {preview[0].SuggestedRequestDate:yyyy-MM-dd}).";
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
            var path = Path.Combine(Path.GetTempPath(), $"pr-{Selected.Number}.csv");
            await File.WriteAllTextAsync(path, ProcurementExportHelper.RequisitionToCsv(Selected));
            StatusMessage = $"Exportado: {path}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task Mutate(Func<IPurchaseRequisitionService, Task<PurchaseRequisition>> action, string ok)
    {
        if (Selected is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var updated = await action(_host.Services.PurchaseRequisitionService);
            StatusMessage = ok;
            await LoadAsync();
            Selected = _items.FirstOrDefault(x => x.Id.Value == updated.Id.Value);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private string ProjectPath() => _host.Session.CurrentProjectPath ?? "project";
    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void RaiseCmds()
    {
        foreach (var c in new[] { RefreshCommand, CreateCommand, AddItemCommand, SubmitCommand, ApproveCommand, RejectCommand, CancelCommand, ConsolidateCommand, ImportBudgetCommand, ImportScheduleCommand, ExportCommand })
            ((RelayCommand)c).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedItemCount));
        OnPropertyChanged(nameof(SelectedBudgetCost));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        _ = LoadAsync();
    }
}
