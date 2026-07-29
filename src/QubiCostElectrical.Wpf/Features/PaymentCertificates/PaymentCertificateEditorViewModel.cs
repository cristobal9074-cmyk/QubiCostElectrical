using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public sealed class PaymentCertificateEditorViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private PaymentCertificate _certificate;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public PaymentCertificateEditorViewModel(IShellNavigationHost shell, WpfAppHost host, PaymentCertificate certificate) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !_isBusy && !IsReadOnly);
        CancelCommand = new RelayCommand(() => GoBack());
        RecalculateCommand = new RelayCommand(async () => await RecalculateAsync(), () => !_isBusy && !IsReadOnly);
    }

    public PaymentCertificate Certificate
    {
        get => _certificate;
        set => SetProperty(ref _certificate, value);
    }

    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RecalculateCommand { get; }

    public async Task SaveAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio de estados de pago no disponible.");

            if (string.IsNullOrEmpty(_certificate.Id?.Value))
            {
                await svc.CreateAsync(_certificate, _host.Session.CurrentUserName ?? "Admin");
            }
            else
            {
                await svc.UpdateDraftAsync(_certificate, _certificate.RowVersion, _host.Session.CurrentUserName ?? "Admin");
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

    public async Task RecalculateAsync()
    {
        if (_isBusy || IsReadOnly) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.PaymentCertificateService;
            if (svc == null) throw new InvalidOperationException("Servicio de estados de pago no disponible.");

            var calculationResult = await svc.RecalculateAsync(_certificate.Id);
            var updated = await svc.ApplyCalculationAsync(_certificate.Id, calculationResult, _certificate.RowVersion, _host.Session.CurrentUserName ?? "Admin");
            Certificate = updated;
            ErrorMessage = string.Empty;
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
