using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class PlanningDurationTests
{
    [Fact]
    public void Create_InMinutes_SetsCorrectValue()
    {
        var d = new PlanningDuration(90, DurationUnit.Minutes);
        Assert.Equal(90, d.Value);
        Assert.Equal(DurationUnit.Minutes, d.Unit);
    }

    [Fact]
    public void Convert_HoursToMinutes()
    {
        var d = new PlanningDuration(2, DurationUnit.Hours);
        var converted = d.ToMinutes();
        Assert.Equal(120, converted.Value);
        Assert.Equal(DurationUnit.Minutes, converted.Unit);
    }

    [Fact]
    public void Convert_WorkDaysToHours()
    {
        var d = new PlanningDuration(5, DurationUnit.WorkDays);
        var converted = d.ToHours();
        Assert.Equal(40, converted.Value);
        Assert.Equal(DurationUnit.Hours, converted.Unit);
    }

    [Fact]
    public void Add_Durations_ReturnsSumInMinutes()
    {
        var a = new PlanningDuration(2, DurationUnit.Hours);
        var b = new PlanningDuration(30, DurationUnit.Minutes);
        var sum = a + b;
        Assert.Equal(150, sum.Value);
        Assert.Equal(DurationUnit.Minutes, sum.Unit);
    }

    [Fact]
    public void Subtract_Durations_ReturnsDifferenceInMinutes()
    {
        var a = new PlanningDuration(3, DurationUnit.Hours);
        var b = new PlanningDuration(1, DurationUnit.Hours);
        var diff = a - b;
        Assert.Equal(120, diff.Value);
        Assert.Equal(DurationUnit.Minutes, diff.Unit);
    }
}
