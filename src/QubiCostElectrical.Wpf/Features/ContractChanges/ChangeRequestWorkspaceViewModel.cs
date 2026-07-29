using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.ContractChanges;

public sealed class ChangeRequestWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ChangeRequest> _items = new();
    private ChangeRequest? _selected;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ChangeRequestWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        ExportCsvCommand = new RelayCommand(async () => await ExportCsvAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<ChangeRequest> Items => _items;

    public ChangeRequest? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
                RaiseCmds();
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
    public ICommand ExportCsvCommand { get; }

    private void RaiseCmds()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        if (_host.Services.ContractChangeService is null)
        {
            ErrorMessage = "Servicio de cambios contractuales no disponible.";
            return;
        }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var projectPath = _host.Session.CurrentProjectPath ?? string.Empty;
            var list = await _host.Services.ContractChangeService.SearchChangeRequestsAsync(projectPath, SearchText, null, 0, 200);
            _items.Clear();
            foreach (var c in list)
                _items.Add(c);
            StatusMessage = $"{list.Count} solicitud(es) de cambio cargada(s).";
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

    private async Task ExportCsvAsync()
    {
        if (_host.Services.ContractChangeService is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Exportar solicitudes de cambio a CSV",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = "SolicitudesDeCambio.csv"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var csv = await _host.Services.ContractChangeService.ExportChangeRequestsCsvAsync(ContractId.New());
            await File.WriteAllTextAsync(dialog.FileName, csv);
            StatusMessage = $"Exportado a {dialog.FileName}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al exportar: {ex.Message}";
        }
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        RaiseCmds();
        _ = LoadAsync();
    }
}
