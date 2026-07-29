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

public sealed class PaymentCertificateWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<PaymentCertificate> _items = new();
    private PaymentCertificate? _selected;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public PaymentCertificateWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateCommand = new RelayCommand(() => CreateNew(), () => !_isBusy && !IsReadOnly && _host.Permissions.CanPaymentCertificateCreate);
        EditCommand = new RelayCommand(() => EditSelected(), () => Selected != null && !_isBusy && !IsReadOnly);
        ReviewCommand = new RelayCommand(() => ReviewSelected(), () => Selected != null && !_isBusy);
        AdvancesCommand = new RelayCommand(() => ShowAdvances(), () => Selected != null && !_isBusy);

        _ = LoadAsync();
    }

    public ObservableCollection<PaymentCertificate> Items => _items;

    public PaymentCertificate? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                RaiseCmds();
            }
        }
    }

    public string SearchText
    {
        get => _search;
        set { if (SetProperty(ref _search, value)) _ = LoadAsync(); }
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ReviewCommand { get; }
    public ICommand AdvancesCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        if (_host.Services.PaymentCertificateService is null)
        {
            ErrorMessage = "Servicio de estados de pago no disponible.";
            return;
        }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var projectPath = _host.Session.CurrentProjectPath ?? string.Empty;
            var list = await _host.Services.PaymentCertificateService.SearchAsync(projectPath, SearchText, null, 0, 200);
            _items.Clear();
            foreach (var c in list)
            {
                _items.Add(c);
            }
            StatusMessage = $"{list.Count} estado(s) de pago cargado(s).";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            RaiseCmds();
            OnPropertyChanged(nameof(IsReadOnly));
        }
    }

    private void CreateNew()
    {
        _shell.RequestNavigate(ShellRoute.PaymentCertificateEditor);
    }

    private void EditSelected()
    {
        if (Selected == null) return;
        _shell.RequestNavigate(ShellRoute.PaymentCertificateEditor);
    }

    private void ReviewSelected()
    {
        if (Selected == null) return;
        _shell.RequestNavigate(ShellRoute.ValuationReviewWorkspace);
    }

    private void ShowAdvances()
    {
        if (Selected == null) return;
        _shell.RequestNavigate(ShellRoute.AdvanceRetentionWorkspace);
    }

    private void RaiseCmds()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AdvancesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        _ = LoadAsync();
    }
}
