using System;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public sealed class CloseoutArchiveViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private string _manifestInfo = string.Empty;

    public CloseoutArchiveViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string ManifestInfo { get => _manifestInfo; private set => SetProperty(ref _manifestInfo, value); }
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
            var svc = _host.Services.ProjectArchiveService;
            if (svc != null)
            {
                var manifest = await svc.GetManifestAsync(projectId);
                ManifestInfo = manifest != null ? $"Manifiesto: {manifest.Status}" : "Sin manifiesto.";
            }
            StatusMessage = "Archivo cargado.";
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
