using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public sealed class ContractVersionsViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ContractId _contractId;
    private readonly ObservableCollection<ContractVersion> _versions = new();
    private ContractVersion? _selected;
    private bool _isBusy;
    private string _integrityMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public ContractVersionsViewModel(IShellNavigationHost shell, WpfAppHost host, ContractId contractId) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _contractId = contractId ?? throw new ArgumentNullException(nameof(contractId));

        VerifyIntegrityCommand = new RelayCommand(async () => await VerifySelectedIntegrityAsync(), () => Selected != null && !_isBusy);
        BackCommand = new RelayCommand(() => GoBack());

        _ = LoadVersionsAsync();
    }

    public ObservableCollection<ContractVersion> Versions => _versions;

    public ContractVersion? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                IntegrityMessage = string.Empty;
                (VerifyIntegrityCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string IntegrityMessage { get => _integrityMessage; private set => SetProperty(ref _integrityMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public ICommand VerifyIntegrityCommand { get; }
    public ICommand BackCommand { get; }

    public async Task LoadVersionsAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.ContractService;
            if (svc == null) throw new InvalidOperationException("Servicio de contratos no disponible.");

            var list = await svc.GetVersionsAsync(_contractId);
            _versions.Clear();
            foreach (var v in list)
            {
                _versions.Add(v);
            }
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

    private async Task VerifySelectedIntegrityAsync()
    {
        if (Selected == null || _isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        IntegrityMessage = string.Empty;
        try
        {
            var svc = _host.Services.ContractService;
            if (svc == null) throw new InvalidOperationException("Servicio de contratos no disponible.");

            var verified = await svc.VerifyIntegrityAsync(Selected.Id);
            IntegrityMessage = $"✓ Integridad Verificada. Hash coincidente: {verified.Hash}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"❌ ERROR DE INTEGRIDAD: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void GoBack()
    {
        _shell.RequestNavigate(ShellRoute.Apu); // Navigate back
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) { }
}
