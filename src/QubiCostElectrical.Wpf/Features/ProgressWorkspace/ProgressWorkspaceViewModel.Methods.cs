using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ProgressWorkspace;

public sealed partial class ProgressWorkspaceViewModel
{
    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            if (_host.Services.ProgressTracking is null || _host.Services.ScheduleRepository is null)
            {
                StatusMessage = "Servicios de seguimiento no inicializados. Abra un proyecto.";
                return;
            }

            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath ?? string.Empty);
            var schedule = schedules.FirstOrDefault();
            if (schedule is null)
            {
                StatusMessage = "No hay cronogramas en el proyecto.";
                return;
            }

            _scheduleId = schedule.Id;
            var status = await _host.Services.ProgressTracking.GetStatusDateAsync(schedule.Id);
            if (status is not null)
            {
                StatusDate = status.StatusDate;
                StatusComment = status.Comment;
            }

            var activities = await _host.Services.ActivityRepository.GetByScheduleAsync(schedule.Id);
            var progress = await _host.Services.ProgressTracking.ListProgressAsync(schedule.Id);
            var progressMap = progress.ToDictionary(p => p.ActivityId.Value, p => p);
            IReadOnlyList<OutOfSequenceWarning> oos = Array.Empty<OutOfSequenceWarning>();
            try { oos = await _host.Services.ProgressTracking.DetectOutOfSequenceAsync(schedule.Id); }
            catch { /* sin fecha de estado aún */ }
            var oosIds = oos.Select(w => w.ActivityId.Value).ToHashSet();

            _rows.Clear();
            foreach (var act in activities.Where(a => !a.IsDeleted && a.Type != ActivityType.Summary))
            {
                progressMap.TryGetValue(act.Id.Value, out var prog);
                var (fs, ff) = ProgressForecastEngine.ForecastActivity(act, prog, StatusDate);
                _rows.Add(new ProgressRowVm
                {
                    ActivityId = act.Id.Value,
                    Code = act.Code,
                    Name = act.Name,
                    Status = prog is null ? ProgressTrackingStatus.NotStarted.ToString() : prog.ProgressStatus.ToString(),
                    PercentComplete = prog?.PercentComplete ?? 0m,
                    PhysicalProgress = prog?.PhysicalProgress ?? 0m,
                    FinancialProgress = prog?.FinancialProgress ?? 0m,
                    PlannedStart = act.PlannedStart,
                    PlannedFinish = act.PlannedFinish,
                    ActualStart = prog?.ActualStart,
                    ActualFinish = prog?.ActualFinish,
                    ForecastStart = prog?.ForecastStart ?? fs,
                    ForecastFinish = prog?.ForecastFinish ?? ff,
                    BaselineStart = act.PlannedStart,
                    BaselineFinish = act.PlannedFinish,
                    IsCritical = act.IsCritical,
                    IsSuspended = prog?.ProgressStatus == ProgressTrackingStatus.Suspended,
                    HasOutOfSequence = oosIds.Contains(act.Id.Value)
                });
            }

            try
            {
                var summary = await _host.Services.ProgressTracking.GetSummaryAsync(schedule.Id);
                SummaryText =
                    $"Total {summary.TotalActivities} · No iniciadas {summary.NotStarted} · En progreso {summary.InProgress} · " +
                    $"Completadas {summary.Completed} · Atrasadas {summary.Delayed} · Adelantadas {summary.Ahead} · " +
                    $"Suspendidas {summary.Suspended} · Críticas {summary.Critical} · " +
                    $"Avance físico {summary.GlobalPhysicalProgress:0.##}% · Financiero {summary.GlobalFinancialProgress:0.##}% · " +
                    $"Costo real {summary.AccumulatedActualCost:N0} · Forecast fin {summary.ForecastProjectFinish:dd/MM/yyyy}";
            }
            catch
            {
                SummaryText = "Establezca fecha de estado para ver el resumen.";
            }

            ApplyFilters();
            StatusMessage = $"Cargadas {_rows.Count} actividades.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            _isBusy = false;
        }
    }

    private async Task SetStatusDateAsync()
    {
        if (_isBusy || _scheduleId is null) return;
        _isBusy = true;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProgressTracking.SetStatusDateAsync(
                _scheduleId, StatusDate, TimeZoneInfo.Local.Id, StatusComment, actor);
            StatusMessage = $"Fecha de estado: {StatusDate:dd/MM/yyyy}";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private async Task SaveSelectedProgressAsync()
    {
        if (_isBusy || _scheduleId is null || SelectedRow is null) return;
        _isBusy = true;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            DelayReasonCode? delay = null;
            if (Enum.TryParse<DelayReasonCode>(EditDelayReason, true, out var parsed))
                delay = parsed;

            var update = new ActivityProgressUpdate
            {
                ProgressUpdateId = Guid.NewGuid().ToString("N"),
                ScheduleId = _scheduleId,
                ActivityId = ActivityId.Parse(SelectedRow.ActivityId),
                StatusDate = StatusDate,
                ActualStart = EditActualStart,
                ActualFinish = EditActualFinish,
                PercentComplete = EditPercent,
                PhysicalProgress = EditPercent,
                ProgressMethod = ProgressMethod.Manual,
                ExecutedQuantity = EditExecutedQuantity > 0 ? EditExecutedQuantity : null,
                ActualHours = EditActualHours > 0 ? EditActualHours : null,
                ActualCost = EditActualCost > 0 ? EditActualCost : null,
                CommittedCost = EditCommittedCost > 0 ? EditCommittedCost : null,
                RemainingDuration = EditRemainingDays > 0
                    ? new PlanningDuration((double)EditRemainingDays, DurationUnit.WorkDays)
                    : null,
                DelayReason = delay,
                Comment = EditComment,
                EvidenceReference = EditEvidence,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = actor,
                ModifiedUtc = DateTime.UtcNow,
                ModifiedBy = actor,
                RowVersion = Guid.NewGuid().ToString("N")
            };
            await _host.Services.ProgressTracking.RegisterProgressAsync(update, actor);
            StatusMessage = "Avance guardado.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private async Task CompleteAsync()
    {
        if (_scheduleId is null || SelectedRow is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProgressTracking.CompleteActivityAsync(
                _scheduleId, ActivityId.Parse(SelectedRow.ActivityId),
                EditActualFinish ?? StatusDate, actor);
            StatusMessage = "Actividad completada.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task SuspendAsync()
    {
        if (_scheduleId is null || SelectedRow is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProgressTracking.SuspendActivityAsync(
                _scheduleId, ActivityId.Parse(SelectedRow.ActivityId), EditComment, actor);
            StatusMessage = "Actividad suspendida.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ResumeAsync()
    {
        if (_scheduleId is null || SelectedRow is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProgressTracking.ResumeActivityAsync(
                _scheduleId, ActivityId.Parse(SelectedRow.ActivityId), actor);
            StatusMessage = "Actividad reanudada.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task RescheduleAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var result = await _host.Services.ProgressTracking.RescheduleFromStatusDateAsync(_scheduleId, actor);
            StatusMessage = $"Reprogramación explícita: {result.ChangedActivityIds.Count} actividades afectadas.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CreateSnapshotAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var snap = await _host.Services.ProgressTracking.CreateSnapshotAsync(_scheduleId, StatusComment, actor);
            StatusMessage = $"Snapshot v{snap.Version} creado. SHA-256: {snap.SHA256[..12]}…";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CompareSnapshotsAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var snaps = await _host.Services.ProgressSnapshotRepository.GetByScheduleAsync(_scheduleId);
            if (snaps.Count < 2)
            {
                StatusMessage = "Se requieren al menos 2 snapshots para comparar.";
                return;
            }
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var a = snaps[^2];
            var b = snaps[^1];
            var cmp = await _host.Services.ProgressTracking.CompareSnapshotsAsync(a.SnapshotId, b.SnapshotId, actor);
            StatusMessage =
                $"Comparación v{a.Version}→v{b.Version}: iniciadas {cmp.NewlyStartedActivityIds.Count}, " +
                $"completadas {cmp.NewlyCompletedActivityIds.Count}, atrasadas {cmp.NewlyDelayedActivityIds.Count}, " +
                $"camino crítico cambiado {cmp.CriticalPathChangedActivityIds.Count}.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task RecalculateAsync()
    {
        if (_scheduleId is null || SelectedRow is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProgressTracking.RecalculateRemainingAsync(
                _scheduleId, ActivityId.Parse(SelectedRow.ActivityId), actor);
            StatusMessage = "Duración restante recalculada.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ExportAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var path = ProgressExportHelper.ExportSummaryCsv(_rows.ToList(), SummaryText, StatusDate);
            StatusMessage = $"Exportado: {path}";
            await Task.CompletedTask;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void ApplyFilters()
    {
        var any = FilterNotStarted || FilterInProgress || FilterCompleted || FilterDelayed || FilterAhead ||
                  FilterCritical || FilterBlocked || FilterSuspended || FilterOos || FilterWithCost || FilterWithoutUpdate;

        _filteredRows.Clear();
        foreach (var row in _rows)
        {
            if (!any)
            {
                _filteredRows.Add(row);
                continue;
            }

            var match = false;
            if (FilterNotStarted && row.Status == nameof(ProgressTrackingStatus.NotStarted)) match = true;
            if (FilterInProgress && (row.Status is nameof(ProgressTrackingStatus.InProgress) or nameof(ProgressTrackingStatus.Started) or nameof(ProgressTrackingStatus.OnTrack))) match = true;
            if (FilterCompleted && row.Status == nameof(ProgressTrackingStatus.Completed)) match = true;
            if (FilterDelayed && row.Status == nameof(ProgressTrackingStatus.Delayed)) match = true;
            if (FilterAhead && row.Status == nameof(ProgressTrackingStatus.Ahead)) match = true;
            if (FilterCritical && row.IsCritical) match = true;
            if (FilterBlocked && row.Status == nameof(ProgressTrackingStatus.Blocked)) match = true;
            if (FilterSuspended && row.IsSuspended) match = true;
            if (FilterOos && row.HasOutOfSequence) match = true;
            if (FilterWithCost && row.FinancialProgress > 0) match = true;
            if (FilterWithoutUpdate && row.PercentComplete == 0 && row.ActualStart is null) match = true;
            if (match) _filteredRows.Add(row);
        }
    }
}
