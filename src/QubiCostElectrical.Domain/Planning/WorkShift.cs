using System;

namespace QubiCostElectrical;

public sealed record WorkShift
{
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }

    public WorkShift(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime >= endTime && endTime != TimeOnly.MinValue)
            throw new ArgumentException("Shift end must be after start");
        if (startTime == endTime)
            throw new ArgumentException("Shift cannot have zero duration");
        StartTime = startTime;
        EndTime = endTime;
    }

    public double TotalHours => (EndTime - StartTime).TotalHours;
}
