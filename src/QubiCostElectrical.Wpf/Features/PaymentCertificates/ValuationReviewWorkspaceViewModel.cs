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

public sealed class ValuationReviewWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private PaymentCertificate? _certificate;
    private readonly ObservableCollection<PaymentCertificateLine> _lines = new();
    private PaymentCertificateLine? _selectedLine;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ValuationReviewWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        ApproveCommand = new RelayCommand(async () => await ApproveAsync(), () => Certificate != null && !_isBusy && !IsReadOnly);
        ObserveCommand = new RelayCommand(async () => await ObserveAsync(), () => Certificate != null && !_isBusy && !IsReadOnly);
        RejectCommand = new RelayCommand(async () => await RejectAsync(), () => Certificate != null && !_isBusy && !IsReadOnly);
        BackCommand = new RelayCommand(() => GoBack(), () => !_isBusy);
    }

    public PaymentCertificate? Certificate
    {
        get => _certificate;
        set => SetProperty(ref _certificate, value);
    }

    public ObservableCollection<PaymentCertificateLine> Lines => _lines;

    public PaymentCertificateLine? SelectedLine
    {
        get => _selectedLine;
        set => SetProperty(ref _selectedLine, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand ApproveCommand { get; }
    public ICommand ObserveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand BackCommand { get; }

    public async Task LoadCertificateAsync(PaymentCertificateId id)
    {
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio de estados de pago no disponible.");

            Certificate = await svc.GetAsync(id);
            if (Certificate != null)
            {
                _lines.Clear();
                foreach (var line in Certificate.Lines)
                {
                    _lines.Add(line);
                }
                StatusMessage = $"Revisión: {Certificate.VisibleNumber}";
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

    private async Task ApproveAsync()
    {
        if (Certificate == null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio no disponible.");

            await svc.ApproveAsync(Certificate.Id, Certificate.RowVersion, _host.Session.CurrentUserName ?? "Admin");
            StatusMessage = "Estado de pago aprobado.";
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

    private async Task ObserveAsync()
    {
        if (Certificate == null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio no disponible.");

            await svc.ObserveAsync(Certificate.Id, Certificate.RowVersion, _host.Session.CurrentUserName ?? "Admin", "En revisión - observaciones pendientes");
            StatusMessage = "Observaciones registradas.";
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

    private async Task RejectAsync()
    {
        if (Certificate == null) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio no disponible.");

            await svc.RejectAsync(Certificate.Id, Certificate.RowVersion, _host.Session.CurrentUserName ?? "Admin", "Rechazado en revisión");
            StatusMessage = "Estado de pago rechazado.";
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
        _shell.RequestNavigate(ShellRoute.PaymentCertificateWorkspace);
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
    }
}
