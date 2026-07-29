using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ActivityEditor;

public sealed partial class ActivityEditorViewModel
{
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            StatusMessage = "Cargando actividades...";
            var scheduleRepo = host.Services.ScheduleRepository;
            var projId = host.Session.CurrentProjectPath;
            var schedules = await scheduleRepo.GetByProjectAsync(projId);
            currentScheduleId = schedules.FirstOrDefault()?.Id;
            if (currentScheduleId == null)
            {
                StatusMessage = "No hay cronograma activo.";
                return;
            }

            var activityRepo = host.Services.ActivityRepository;
            var request = new ActivitySearchRequest
            {
                ScheduleId = currentScheduleId,
                Query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                MaxResults = 500
            };
            var result = await activityRepo.SearchAsync(request);
            activities.Clear();
            foreach (var a in result.Activities)
            {
                activities.Add(new ActivityVm
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
                    Author = a.Author
                });
            }

            var calRepo = host.Services.CalendarRepository;
            var allCals = await calRepo.GetAllAsync();
            calendars.Clear();
            foreach (var c in allCals)
                calendars.Add(c);

            StatusMessage = $"{result.FilteredCount} actividad(es) cargada(s).";
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

    private void NewActivity()
    {
        IsEditing = true;
        ActivityCode = "";
        ActivityName = "";
        ActivityType = global::QubiCostElectrical.ActivityType.Task;
        DurationValue = 1;
        DurationUnit = DurationUnit.Hours;
        SelectedCalendarId = null;
        ResponsibleId = "";
        Status = ActivityStatus.NotStarted;
    }

    public async Task SaveActivityAsync()
    {
        if (string.IsNullOrWhiteSpace(ActivityName)) return;
        if (currentScheduleId == null) return;
        try
        {
            StatusMessage = "Guardando actividad...";
            var repo = host.Services.ActivityRepository;
            var code = string.IsNullOrWhiteSpace(ActivityCode)
                ? await repo.GetNextCodeAsync(currentScheduleId)
                : ActivityCode.Trim();
            var id = ActivityId.New();
            var wbsRepo = host.Services.WbsPlanningRepository;
            var wbsNodes = await wbsRepo.GetByScheduleAsync(currentScheduleId);
            var wbsNode = wbsNodes.FirstOrDefault();
            var wbsNodeId = wbsNode?.Id ?? WbsPlanningNodeId.New();
            var activity = new Activity(id, currentScheduleId, wbsNodeId, code, ActivityName.Trim(), ActivityType,
                new PlanningDuration(DurationValue, DurationUnit), host.Session.CurrentUserName)
            {
                CalendarId = SelectedCalendarId,
                ResponsibleId = string.IsNullOrWhiteSpace(ResponsibleId) ? null : ResponsibleId,
                Status = Status
            };
            await repo.SaveAsync(activity);
            IsEditing = false;
            await LoadAsync();
            StatusMessage = "Actividad guardada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    public async Task DeleteActivityAsync()
    {
        var vm = SelectedActivity;
        if (vm == null) return;
        try
        {
            StatusMessage = "Eliminando actividad...";
            var repo = host.Services.ActivityRepository;
            await repo.DeleteAsync(vm.Id);
            SelectedActivity = null;
            await LoadAsync();
            StatusMessage = "Actividad eliminada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar: {ex.Message}";
        }
    }

    private void EditDependencies()
    {
        var act = SelectedActivity;
        if (act == null || currentScheduleId == null) return;
        var vm = new DependencyEditorViewModel(host, act.Id, currentScheduleId);
        var dialog = new DependencyEditorDialog(vm);
        dialog.Owner = App.Current?.MainWindow;
        dialog.ShowDialog();
    }

    private void Reset()
    {
        activities.Clear();
        calendars.Clear();
        ActivityCode = "";
        ActivityName = "";
        SearchText = "";
        currentScheduleId = null;
        StatusMessage = "";
        IsEditing = false;
    }
}
