using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.OperationalDashboard;

public sealed partial class OperationalDashboardViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<DashboardOperationalRow> _rows = new();
    private ScheduleId? _scheduleId;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _criticalOnly;
    private bool _delayedOnly;
    private bool _withoutUpdateOnly;
    private bool _oosOnly;

    public OperationalDashboardViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        GoExecutiveCommand = new RelayCommand(() => _shell.RequestNavigate(ShellRoute.PlanningExecutiveDashboard));
        GoProgressCommand = new RelayCommand(() => _shell.RequestNavigate(ShellRoute.PlanningProgress));
    }

    public ObservableCollection<DashboardOperationalRow> Rows => _rows;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool CriticalOnly { get => _criticalOnly; set { if (SetProperty(ref _criticalOnly, value)) _ = LoadAsync(); } }
    public bool DelayedOnly { get => _delayedOnly; set { if (SetProperty(ref _delayedOnly, value)) _ = LoadAsync(); } }
    public bool WithoutUpdateOnly { get => _withoutUpdateOnly; set { if (SetProperty(ref _withoutUpdateOnly, value)) _ = LoadAsync(); } }
    public bool OosOnly { get => _oosOnly; set { if (SetProperty(ref _oosOnly, value)) _ = LoadAsync(); } }

    public ICommand RefreshCommand { get; }
    public ICommand GoExecutiveCommand { get; }
    public ICommand GoProgressCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) => _ = LoadAsync();

    public async System.Threading.Tasks.Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            if (_host.Services.ProjectDashboard is null || _host.Services.ScheduleRepository is null)
            {
                StatusMessage = "Servicios no inicializados.";
                return;
            }
            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath ?? string.Empty);
            var schedule = System.Linq.Enumerable.FirstOrDefault(schedules);
            if (schedule is null) { StatusMessage = "No hay cronograma."; return; }
            _scheduleId = schedule.Id;
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var filter = new DashboardFilter
            {
                CriticalOnly = CriticalOnly ? true : null,
                DelayedOnly = DelayedOnly ? true : null,
                WithoutUpdateOnly = WithoutUpdateOnly ? true : null,
                OutOfSequenceOnly = OosOnly ? true : null
            };
            var rows = await _host.Services.ProjectDashboard.GetOperationalAsync(schedule.Id, filter, actor);
            _rows.Clear();
            foreach (var r in rows) _rows.Add(r);
            StatusMessage = $"{_rows.Count} actividades · filtros aplicados";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }
}
