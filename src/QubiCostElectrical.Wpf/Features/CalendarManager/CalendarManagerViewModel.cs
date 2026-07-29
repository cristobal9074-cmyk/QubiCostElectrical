using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CalendarManager;

public sealed class ExceptionVm
{
    public string Id { get; init; } = "";
    public DateTime Date { get; init; }
    public CalendarExceptionType ExceptionType { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class ShiftVm
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public double TotalHours { get; set; }
}

public sealed class WeeklyDayVm
{
    public DayOfWeek DayOfWeek { get; init; }
    public bool IsWorkingDay { get; set; }
    public ObservableCollection<ShiftVm> Shifts { get; init; } = new();
}

public sealed partial class CalendarManagerViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<Calendar> calendars = new();
    private readonly ObservableCollection<WeeklyDayVm> weeklyPattern = new();
    private readonly ObservableCollection<ExceptionVm> exceptions = new();
    private Calendar? selectedCalendar;
    private bool isEditing;
    private string calendarCode = "";
    private string calendarName = "";
    private CalendarType calendarType;
    private string statusMessage = "";
    private bool isLoading;
    private string exceptionDate = "";
    private CalendarExceptionType exceptionType;
    private string exceptionReason = "";

    public static CalendarType[] CalendarTypeValues { get; } = (CalendarType[])Enum.GetValues(typeof(CalendarType));
    public static CalendarExceptionType[] ExceptionTypeValues { get; } = (CalendarExceptionType[])Enum.GetValues(typeof(CalendarExceptionType));
    public static DayOfWeek[] DayOfWeekValues { get; } = (DayOfWeek[])Enum.GetValues(typeof(DayOfWeek));

    public CalendarManagerViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        AddCalendarCommand = new RelayCommand(NewCalendar);
        SaveCalendarCommand = new RelayCommand(async () => await SaveCalendarAsync());
        DeleteCalendarCommand = new RelayCommand(async () => await DeleteCalendarAsync(), () => SelectedCalendar != null);
        AddShiftCommand = new RelayCommand<WeeklyDayVm?>(AddShift);
        AddExceptionCommand = new RelayCommand(async () => await AddExceptionAsync());
        DeleteExceptionCommand = new RelayCommand<ExceptionVm?>(DeleteException);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
    }

    public ObservableCollection<Calendar> Calendars => calendars;
    public Calendar? SelectedCalendar { get => selectedCalendar; set => SetProperty(ref selectedCalendar, value); }
    public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
    public string CalendarCode { get => calendarCode; set => SetProperty(ref calendarCode, value); }
    public string CalendarName { get => calendarName; set => SetProperty(ref calendarName, value); }
    public CalendarType CalendarType { get => calendarType; set => SetProperty(ref calendarType, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public ObservableCollection<WeeklyDayVm> WeeklyPattern => weeklyPattern;
    public ObservableCollection<ExceptionVm> Exceptions => exceptions;
    public string ExceptionDate { get => exceptionDate; set => SetProperty(ref exceptionDate, value); }
    public CalendarExceptionType ExceptionType { get => exceptionType; set => SetProperty(ref exceptionType, value); }
    public string ExceptionReason { get => exceptionReason; set => SetProperty(ref exceptionReason, value); }

    public ICommand AddCalendarCommand { get; }
    public ICommand SaveCalendarCommand { get; }
    public ICommand DeleteCalendarCommand { get; }
    public ICommand AddShiftCommand { get; }
    public ICommand AddExceptionCommand { get; }
    public ICommand DeleteExceptionCommand { get; }
    public ICommand RefreshCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Reset);
    }
}
