using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractWorkspaceViewModel : WorkspacePageViewModelBase
{
    private readonly IShellNavigationHost _shell;

    private readonly ObservableCollection<Contract> _items = new ObservableCollection<Contract>();

    private readonly AsyncDebounceRunner _searchDebounce;

    private Contract? _selected;

    private string _search = string.Empty;

    private string _statusMessage = string.Empty;

    private string _errorMessage = string.Empty;

    public ObservableCollection<Contract> Items => _items;

    public Contract? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value, nameof(Selected)))
                RaiseCmds();
        }
    }

    public string SearchText
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value, nameof(SearchText)))
                _searchDebounce.Schedule();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
    }

    public bool IsReadOnly => Session.IsReadOnly;

    public ICommand RefreshCommand { get; }

    public ICommand CreateCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand VersionsCommand { get; }

    private readonly IProjectDbContext projectDb;
    private readonly IPermissionService permissions;

    public ContractWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Services.ProjectDb, host.Permissions) { }

    public ContractWorkspaceViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IProjectDbContext projectDb, IPermissionService permissions)
        : base(session, workspace)
    {
        this.projectDb = projectDb;
        this.permissions = permissions;
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _searchDebounce = new AsyncDebounceRunner(LoadAsync, TimeSpan.FromMilliseconds(250));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !IsLoading && !ShowEmptyState);
        CreateCommand = new RelayCommand(CreateNew, () => !IsLoading && CanEdit && permissions.CanContractCreate);
        EditCommand = new RelayCommand(EditSelected, () => Selected != null && !IsLoading && CanEdit);
        VersionsCommand = new RelayCommand(ViewVersions, () => Selected != null && !IsLoading && !ShowEmptyState);
        SyncWorkspaceGate();
        if (!ShowEmptyState)
            _ = LoadAsync();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoading || ShowEmptyState)
            return;

        if (projectDb.Contracts == null)
        {
            ErrorMessage = "Servicio de contratos no disponible.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        RaiseCmds();
        try
        {
            string projectPath = Session.CurrentProjectPath ?? string.Empty;
            List<Contract> list = await projectDb.Contracts.SearchAsync(projectPath, SearchText, null, 0, 200, ct);
            if (ct.IsCancellationRequested)
                return;

            _items.Clear();
            foreach (Contract item in list)
                _items.Add(item);

            StatusMessage = $"{list.Count} contrato(s) cargado(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseCmds();
            OnPropertyChanged(nameof(IsReadOnly));
        }
    }

    private void CreateNew()
    {
        if (!CanEdit)
            return;
        _shell.RequestNavigate(ShellRoute.ContractEditor, new ContractEditorNavContext(null));
    }

    private void EditSelected()
    {
        if (Selected != null && CanEdit)
            _shell.RequestNavigate(ShellRoute.ContractEditor, new ContractEditorNavContext(Selected));
    }

    private void ViewVersions()
    {
        if (Selected != null && !ShowEmptyState)
            _shell.RequestNavigate(ShellRoute.ContractVersions, new ContractVersionsNavContext(Selected.Id));
    }

    private void RaiseCmds()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (VersionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    protected override void OnWorkspaceGateChanged() => RaiseCmds();

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        SyncWorkspaceGate();
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        if (!ShowEmptyState)
            _ = LoadAsync();
        else
        {
            _items.Clear();
            StatusMessage = string.Empty;
            ErrorMessage = string.Empty;
        }
    }

    protected override void DisposeCore()
    {
        _searchDebounce.Dispose();
    }
}
