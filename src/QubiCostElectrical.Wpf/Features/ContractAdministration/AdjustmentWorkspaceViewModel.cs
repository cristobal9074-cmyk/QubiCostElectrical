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

namespace QubiCostElectrical.Wpf.Features.ContractAdministration;

public sealed class AdjustmentWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ContractAdjustment> _items = new();
    private ContractAdjustment? _selected;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public AdjustmentWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        ExportCsvCommand = new RelayCommand(async () => await ExportCsvAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<ContractAdjustment> Items => _items;

    public ContractAdjustment? Selected
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
        if (_host.Services.ContractAdministrationService is null)
        {
            ErrorMessage = "Servicio de reajustes contractuales no disponible.";
            return;
        }
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var projectPath = _host.Session.CurrentProjectPath ?? string.Empty;
            var contractId = ContractId.From(projectPath);
            var list = await _host.Services.ContractAdministrationService.GetAdjustmentsByContractAsync(contractId);
            if (!string.IsNullOrWhiteSpace(SearchText))
                list = list.Where(a => a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || a.Responsible.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            _items.Clear();
            foreach (var item in list)
                _items.Add(item);
            StatusMessage = $"{list.Count} reajuste(s) cargado(s).";
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
        if (_host.Services.ContractAdministrationService is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Exportar reajustes a CSV",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = "ReajustesContractuales.csv"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var csv = await _host.Services.ContractAdministrationService.ExportAdjustmentsCsvAsync(ContractId.New());
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
