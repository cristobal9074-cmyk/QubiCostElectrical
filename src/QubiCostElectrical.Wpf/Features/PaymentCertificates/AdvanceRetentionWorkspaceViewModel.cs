using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public sealed class AdvanceRetentionWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ContractAdvance> _advances = new();
    private readonly ObservableCollection<ContractRetention> _retentions = new();
    private ContractAdvance? _selectedAdvance;
    private ContractRetention? _selectedRetention;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private ContractId? _currentContractId;

    public AdvanceRetentionWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        AddAdvanceCommand = new RelayCommand(async () => await AddAdvanceAsync(), () => !_isBusy && !IsReadOnly && _currentContractId != null);
        ReleaseRetentionCommand = new RelayCommand(async () => await ReleaseRetentionAsync(), () => SelectedRetention != null && !_isBusy && !IsReadOnly);
        BackCommand = new RelayCommand(() => GoBack(), () => !_isBusy);
    }

    public ObservableCollection<ContractAdvance> Advances => _advances;
    public ObservableCollection<ContractRetention> Retentions => _retentions;

    public ContractAdvance? SelectedAdvance
    {
        get => _selectedAdvance;
        set => SetProperty(ref _selectedAdvance, value);
    }

    public ContractRetention? SelectedRetention
    {
        get => _selectedRetention;
        set => SetProperty(ref _selectedRetention, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand AddAdvanceCommand { get; }
    public ICommand ReleaseRetentionCommand { get; }
    public ICommand BackCommand { get; }

    public async Task LoadForContractAsync(ContractId contractId)
    {
        _currentContractId = contractId;
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio de estados de pago no disponible.");

            var advances = await svc.GetAdvancesByContractAsync(contractId);
            _advances.Clear();
            foreach (var a in advances) _advances.Add(a);

            var retentions = await svc.GetRetentionsByContractAsync(contractId);
            _retentions.Clear();
            foreach (var r in retentions) _retentions.Add(r);

            StatusMessage = $"{advances.Count} anticipo(s), {retentions.Count} retención(es).";
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

    private async Task AddAdvanceAsync()
    {
        if (_currentContractId == null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio no disponible.");

            var advance = new ContractAdvance
            {
                ContractId = _currentContractId,
                Amount = 0,
                Date = DateTime.Today,
                Status = "Active"
            };
            await svc.CreateAdvanceAsync(advance, _host.Session.CurrentUserName ?? "Admin");
            await LoadForContractAsync(_currentContractId);
            StatusMessage = "Anticipo creado.";
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

    private async Task ReleaseRetentionAsync()
    {
        if (SelectedRetention == null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio no disponible.");

            await svc.ReleaseRetentionAsync(SelectedRetention.Id, SelectedRetention.LimitAmount, _host.Session.CurrentUserName ?? "Admin", "Liberación manual");
            if (_currentContractId != null)
                await LoadForContractAsync(_currentContractId);
            StatusMessage = "Retención liberada.";
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
        _shell.RequestNavigate(ShellRoute.PaymentCertificateWorkspace);
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
    }
}
