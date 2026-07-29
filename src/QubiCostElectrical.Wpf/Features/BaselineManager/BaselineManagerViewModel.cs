using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.BaselineManager;

public sealed class BaselineManagerViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ScheduleBaseline> _baselines = new();
    private readonly ObservableCollection<ActivityDiff> _activityDifferences = new();
    
    private ScheduleBaseline? _selectedBaselineA;
    private ScheduleBaseline? _selectedBaselineB;
    private ScheduleComparisonResult? _comparisonResult;
    private string _newBaselineName = string.Empty;
    private string _newBaselineDescription = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private bool _hasComparison;

    public BaselineManagerViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        CreateBaselineCommand = new RelayCommand(async () => await CreateBaselineAsync(), CanCreateBaseline);
        RestoreBaselineCommand = new RelayCommand(async () => await RestoreBaselineAsync(), () => SelectedBaselineA != null);
        DeleteBaselineCommand = new RelayCommand(async () => await DeleteBaselineAsync(), () => SelectedBaselineA != null && !SelectedBaselineA.IsOfficial);
        CompareCommand = new RelayCommand(async () => await CompareAsync(), () => SelectedBaselineA != null);
        MarkOfficialCommand = new RelayCommand(async () => await MarkOfficialAsync(), () => SelectedBaselineA != null && !SelectedBaselineA.IsOfficial);
        VerifyIntegrityCommand = new RelayCommand(async () => await VerifyIntegrityAsync(), () => SelectedBaselineA != null);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
    }

    public ObservableCollection<ScheduleBaseline> Baselines => _baselines;
    public ObservableCollection<ActivityDiff> ActivityDifferences => _activityDifferences;

    public ScheduleBaseline? SelectedBaselineA
    {
        get => _selectedBaselineA;
        set
        {
            if (SetProperty(ref _selectedBaselineA, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public ScheduleBaseline? SelectedBaselineB
    {
        get => _selectedBaselineB;
        set
        {
            if (SetProperty(ref _selectedBaselineB, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public ScheduleComparisonResult? ComparisonResult
    {
        get => _comparisonResult;
        private set => SetProperty(ref _comparisonResult, value);
    }

    public string NewBaselineName
    {
        get => _newBaselineName;
        set
        {
            if (SetProperty(ref _newBaselineName, value))
            {
                ((RelayCommand)CreateBaselineCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NewBaselineDescription
    {
        get => _newBaselineDescription;
        set => SetProperty(ref _newBaselineDescription, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasComparison
    {
        get => _hasComparison;
        private set => SetProperty(ref _hasComparison, value);
    }

    public ICommand CreateBaselineCommand { get; }
    public ICommand RestoreBaselineCommand { get; }
    public ICommand DeleteBaselineCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand MarkOfficialCommand { get; }
    public ICommand VerifyIntegrityCommand { get; }
    public ICommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_host.Session.CurrentProjectPath) || _host.Services.ScheduleBaseline == null)
        {
            StatusMessage = "No active project loaded.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading baselines...";
        try
        {
            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath);
            var firstSched = schedules.FirstOrDefault();
            if (firstSched == null)
            {
                StatusMessage = "No active schedule found for the project. Please create a schedule first.";
                Baselines.Clear();
                return;
            }

            var list = await _host.Services.ScheduleBaseline!.ListBaselinesAsync(firstSched.Id);
            Baselines.Clear();
            foreach (var b in list)
            {
                Baselines.Add(b);
            }
            StatusMessage = $"Loaded {Baselines.Count} baseline(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading baselines: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanCreateBaseline()
    {
        return !string.IsNullOrWhiteSpace(NewBaselineName) && !string.IsNullOrEmpty(_host.Session.CurrentProjectPath);
    }

    private async Task CreateBaselineAsync()
    {
        if (!CanCreateBaseline()) return;

        IsLoading = true;
        StatusMessage = "Creating baseline snapshot...";
        try
        {
            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath);
            var sched = schedules.First();
            await _host.Services.ScheduleBaseline!.CreateBaselineAsync(sched.Id, NewBaselineName, NewBaselineDescription, _host.Session.ProjectInfo.UsuarioNombre ?? "System");
            
            NewBaselineName = string.Empty;
            NewBaselineDescription = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating baseline: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RestoreBaselineAsync()
    {
        if (SelectedBaselineA == null) return;
        
        IsLoading = true;
        StatusMessage = "Restoring baseline snapshot...";
        try
        {
            await _host.Services.ScheduleBaseline!.RestoreBaselineAsync(SelectedBaselineA.BaselineId, _host.Session.ProjectInfo.UsuarioNombre ?? "System");
            StatusMessage = $"Baseline version {SelectedBaselineA.Version} restored successfully.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error restoring baseline: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteBaselineAsync()
    {
        if (SelectedBaselineA == null || SelectedBaselineA.IsOfficial) return;
        
        IsLoading = true;
        StatusMessage = "Deleting draft baseline...";
        try
        {
            await _host.Services.ScheduleBaseline!.DeleteDraftBaselineAsync(SelectedBaselineA.BaselineId);
            StatusMessage = "Draft baseline deleted.";
            SelectedBaselineA = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting baseline: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CompareAsync()
    {
        if (SelectedBaselineA == null) return;

        IsLoading = true;
        StatusMessage = "Comparing schedules...";
        try
        {
            ScheduleComparisonResult result;
            if (SelectedBaselineB != null)
            {
                result = await _host.Services.ScheduleBaseline!.CompareBaselinesAsync(SelectedBaselineA.BaselineId, SelectedBaselineB.BaselineId);
            }
            else
            {
                var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath);
                var sched = schedules.First();
                result = await _host.Services.ScheduleBaseline!.CompareWithCurrentAsync(sched.Id, SelectedBaselineA.BaselineId);
            }

            ComparisonResult = result;
            ActivityDifferences.Clear();
            foreach (var diff in result.ActivityDifferences)
            {
                ActivityDifferences.Add(diff);
            }
            HasComparison = true;
            StatusMessage = $"Comparison loaded. Added: {result.AddedActivitiesCount}, Deleted: {result.DeletedActivitiesCount}, Modified: {result.ModifiedActivitiesCount}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error comparing baselines: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task MarkOfficialAsync()
    {
        if (SelectedBaselineA == null || SelectedBaselineA.IsOfficial) return;

        IsLoading = true;
        StatusMessage = "Marking baseline as official...";
        try
        {
            await _host.Services.ScheduleBaseline!.MarkOfficialAsync(SelectedBaselineA.BaselineId);
            StatusMessage = "Baseline marked as official.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error marking baseline official: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task VerifyIntegrityAsync()
    {
        if (SelectedBaselineA == null) return;

        IsLoading = true;
        StatusMessage = "Verifying snapshot integrity...";
        try
        {
            bool ok = await _host.Services.ScheduleBaseline!.VerifyIntegrityAsync(SelectedBaselineA.BaselineId);
            StatusMessage = ok ? "Integrity verification PASSED. Snapshot matches original hash." : "Integrity verification FAILED. Snapshot was modified or corrupted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error verifying integrity: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCommandStates()
    {
        ((RelayCommand)RestoreBaselineCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteBaselineCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CompareCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MarkOfficialCommand).RaiseCanExecuteChanged();
        ((RelayCommand)VerifyIntegrityCommand).RaiseCanExecuteChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
        {
            Baselines.Clear();
            ActivityDifferences.Clear();
            ComparisonResult = null;
            HasComparison = false;
            SelectedBaselineA = null;
            SelectedBaselineB = null;
            StatusMessage = "No active project loaded.";
        }
        else if (e.Kind == ProjectSessionChangeKind.ProjectInfo || e.Kind == ProjectSessionChangeKind.Saved)
        {
            _ = LoadAsync();
        }
    }
}
