using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public sealed class CloseoutReceptionViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private int _selectedTabIndex;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private readonly ObservableCollection<ProjectReception> _receptions = new();

    public CloseoutReceptionViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public ObservableCollection<ProjectReception> Receptions => _receptions;
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
            var projectId = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            var svc = _host.Services.ProjectReceptionService;
            if (svc != null)
            {
                _receptions.Clear();
                var list = await svc.ListAsync(projectId ?? "");
                foreach (var r in list) _receptions.Add(r);
            }
            StatusMessage = "Recepción cargada.";
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
        _receptions.Clear();
        _ = LoadAsync();
    }
}
