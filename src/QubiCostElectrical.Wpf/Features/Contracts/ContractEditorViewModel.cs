using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractEditorViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private Contract _contract;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public ContractEditorViewModel(IShellNavigationHost shell, WpfAppHost host, Contract contract) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !_isBusy && !IsReadOnly);
        CancelCommand = new RelayCommand(() => GoBack());
    }

    public Contract Contract
    {
        get => _contract;
        set => SetProperty(ref _contract, value);
    }

    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task SaveAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.ContractService;
            if (svc == null) throw new InvalidOperationException("Servicio de contratos no disponible.");

            if (string.IsNullOrEmpty(_contract.Id?.Value))
            {
                await svc.CreateAsync(_contract, _host.Session.CurrentUserName ?? "Admin");
            }
            else
            {
                await svc.UpdateDraftAsync(_contract, _contract.RowVersion, _host.Session.CurrentUserName ?? "Admin");
            }
            GoBack();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void GoBack()
    {
        _shell.RequestNavigate(ShellRoute.Apu); // Navigate back to list / work area
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
    }
}
