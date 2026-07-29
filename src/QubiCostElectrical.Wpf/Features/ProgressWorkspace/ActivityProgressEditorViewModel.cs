using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ProgressWorkspace;

/// <summary>
/// Editor delgado de avance: solo captura inputs; la validación/cálculo ocurre en Application/Domain.
/// </summary>
public sealed class ActivityProgressEditorViewModel : INotifyPropertyChanged
{
    private DateTime? _actualStart;
    private DateTime? _actualFinish;
    private decimal _percentComplete;
    private decimal _executedQuantity;
    private decimal _actualHours;
    private decimal _actualCost;
    private decimal _committedCost;
    private decimal _remainingDays;
    private string _comment = string.Empty;
    private string _evidence = string.Empty;
    private string _delayReason = nameof(DelayReasonCode.Other);
    private string _validationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime? ActualStart { get => _actualStart; set => Set(ref _actualStart, value); }
    public DateTime? ActualFinish { get => _actualFinish; set => Set(ref _actualFinish, value); }
    public decimal PercentComplete { get => _percentComplete; set => Set(ref _percentComplete, value); }
    public decimal ExecutedQuantity { get => _executedQuantity; set => Set(ref _executedQuantity, value); }
    public decimal ActualHours { get => _actualHours; set => Set(ref _actualHours, value); }
    public decimal ActualCost { get => _actualCost; set => Set(ref _actualCost, value); }
    public decimal CommittedCost { get => _committedCost; set => Set(ref _committedCost, value); }
    public decimal RemainingDays { get => _remainingDays; set => Set(ref _remainingDays, value); }
    public string Comment { get => _comment; set => Set(ref _comment, value); }
    public string Evidence { get => _evidence; set => Set(ref _evidence, value); }
    public string DelayReason { get => _delayReason; set => Set(ref _delayReason, value); }
    public string ValidationMessage { get => _validationMessage; private set => Set(ref _validationMessage, value); }

    public void ShowImmediateValidationHint()
    {
        if (PercentComplete < 0m || PercentComplete > 100m)
            ValidationMessage = "El porcentaje debe estar entre 0 y 100.";
        else if (ActualFinish.HasValue && !ActualStart.HasValue)
            ValidationMessage = "No puede existir término real sin inicio real.";
        else if (ActualStart.HasValue && ActualFinish.HasValue && ActualFinish < ActualStart)
            ValidationMessage = "El término real no puede ser anterior al inicio real.";
        else
            ValidationMessage = string.Empty;
    }

    public ActivityProgressUpdate ToUpdate(ScheduleId scheduleId, ActivityId activityId, DateTime statusDate, string actor) =>
        new()
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            ActivityId = activityId,
            StatusDate = statusDate,
            ActualStart = ActualStart,
            ActualFinish = ActualFinish,
            PercentComplete = PercentComplete,
            PhysicalProgress = PercentComplete,
            ProgressMethod = ProgressMethod.Manual,
            ExecutedQuantity = ExecutedQuantity > 0 ? ExecutedQuantity : null,
            ActualHours = ActualHours > 0 ? ActualHours : null,
            ActualCost = ActualCost > 0 ? ActualCost : null,
            CommittedCost = CommittedCost > 0 ? CommittedCost : null,
            RemainingDuration = RemainingDays > 0 ? new PlanningDuration((double)RemainingDays, DurationUnit.WorkDays) : null,
            DelayReason = Enum.TryParse<DelayReasonCode>(DelayReason, true, out var d) ? d : null,
            Comment = Comment,
            EvidenceReference = Evidence,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(PercentComplete) or nameof(ActualStart) or nameof(ActualFinish))
            ShowImmediateValidationHint();
    }
}
