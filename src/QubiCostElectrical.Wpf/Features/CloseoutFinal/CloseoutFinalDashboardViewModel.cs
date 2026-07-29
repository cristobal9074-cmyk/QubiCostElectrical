using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public sealed class CloseoutFinalDashboardViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private string _finalResult = string.Empty;

    public CloseoutFinalDashboardViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string FinalResult { get => _finalResult; private set => SetProperty(ref _finalResult, value); }
    public ICommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var projectPath = _host.Session.CurrentProjectPath;
            if (projectPath == null) { StatusMessage = "No hay proyecto abierto."; return; }
            var projectId = System.IO.Path.GetFileNameWithoutExtension(projectPath) ?? "";
            var svc = _host.Services.FinalCloseoutService;
            if (svc != null)
            {
                var result = await svc.CalculateFinalResultAsync(projectId, projectId);
                FinalResult = $"Resultado final: {result.Financial.FinalBalance:F2}";
            }
            StatusMessage = "Dashboard final cargado.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; RaiseCommands(); }
    }

    private void RaiseCommands()
    {
        if (RefreshCommand is RelayCommand rc) rc.RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        _ = LoadAsync();
    }
}
