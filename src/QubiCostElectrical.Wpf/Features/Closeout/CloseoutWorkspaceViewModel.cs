using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Closeout;

public sealed class CloseoutWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private int _selectedTabIndex;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _closeoutCode = string.Empty;
    private string _closeoutName = string.Empty;
    private string _closeoutStatus = string.Empty;
    private string _closeoutProject = string.Empty;
    private string _reconciliationSummary = string.Empty;
    private decimal _readinessGlobalPct;
    private decimal _readinessDocPct;
    private decimal _readinessTechPct;
    private decimal _readinessEcoPct;
    private bool _isBusy;
    private ProjectCloseout? _closeout;
    private CloseoutChecklistItem? _selectedChecklistItem;
    private PunchItem? _selectedPunchItem;

    private readonly ObservableCollection<CloseoutChecklistItem> _checklistItems = new();
    private readonly ObservableCollection<PunchItem> _punchItems = new();
    private readonly ObservableCollection<CloseoutSnapshot> _snapshots = new();

    public CloseoutWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateCloseoutCommand = new RelayCommand(async () => await CreateCloseoutAsync(), () => !_isBusy && _host.Session.CurrentProjectPath != null);
        SubmitCloseoutCommand = new RelayCommand(async () => await SubmitCloseoutAsync(), () => _closeout != null && _closeout.Status == QubiCostElectrical.CloseoutStatus.Draft);
        ApproveReadinessCommand = new RelayCommand(async () => await ApproveReadinessAsync(), () => _closeout != null);
        ApplyTemplateCommand = new RelayCommand(async () => await ApplyTemplateAsync(), () => _closeout != null);
        VerifyItemCommand = new RelayCommand(async () => await VerifyItemAsync(), () => _selectedChecklistItem != null);
        CreatePunchItemCommand = new RelayCommand(async () => await CreatePunchItemAsync(), () => _closeout != null);
        ClosePunchItemCommand = new RelayCommand(async () => await ClosePunchItemAsync(), () => _selectedPunchItem != null);
        ReopenPunchItemCommand = new RelayCommand(async () => await ReopenPunchItemAsync(), () => _selectedPunchItem != null);
        RunReconciliationCommand = new RelayCommand(async () => await RunReconciliationAsync(), () => _closeout != null);
        CalculateReadinessCommand = new RelayCommand(async () => await CalculateReadinessAsync(), () => _closeout != null);
        CreateSnapshotCommand = new RelayCommand(async () => await CreateSnapshotAsync(), () => _closeout != null);
        NavigateTabCommand = new RelayCommand<string>(OnNavigateTab);
        _ = LoadAsync();
    }

    public int SelectedTabIndex { get => _selectedTabIndex; set { if (SetProperty(ref _selectedTabIndex, value)) { OnTabChanged(); } } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string CloseoutCode { get => _closeoutCode; private set => SetProperty(ref _closeoutCode, value); }
    public string CloseoutName { get => _closeoutName; private set => SetProperty(ref _closeoutName, value); }
    public string CloseoutStatus { get => _closeoutStatus; private set => SetProperty(ref _closeoutStatus, value); }
    public string CloseoutProject { get => _closeoutProject; private set => SetProperty(ref _closeoutProject, value); }
    public string ReconciliationSummary { get => _reconciliationSummary; private set => SetProperty(ref _reconciliationSummary, value); }
    public decimal ReadinessGlobalPct { get => _readinessGlobalPct; private set => SetProperty(ref _readinessGlobalPct, value); }
    public decimal ReadinessDocPct { get => _readinessDocPct; private set => SetProperty(ref _readinessDocPct, value); }
    public decimal ReadinessTechPct { get => _readinessTechPct; private set => SetProperty(ref _readinessTechPct, value); }
    public decimal ReadinessEcoPct { get => _readinessEcoPct; private set => SetProperty(ref _readinessEcoPct, value); }
    public bool IsGeneralTab => SelectedTabIndex == 0;
    public bool IsChecklistTab => SelectedTabIndex == 1;
    public bool IsPunchTab => SelectedTabIndex == 2;
    public bool IsReconciliationTab => SelectedTabIndex == 3;
    public bool IsReadinessTab => SelectedTabIndex == 4;
    public CloseoutChecklistItem? SelectedChecklistItem { get => _selectedChecklistItem; set { if (SetProperty(ref _selectedChecklistItem, value)) RaiseCmds(); } }
    public PunchItem? SelectedPunchItem { get => _selectedPunchItem; set { if (SetProperty(ref _selectedPunchItem, value)) RaiseCmds(); } }
    public ObservableCollection<CloseoutChecklistItem> ChecklistItems => _checklistItems;
    public ObservableCollection<PunchItem> PunchItems => _punchItems;
    public ObservableCollection<CloseoutSnapshot> Snapshots => _snapshots;

    public ICommand NavigateTabCommand { get; }

    public ICommand RefreshCommand { get; }
    public ICommand CreateCloseoutCommand { get; }
    public ICommand SubmitCloseoutCommand { get; }
    public ICommand ApproveReadinessCommand { get; }
    public ICommand ApplyTemplateCommand { get; }
    public ICommand VerifyItemCommand { get; }
    public ICommand CreatePunchItemCommand { get; }
    public ICommand ClosePunchItemCommand { get; }
    public ICommand ReopenPunchItemCommand { get; }
    public ICommand RunReconciliationCommand { get; }
    public ICommand CalculateReadinessCommand { get; }
    public ICommand CreateSnapshotCommand { get; }

    private void OnTabChanged()
    {
        OnPropertyChanged(nameof(IsGeneralTab));
        OnPropertyChanged(nameof(IsChecklistTab));
        OnPropertyChanged(nameof(IsPunchTab));
        OnPropertyChanged(nameof(IsReconciliationTab));
        OnPropertyChanged(nameof(IsReadinessTab));
    }

    private void OnNavigateTab(string? tabIndex)
    {
        if (int.TryParse(tabIndex, out var idx))
            SelectedTabIndex = idx;
    }

    private void RaiseCmds()
    {
        if (RefreshCommand is RelayCommand rc) rc.RaiseCanExecuteChanged();
        if (CreateCloseoutCommand is RelayCommand c1) c1.RaiseCanExecuteChanged();
        if (SubmitCloseoutCommand is RelayCommand c2) c2.RaiseCanExecuteChanged();
        if (ApproveReadinessCommand is RelayCommand c3) c3.RaiseCanExecuteChanged();
        if (ApplyTemplateCommand is RelayCommand c4) c4.RaiseCanExecuteChanged();
        if (VerifyItemCommand is RelayCommand c5) c5.RaiseCanExecuteChanged();
        if (CreatePunchItemCommand is RelayCommand c6) c6.RaiseCanExecuteChanged();
        if (ClosePunchItemCommand is RelayCommand c7) c7.RaiseCanExecuteChanged();
        if (ReopenPunchItemCommand is RelayCommand c8) c8.RaiseCanExecuteChanged();
        if (RunReconciliationCommand is RelayCommand c9) c9.RaiseCanExecuteChanged();
        if (CalculateReadinessCommand is RelayCommand c10) c10.RaiseCanExecuteChanged();
        if (CreateSnapshotCommand is RelayCommand c11) c11.RaiseCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var projectPath = _host.Session.CurrentProjectPath;
            if (projectPath == null)
            {
                StatusMessage = "No hay proyecto abierto.";
                return;
            }
            var projectId = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            if (projectId == null) return;
            var service = _host.Services.ProjectCloseoutService;
            if (service != null)
            {
                _closeout = await service.GetByProjectAsync(projectId);
                if (_closeout != null)
                {
                    CloseoutCode = _closeout.Code;
                    CloseoutName = _closeout.Name;
                    CloseoutStatus = _closeout.Status.ToString();
                    CloseoutProject = _closeout.ProjectId;
                }
            }
            StatusMessage = _closeout != null ? "Cierre cargado." : "Sin cierre activo.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCmds(); }
    }

    private async Task CreateCloseoutAsync()
    {
        if (_closeout != null) return;
        var projectPath = _host.Session.CurrentProjectPath;
        if (projectPath == null) return;
        try
        {
            var projectId = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            if (projectId == null) return;
            var service = _host.Services.ProjectCloseoutService;
            if (service == null) { ErrorMessage = "Servicio no disponible."; return; }
            _closeout = await service.CreateAsync(projectId, $"CIERRE-{projectId}", $"Cierre {projectId}", _host.Session.CurrentUserName);
            CloseoutCode = _closeout.Code;
            CloseoutName = _closeout.Name;
            CloseoutStatus = _closeout.Status.ToString();
            CloseoutProject = _closeout.ProjectId;
            StatusMessage = "Cierre iniciado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { RaiseCmds(); }
    }

    private async Task SubmitCloseoutAsync()
    {
        if (_closeout == null) return;
        try
        {
            _closeout.Status = QubiCostElectrical.CloseoutStatus.UnderReview;
            var svc = _host.Services.ProjectCloseoutService;
            if (svc != null) await svc.SaveAsync(_closeout, _host.Session.CurrentUserName);
            CloseoutStatus = _closeout.Status.ToString();
            StatusMessage = "Enviado a revisión.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { RaiseCmds(); }
    }

    private async Task ApproveReadinessAsync()
    {
        if (_closeout == null) return;
        try
        {
            _closeout.Status = QubiCostElectrical.CloseoutStatus.ReadyForProvisionalReception;
            var svc = _host.Services.ProjectCloseoutService;
            if (svc != null) await svc.SaveAsync(_closeout, _host.Session.CurrentUserName);
            CloseoutStatus = _closeout.Status.ToString();
            StatusMessage = "Readiness aprobado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { RaiseCmds(); }
    }

    private async Task ApplyTemplateAsync()
    {
        if (_closeout == null) return;
        try
        {
            var svc = _host.Services.CloseoutChecklistService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            var templates = await svc.ListTemplatesAsync();
            var template = templates.FirstOrDefault();
            if (template == null) { StatusMessage = "No hay plantillas disponibles."; return; }
            var checklist = await svc.ApplyTemplateAsync(_closeout.Id, template.Id);
            var items = await svc.GetItemsAsync(checklist.Id);
            _checklistItems.Clear();
            foreach (var item in items) _checklistItems.Add(item);
            StatusMessage = $"Check-list '{checklist.Name}' aplicado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task VerifyItemAsync()
    {
        if (_selectedChecklistItem == null) return;
        try
        {
            var svc = _host.Services.CloseoutChecklistService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            await svc.VerifyItemAsync(_selectedChecklistItem.Id, _host.Session.CurrentUserName);
            StatusMessage = "Ítem verificado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task CreatePunchItemAsync()
    {
        if (_closeout == null) return;
        try
        {
            var svc = _host.Services.PunchListService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            var item = await svc.CreateAsync(_closeout.Id.ToString(), "Nuevo ítem", _host.Session.CurrentUserName, _host.Session.CurrentUserName);
            _punchItems.Add(item);
            StatusMessage = "Ítem creado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task ClosePunchItemAsync()
    {
        if (_selectedPunchItem == null) return;
        try
        {
            var svc = _host.Services.PunchListService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            await svc.CloseAsync(_selectedPunchItem.Id, _host.Session.CurrentUserName);
            StatusMessage = "Ítem cerrado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task ReopenPunchItemAsync()
    {
        if (_selectedPunchItem == null) return;
        try
        {
            var svc = _host.Services.PunchListService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            await svc.ReopenAsync(_selectedPunchItem.Id, "Reabierto", _host.Session.CurrentUserName);
            StatusMessage = "Ítem reabierto.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task RunReconciliationAsync()
    {
        if (_closeout == null) return;
        try
        {
            var svc = _host.Services.CloseoutReconciliationService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            var projectPath = _host.Session.CurrentProjectPath ?? string.Empty;
            var projectId = System.IO.Path.GetFileNameWithoutExtension(projectPath) ?? string.Empty;
            var result = await svc.ReconcileAsync(projectId, _closeout.Id.ToString(), DateTime.Now.ToString("yyyy-MM-dd"), "CLP");
            var parts = result.Dimensions.Select(d => $"{d.Dimension}: {d.Pct:F0}%");
            ReconciliationSummary = string.Join(", ", parts);
            StatusMessage = result.IsReady ? "Conciliación: lista." : "Conciliación: con observaciones.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task CalculateReadinessAsync()
    {
        if (_closeout == null) return;
        try
        {
            var svc = _host.Services.CloseoutReadinessService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            var result = await svc.CalculateAsync(_closeout.Id.ToString());
            ReadinessGlobalPct = (decimal)result.GlobalPct;
            ReadinessDocPct = (decimal)(result.Dimensions.FirstOrDefault(d => d.Dimension == QubiCostElectrical.CloseoutDimension.Documental)?.Pct ?? 0);
            ReadinessTechPct = (decimal)(result.Dimensions.FirstOrDefault(d => d.Dimension == QubiCostElectrical.CloseoutDimension.Technical)?.Pct ?? 0);
            ReadinessEcoPct = (decimal)(result.Dimensions.FirstOrDefault(d => d.Dimension == QubiCostElectrical.CloseoutDimension.Economic)?.Pct ?? 0);
            StatusMessage = $"Readiness: {(result.IsReady ? "Listo" : "Pendiente")} ({result.GlobalPct:F0}%)";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task CreateSnapshotAsync()
    {
        if (_closeout == null) return;
        try
        {
            var svc = _host.Services.CloseoutReadinessService;
            if (svc == null) { ErrorMessage = "Servicio no disponible."; return; }
            var snapshot = await svc.CreateSnapshotAsync(_closeout.Id.ToString(), "Snapshot manual", _host.Session.CurrentUserName);
            _snapshots.Add(snapshot);
            StatusMessage = "Snapshot creado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        _closeout = null;
        _checklistItems.Clear();
        _punchItems.Clear();
        _snapshots.Clear();
        _ = LoadAsync();
    }
}
