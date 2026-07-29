using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class ProgressCoverageExtraTests
{
    [Theory]
    [InlineData(DelayReasonCode.Material)]
    [InlineData(DelayReasonCode.Labor)]
    [InlineData(DelayReasonCode.Equipment)]
    [InlineData(DelayReasonCode.Design)]
    [InlineData(DelayReasonCode.Client)]
    [InlineData(DelayReasonCode.Subcontract)]
    [InlineData(DelayReasonCode.Permit)]
    [InlineData(DelayReasonCode.Weather)]
    [InlineData(DelayReasonCode.Access)]
    [InlineData(DelayReasonCode.Quality)]
    [InlineData(DelayReasonCode.Safety)]
    [InlineData(DelayReasonCode.Reschedule)]
    [InlineData(DelayReasonCode.Other)]
    public void Delay_reason_codes_are_defined(DelayReasonCode code)
    {
        Assert.True(Enum.IsDefined(code));
    }

    [Theory]
    [InlineData(ProgressMethod.Manual)]
    [InlineData(ProgressMethod.ByDuration)]
    [InlineData(ProgressMethod.ByQuantity)]
    [InlineData(ProgressMethod.ByPhysicalUnits)]
    [InlineData(ProgressMethod.ByWeightedMilestones)]
    [InlineData(ProgressMethod.Other)]
    public void Progress_methods_are_defined(ProgressMethod method)
    {
        Assert.True(Enum.IsDefined(method));
    }

    [Theory]
    [InlineData(ProgressTrackingStatus.NotStarted)]
    [InlineData(ProgressTrackingStatus.Started)]
    [InlineData(ProgressTrackingStatus.InProgress)]
    [InlineData(ProgressTrackingStatus.Suspended)]
    [InlineData(ProgressTrackingStatus.Completed)]
    [InlineData(ProgressTrackingStatus.Cancelled)]
    [InlineData(ProgressTrackingStatus.Blocked)]
    [InlineData(ProgressTrackingStatus.Delayed)]
    [InlineData(ProgressTrackingStatus.Ahead)]
    [InlineData(ProgressTrackingStatus.OnTrack)]
    [InlineData(ProgressTrackingStatus.NotApplicable)]
    public void Tracking_statuses_are_defined(ProgressTrackingStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    [Theory]
    [InlineData(EvidenceReferenceType.Document)]
    [InlineData(EvidenceReferenceType.Photo)]
    [InlineData(EvidenceReferenceType.Minutes)]
    [InlineData(EvidenceReferenceType.Report)]
    [InlineData(EvidenceReferenceType.Email)]
    [InlineData(EvidenceReferenceType.Drawing)]
    [InlineData(EvidenceReferenceType.ExternalRecord)]
    public void Evidence_types_are_defined(EvidenceReferenceType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Theory]
    [InlineData(GlobalProgressWeighting.ByPlannedCost)]
    [InlineData(GlobalProgressWeighting.ByDuration)]
    [InlineData(GlobalProgressWeighting.ByQuantity)]
    [InlineData(GlobalProgressWeighting.ByManualWeight)]
    [InlineData(GlobalProgressWeighting.ByMilestones)]
    public void Weighting_policies_are_defined(GlobalProgressWeighting w)
    {
        Assert.True(Enum.IsDefined(w));
    }

    [Fact]
    public void Hours_negative_validation()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "H", "H",
            ActivityType.Task, new PlanningDuration(1, DurationUnit.WorkDays), "t");
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = DateTime.Today, ActualStart = DateTime.Today, ActualHours = -1,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "HOURS_NEG");
    }

    [Fact]
    public void Financial_range_validation()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "F", "F",
            ActivityType.Task, new PlanningDuration(1, DurationUnit.WorkDays), "t");
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = DateTime.Today, ActualStart = DateTime.Today, FinancialProgress = 101,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "FIN_RANGE");
    }

    [Fact]
    public void Summary_activity_type_summary_excluded()
    {
        var sid = ScheduleId.New();
        var summary = new Activity(ActivityId.New(), sid, WbsPlanningNodeId.New(), "S", "Sum",
            ActivityType.Summary, new PlanningDuration(1, DurationUnit.WorkDays), "t");
        var task = new Activity(ActivityId.New(), sid, WbsPlanningNodeId.New(), "T", "T",
            ActivityType.Task, new PlanningDuration(1, DurationUnit.WorkDays), "t");
        var result = ScheduleProgressSummaryCalculator.Calculate(sid, DateTime.Today,
            new[] { summary, task }, new Dictionary<string, ActivityProgressUpdate>(), ProgressPolicy.Default);
        Assert.Equal(1, result.TotalActivities);
    }

    [Fact]
    public void Estimate_at_completion_sums()
    {
        var consumed = new PlanningDuration(3, DurationUnit.WorkDays);
        var rem = new PlanningDuration(2, DurationUnit.WorkDays);
        var eac = RemainingDurationCalculator.EstimateAtCompletion(consumed, rem);
        Assert.Equal(5, eac.ToWorkDays().Value);
    }
}
