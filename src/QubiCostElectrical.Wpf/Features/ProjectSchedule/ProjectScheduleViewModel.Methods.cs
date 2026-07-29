using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Features.ActivityEditor;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ProjectSchedule;

public sealed partial class ProjectScheduleViewModel
{
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            StatusMessage = "Cargando cronogramas...";
            var repo = host.Services.ScheduleRepository;
            var projectId = host.Session.CurrentProjectPath;
            var schedules = await repo.GetByProjectAsync(projectId);
            summaries.Clear();
            foreach (var s in schedules)
            {
                summaries.Add(new ScheduleSummary
                {
                    Id = s.Id,
                    Name = s.Name,
                    Status = s.Status,
                    ProjectStartDate = s.ProjectStartDate,
                    TargetEndDate = s.TargetEndDate
                });
            }
            StatusMessage = $"{summaries.Count} cronograma(s) cargado(s).";
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

    private void NewSchedule()
    {
        IsEditing = true;
        ScheduleName = "";
        TimeZoneId = "Pacific/Easter";
        ProjectStartDate = null;
        TargetEndDate = null;
        Status = ScheduleStatus.Draft;
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ScheduleName)) return;
        try
        {
            StatusMessage = "Guardando cronograma...";
            var repo = host.Services.ScheduleRepository;
            var id = ScheduleId.New();
            var calRepo = host.Services.CalendarRepository;
            var calendars = await calRepo.GetAllAsync();
            var defaultCalId = calendars.FirstOrDefault()?.Id ?? CalendarId.New();
            var schedule = new Schedule(id, ScheduleName.Trim(), TimeZoneId, defaultCalId, host.Session.CurrentUserName)
            {
                ProjectStartDate = ProjectStartDate,
                TargetEndDate = TargetEndDate,
                Status = Status
            };
            await repo.SaveAsync(schedule);
            IsEditing = false;
            await LoadAsync();
            StatusMessage = "Cronograma guardado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    public async Task DeleteAsync()
    {
        var summary = SelectedSummary;
        if (summary == null) return;
        try
        {
            StatusMessage = "Eliminando cronograma...";
            var repo = host.Services.ScheduleRepository;
            await repo.DeleteAsync(summary.Id);
            SelectedSummary = null;
            await LoadAsync();
            StatusMessage = "Cronograma eliminado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar: {ex.Message}";
        }
    }

    private void OpenWbs()
    {
        if (SelectedSummary == null) return;
        shell.RequestNavigate(ShellRoute.Itemized);
    }

    private void OpenCalendar()
    {
        if (SelectedSummary == null) return;
        shell.RequestNavigate(ShellRoute.Preferences);
    }

    private async Task LoadScheduleDetailsAsync()
    {
        if (SelectedSummary == null)
        {
            activities.Clear();
            criticalPathActivities.Clear();
            return;
        }

        try
        {
            var schedId = SelectedSummary.Id;
            var activityRepo = host.Services.ActivityRepository;
            var allActs = await activityRepo.GetByScheduleAsync(schedId);

            activities.Clear();
            criticalPathActivities.Clear();

            var projectStart = SelectedSummary?.ProjectStartDate ?? DateTime.Today;
            foreach (var a in allActs)
            {
                var vm = new ActivityVm
                {
                    Id = a.Id,
                    Code = a.Code,
                    Name = a.Name,
                    Type = a.Type,
                    Status = a.Status,
                    ResponsibleId = a.ResponsibleId,
                    CalendarId = a.CalendarId,
                    PlannedDuration = a.PlannedDuration,
                    PlannedStart = a.PlannedStart,
                    PlannedFinish = a.PlannedFinish,
                    PlannedProgressPct = a.PlannedProgressPct,
                    EarlyStart = a.EarlyStart,
                    EarlyFinish = a.EarlyFinish,
                    LateStart = a.LateStart,
                    LateFinish = a.LateFinish,
                    TotalFloat = a.TotalFloat,
                    FreeFloat = a.FreeFloat,
                    IsCritical = a.IsCritical,
                    GanttLeftOffset = a.EarlyStart.HasValue ? Math.Max(0, (a.EarlyStart.Value - projectStart).TotalDays * 15) : 0,
                    GanttWidth = (a.EarlyStart.HasValue && a.EarlyFinish.HasValue) ? Math.Max(10, (a.EarlyFinish.Value - a.EarlyStart.Value).TotalDays * 15) : 30,
                    GanttFloatWidth = a.TotalFloat.HasValue ? a.TotalFloat.Value * 15 : 0,
                    Author = a.Author
                };

                activities.Add(vm);
                if (a.IsCritical)
                {
                    criticalPathActivities.Add(vm);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar detalles: {ex.Message}";
        }
    }

    private async Task RecalculateCpmAsync()
    {
        if (SelectedSummary == null) return;
        try
        {
            StatusMessage = "Recalculando programación CPM...";
            var result = await host.Services.CpmCalculation.CalculateScheduleAsync(SelectedSummary.Id);
            if (result.Success)
            {
                StatusMessage = "Programación CPM recalculada exitosamente.";
                await LoadScheduleDetailsAsync();
            }
            else
            {
                StatusMessage = $"Error CPM: {result.ErrorMessage}";
                WpfDialogService.Error("Error de Red de Programación", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al recalcular: {ex.Message}";
        }
    }

    private void Reset()
    {
        summaries.Clear();
        activities.Clear();
        criticalPathActivities.Clear();
        ScheduleName = "";
        TimeZoneId = "Pacific/Easter";
        ProjectStartDate = null;
        TargetEndDate = null;
        StatusMessage = "";
        IsEditing = false;
    }
}
