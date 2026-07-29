using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public sealed class CloseoutLessonsViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private readonly ObservableCollection<LessonLearned> _lessons = new();

    public CloseoutLessonsViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        _ = LoadAsync();
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public ObservableCollection<LessonLearned> Lessons => _lessons;
    public ICommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var svc = _host.Services.LessonLearnedService;
            if (svc != null)
            {
                _lessons.Clear();
                var list = await svc.ListAsync(null);
                foreach (var l in list) _lessons.Add(l);
            }
            StatusMessage = "Lecciones cargadas.";
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
        _lessons.Clear();
        _ = LoadAsync();
    }
}
