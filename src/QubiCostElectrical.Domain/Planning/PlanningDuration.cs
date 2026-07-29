using System;

namespace QubiCostElectrical;

public sealed record PlanningDuration
{
    public double Value { get; init; }
    public DurationUnit Unit { get; init; }

    public PlanningDuration(double value, DurationUnit unit)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Duration cannot be negative");
        if (unit == DurationUnit.Minutes && value > 525600) throw new ArgumentOutOfRangeException(nameof(value), "Duration exceeds maximum (525600 minutes)");
        if (unit == DurationUnit.Hours && value > 8760) throw new ArgumentOutOfRangeException(nameof(value), "Duration exceeds maximum (8760 hours)");
        if (unit == DurationUnit.WorkDays && value > 3650) throw new ArgumentOutOfRangeException(nameof(value), "Duration exceeds maximum (3650 work days)");
        if (unit == DurationUnit.WorkWeeks && value > 520) throw new ArgumentOutOfRangeException(nameof(value), "Duration exceeds maximum (520 work weeks)");
        Value = value;
        Unit = unit;
    }

    public static PlanningDuration Zero(ActivityType type)
    {
        if (type == ActivityType.Milestone) return new PlanningDuration(0, DurationUnit.Hours);
        throw new InvalidOperationException("Non-milestone activities must have positive duration");
    }

    public PlanningDuration ToMinutes(double workHoursPerDay = 8) => Unit switch
    {
        DurationUnit.Minutes => this,
        DurationUnit.Hours => new PlanningDuration(Value * 60, DurationUnit.Minutes),
        DurationUnit.WorkDays => new PlanningDuration(Value * workHoursPerDay * 60, DurationUnit.Minutes),
        DurationUnit.WorkWeeks => new PlanningDuration(Value * workHoursPerDay * 5 * 60, DurationUnit.Minutes),
        _ => this
    };

    public PlanningDuration ToHours(double workHoursPerDay = 8) => Unit switch
    {
        DurationUnit.Minutes => new PlanningDuration(Value / 60, DurationUnit.Hours),
        DurationUnit.Hours => this,
        DurationUnit.WorkDays => new PlanningDuration(Value * workHoursPerDay, DurationUnit.Hours),
        DurationUnit.WorkWeeks => new PlanningDuration(Value * workHoursPerDay * 5, DurationUnit.Hours),
        _ => this
    };

    public PlanningDuration ToWorkDays(double workHoursPerDay = 8) => Unit switch
    {
        DurationUnit.Minutes => new PlanningDuration(Value / (workHoursPerDay * 60), DurationUnit.WorkDays),
        DurationUnit.Hours => new PlanningDuration(Value / workHoursPerDay, DurationUnit.WorkDays),
        DurationUnit.WorkDays => this,
        DurationUnit.WorkWeeks => new PlanningDuration(Value * 5, DurationUnit.WorkDays),
        _ => this
    };

    public static PlanningDuration operator +(PlanningDuration a, PlanningDuration b)
    {
        var minutesA = a.ToMinutes().Value;
        var minutesB = b.ToMinutes().Value;
        return new PlanningDuration(minutesA + minutesB, DurationUnit.Minutes);
    }

    public static PlanningDuration operator -(PlanningDuration a, PlanningDuration b)
    {
        var minutesA = a.ToMinutes().Value;
        var minutesB = b.ToMinutes().Value;
        return new PlanningDuration(minutesA - minutesB, DurationUnit.Minutes);
    }
}
