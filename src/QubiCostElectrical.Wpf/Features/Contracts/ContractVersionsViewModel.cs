using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractVersionsViewModel : WorkspacePageViewModelBase
{
    private readonly IShellNavigationHost _shell;

    private ContractId _contractId;

    private readonly ObservableCollection<ContractVersion> _versions = new ObservableCollection<ContractVersion>();

    private ContractVersion? _selected;

    private string _integrityMessage = string.Empty;

    private string _errorMessage = string.Empty;

    public ObservableCollection<ContractVersion> Versions => _versions;

    public ContractVersion? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value, nameof(Selected)))
            {
                IntegrityMessage = string.Empty;
                (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string IntegrityMessage
    {
        get => _integrityMessage;
        private set => SetProperty(ref _integrityMessage, value, nameof(IntegrityMessage));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
    }

    public ICommand VerifyIntegrityCommand { get; }

    public ICommand BackCommand { get; }

    private readonly IProjectDbContext projectDb;

    public ContractVersionsViewModel(IShellNavigationHost shell, WpfAppHost host, ContractId contractId)
        : this(shell, host.Session, host.ProjectWorkspace, host.ProjectDb, contractId) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ContractVersionsViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IProjectDbContext projectDb, ContractId contractId)
        : base(session, workspace)
    {
        this.projectDb = projectDb;
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _contractId = contractId ?? throw new ArgumentNullException(nameof(contractId));
        VerifyIntegrityCommand = new RelayCommand(async () => await VerifySelectedIntegrityAsync(),
            () => Selected != null && !IsLoading && !ShowEmptyState);
        BackCommand = new RelayCommand(GoBack);
        SyncWorkspaceGate();
        if (!ShowEmptyState)
            _ = LoadVersionsAsync();
    }

    public async Task LoadVersionsAsync()
    {
        if (IsLoading || ShowEmptyState)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        try
        {
            List<ContractVersion> versions = await (projectDb.Contracts
                ?? throw new InvalidOperationException("Servicio de contratos no disponible.")).GetVersionsAsync(_contractId);
            _versions.Clear();
            foreach (ContractVersion item in versions)
                _versions.Add(item);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private async Task VerifySelectedIntegrityAsync()
    {
        if (Selected == null || IsLoading || ShowEmptyState)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        IntegrityMessage = string.Empty;
        (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        try
        {
            IntegrityMessage = "✓ Integridad Verificada. Hash coincidente: "
                + (await (projectDb.Contracts
                    ?? throw new InvalidOperationException("Servicio de contratos no disponible.")).VerifyIntegrityAsync(Selected.Id)).Hash;
        }
        catch (Exception ex)
        {
            ErrorMessage = "ERROR DE INTEGRIDAD: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
            (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public void LoadForContract(ContractId contractId)
    {
        _contractId = contractId ?? throw new ArgumentNullException(nameof(contractId));
        if (!ShowEmptyState)
            _ = LoadVersionsAsync();
    }

    private void GoBack()
    {
        _shell.RequestNavigate(ShellRoute.ContractWorkspace);
    }

    protected override void OnWorkspaceGateChanged()
    {
        (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        SyncWorkspaceGate();
        if (ShowEmptyState)
        {
            _versions.Clear();
            ErrorMessage = string.Empty;
            IntegrityMessage = string.Empty;
        }
        else
            _ = LoadVersionsAsync();
    }
}
