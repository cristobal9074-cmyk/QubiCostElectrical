using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CalendarManager;

public sealed partial class CalendarManagerViewModel
{
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            StatusMessage = "Cargando calendarios...";
            var repo = host.Services.CalendarRepository;
            var result = await repo.GetAllAsync();
            calendars.Clear();
            foreach (var c in result)
                calendars.Add(c);
            StatusMessage = $"{calendars.Count} calendario(s) cargado(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NewCalendar()
    {
        IsEditing = true;
        CalendarCode = "";
        CalendarName = "";
        CalendarType = global::QubiCostElectrical.CalendarType.Corporate;
        weeklyPattern.Clear();
        exceptions.Clear();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            var wd = new WeeklyDayVm
            {
                DayOfWeek = day,
                IsWorkingDay = day != DayOfWeek.Saturday && day != DayOfWeek.Sunday,
            };
            if (wd.IsWorkingDay)
                wd.Shifts.Add(new ShiftVm { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), TotalHours = 4 });
            weeklyPattern.Add(wd);
        }
    }

    public async Task SaveCalendarAsync()
    {
        if (string.IsNullOrWhiteSpace(CalendarName)) return;
        try
        {
            StatusMessage = "Guardando calendario...";
            var repo = host.Services.CalendarRepository;
            var id = CalendarId.New();
            var calendar = new Calendar(id, CalendarCode.Trim(), CalendarName.Trim(), "Pacific/Easter", host.Session.CurrentUserName, CalendarType);
            foreach (var wd in weeklyPattern)
            {
                var day = new WeeklyWorkDay { DayOfWeek = wd.DayOfWeek, IsWorkingDay = wd.IsWorkingDay };
                day.Shifts.AddRange(wd.Shifts.Select(s => new WorkShift(s.StartTime, s.EndTime)));
                calendar.WeeklyPattern.Add(day);
            }
            foreach (var ex in exceptions)
            {
                if (DateTime.TryParse(ex.Date.ToString(), out var dt))
                {
                    calendar.Exceptions.Add(new CalendarException
                    {
                        Date = dt,
                        Type = ex.ExceptionType,
                        Reason = ex.Reason
                    });
                }
            }
            await repo.SaveAsync(calendar);
            IsEditing = false;
            await LoadAsync();
            StatusMessage = "Calendario guardado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    public async Task DeleteCalendarAsync()
    {
        var cal = SelectedCalendar;
        if (cal == null) return;
        try
        {
            StatusMessage = "Eliminando calendario...";
            var repo = host.Services.CalendarRepository;
            await repo.DeleteAsync(cal.Id);
            SelectedCalendar = null;
            await LoadAsync();
            StatusMessage = "Calendario eliminado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar: {ex.Message}";
        }
    }

    private void AddShift(WeeklyDayVm? day)
    {
        if (day == null) return;
        day.Shifts.Add(new ShiftVm { StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(18, 0), TotalHours = 4 });
    }

    public async Task AddExceptionAsync()
    {
        if (!DateTime.TryParse(ExceptionDate, out var dt)) return;
        try
        {
            exceptions.Add(new ExceptionVm
            {
                Id = Guid.NewGuid().ToString("N"),
                Date = dt,
                ExceptionType = ExceptionType,
                Reason = ExceptionReason
            });
            ExceptionDate = "";
            ExceptionReason = "";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al agregar excepción: {ex.Message}";
        }
    }

    private void DeleteException(ExceptionVm? ex)
    {
        if (ex == null) return;
        exceptions.Remove(ex);
    }

    private void Reset()
    {
        calendars.Clear();
        weeklyPattern.Clear();
        exceptions.Clear();
        CalendarCode = "";
        CalendarName = "";
        StatusMessage = "";
        IsEditing = false;
    }
}
