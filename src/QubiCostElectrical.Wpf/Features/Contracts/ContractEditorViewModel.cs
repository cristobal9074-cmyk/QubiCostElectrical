using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractEditorViewModel : WorkspacePageViewModelBase
{
    private readonly IShellNavigationHost _shell;

    private Contract _contract;

    private string _errorMessage = string.Empty;

    public Contract Contract
    {
        get => _contract;
        set => SetProperty(ref _contract, value, nameof(Contract));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
    }

    public bool IsReadOnly => Session.IsReadOnly;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    private readonly IProjectDbContext projectDb;

    public ContractEditorViewModel(IShellNavigationHost shell, WpfAppHost host, Contract contract)
        : this(shell, host.Session, host.ProjectWorkspace, host.Services.ProjectDb, contract) { }

    public ContractEditorViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IProjectDbContext projectDb, Contract contract)
        : base(session, workspace)
    {
        this.projectDb = projectDb;
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsLoading && CanEdit);
        CancelCommand = new RelayCommand(GoBack);
        SyncWorkspaceGate();
    }

    public async Task SaveAsync()
    {
        if (IsLoading || !CanEdit)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        try
        {
            IContractService? contractService = projectDb.Contracts;
            if (contractService == null)
                throw new InvalidOperationException("Servicio de contratos no disponible.");

            if (!string.IsNullOrEmpty(_contract.Id?.Value))
                await contractService.UpdateDraftAsync(_contract, _contract.RowVersion, Session.CurrentUserName ?? "Admin");
            else
                await contractService.CreateAsync(_contract, Session.CurrentUserName ?? "Admin");

            GoBack();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void GoBack()
    {
        _shell.RequestNavigate(ShellRoute.ContractWorkspace);
    }

    protected override void OnWorkspaceGateChanged()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        SyncWorkspaceGate();
        OnPropertyChanged(nameof(IsReadOnly));
    }
}
