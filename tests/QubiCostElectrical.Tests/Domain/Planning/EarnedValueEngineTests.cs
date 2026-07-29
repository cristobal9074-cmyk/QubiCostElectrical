using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class EarnedValueEngineTests
{
    private static EarnedValueInput BaseInput(
        decimal bac = 1000m, decimal pv = 400m, decimal ev = 350m, decimal ac = 450m,
        bool hasBaseline = true, bool hasBudget = true) =>
        new()
        {
            ScheduleId = ScheduleId.New(),
            StatusDate = new DateTime(2026, 3, 1),
            Bac = bac,
            Pv = pv,
            Ev = ev,
            Ac = ac,
            HasBaseline = hasBaseline,
            HasStatusDate = true,
            HasBudget = hasBudget,
            HasProgress = true,
            EacMethod = EacMethod.BacOverCpi
        };

    [Fact]
    public void Calculates_core_indicators()
    {
        var r = EarnedValueEngine.Calculate(BaseInput());
        Assert.Equal(-50m, r.Indicators.Sv); // 350-400
        Assert.Equal(-100m, r.Indicators.Cv); // 350-450
        Assert.Equal(0.875m, r.Indicators.Spi);
        Assert.True(r.Indicators.Cpi < 1m);
        Assert.True(r.Indicators.Eac > r.Indicators.Bac);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Vac_is_bac_minus_eac()
    {
        var r = EarnedValueEngine.Calculate(BaseInput());
        Assert.Equal(r.Indicators.Bac - r.Indicators.Eac, r.Indicators.Vac);
    }

    [Fact]
    public void Etc_is_eac_minus_ac()
    {
        var r = EarnedValueEngine.Calculate(BaseInput());
        Assert.Equal(r.Indicators.Eac - r.Indicators.Ac, r.Indicators.Etc);
    }

    [Fact]
    public void Spi_null_when_pv_zero()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(pv: 0m, ev: 100m, ac: 100m));
        Assert.Null(r.Indicators.Spi);
        Assert.Contains(r.Issues, i => i.Code is "SPI_UNDEF" or "SPI_DIV0");
    }

    [Fact]
    public void Cpi_null_when_ac_zero()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(ac: 0m, ev: 100m, pv: 100m));
        Assert.Null(r.Indicators.Cpi);
    }

    [Fact]
    public void Rejects_negative_ac()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(ac: -10m));
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Code == "AC_NEG" && i.IsError);
    }

    [Fact]
    public void Rejects_no_baseline()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(hasBaseline: false));
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Code == "NO_BASELINE");
    }

    [Fact]
    public void Rejects_bac_zero()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(bac: 0m, hasBudget: false));
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Code is "BAC_ZERO" or "NO_BUDGET");
    }

    [Fact]
    public void Eac_method_ac_plus_remaining()
    {
        var input = BaseInput();
        input = new EarnedValueInput
        {
            ScheduleId = input.ScheduleId, StatusDate = input.StatusDate,
            Bac = 1000m, Pv = 400m, Ev = 400m, Ac = 400m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true,
            EacMethod = EacMethod.AcPlusRemainingBudget
        };
        var r = EarnedValueEngine.Calculate(input);
        Assert.Equal(1000m, r.Indicators.Eac);
    }

    [Fact]
    public void Tcpi_computed_when_funds_remain()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(bac: 1000m, ev: 400m, ac: 500m, pv: 500m));
        Assert.NotNull(r.Indicators.Tcpi);
        Assert.Equal(1.2m, r.Indicators.Tcpi); // (1000-400)/(1000-500)=600/500=1.2
    }

    [Fact]
    public void Percentages_derived_from_bac()
    {
        var r = EarnedValueEngine.Calculate(BaseInput(bac: 1000m, pv: 250m, ev: 200m, ac: 300m));
        Assert.Equal(25m, r.Indicators.PercentPlanned);
        Assert.Equal(20m, r.Indicators.PercentEarned);
        Assert.Equal(30m, r.Indicators.PercentActual);
    }

    [Fact]
    public void Forecast_finish_variance_days()
    {
        var input = BaseInput();
        input = new EarnedValueInput
        {
            ScheduleId = input.ScheduleId, StatusDate = input.StatusDate,
            Bac = 1000m, Pv = 500m, Ev = 500m, Ac = 500m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true,
            BaselineFinish = new DateTime(2026, 6, 1),
            ForecastFinish = new DateTime(2026, 6, 11)
        };
        var r = EarnedValueEngine.Calculate(input);
        Assert.Equal(10m, r.Forecast.ForecastFinishVarianceDays);
        Assert.Equal(r.Indicators.Eac, r.Forecast.ForecastCost);
    }

    [Fact]
    public void Deterministic_same_input_same_output()
    {
        var input = BaseInput();
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var a = EarnedValueEngine.Calculate(input, utc);
        var b = EarnedValueEngine.Calculate(input, utc);
        Assert.Equal(a.Indicators.Spi, b.Indicators.Spi);
        Assert.Equal(a.Indicators.Cpi, b.Indicators.Cpi);
        Assert.Equal(a.Indicators.Eac, b.Indicators.Eac);
        Assert.Equal(a.Explanation, b.Explanation);
    }

    [Theory]
    [InlineData(100, 0, 0)]
    [InlineData(100, 50, 50)]
    [InlineData(100, 100, 100)]
    public void Activity_ev_percent_complete(decimal budget, decimal pct, decimal expected)
    {
        Assert.Equal(expected, EarnedValueEngine.ComputeActivityEv(budget, pct, EarnedValueMethod.PercentComplete));
    }

    [Fact]
    public void Activity_ev_0_100()
    {
        Assert.Equal(0m, EarnedValueEngine.ComputeActivityEv(100m, 50m, EarnedValueMethod.FixedFormula0_100));
        Assert.Equal(100m, EarnedValueEngine.ComputeActivityEv(100m, 100m, EarnedValueMethod.FixedFormula0_100));
    }

    [Fact]
    public void Activity_ev_50_50()
    {
        Assert.Equal(50m, EarnedValueEngine.ComputeActivityEv(100m, 10m, EarnedValueMethod.FixedFormula50_50));
        Assert.Equal(100m, EarnedValueEngine.ComputeActivityEv(100m, 100m, EarnedValueMethod.FixedFormula50_50));
    }

    [Fact]
    public void Pv_at_status_linear()
    {
        var pv = EarnedValueEngine.ComputePvAtStatus(100m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 11), new DateTime(2026, 1, 6));
        Assert.Equal(50m, pv);
    }

    [Fact]
    public void Pv_before_start_is_zero()
    {
        var pv = EarnedValueEngine.ComputePvAtStatus(100m, new DateTime(2026, 2, 1), new DateTime(2026, 2, 10), new DateTime(2026, 1, 15));
        Assert.Equal(0m, pv);
    }

    [Fact]
    public void Pv_after_finish_is_full()
    {
        var pv = EarnedValueEngine.ComputePvAtStatus(100m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 10), new DateTime(2026, 2, 1));
        Assert.Equal(100m, pv);
    }

    [Theory]
    [InlineData(0.85, EvmTrafficLight.Red)]
    [InlineData(0.95, EvmTrafficLight.Amber)]
    [InlineData(1.0, EvmTrafficLight.Green)]
    [InlineData(1.1, EvmTrafficLight.Green)]
    public void Spi_interpretation_lights(decimal spi, EvmTrafficLight expected)
    {
        Assert.Equal(expected, EarnedValueInterpreter.InterpretSpi(spi).Light);
    }

    [Theory]
    [InlineData(0.85, "Sobre costo")]
    [InlineData(1.05, "Buen desempeño")]
    public void Cpi_interpretation_messages(decimal cpi, string contains)
    {
        Assert.Contains(contains, EarnedValueInterpreter.InterpretCpi(cpi).Message);
    }

    [Fact]
    public void Snapshot_hash_stable()
    {
        var result = EarnedValueEngine.Calculate(BaseInput(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var snap = new EarnedValueSnapshot
        {
            SnapshotId = "s1", ScheduleId = result.ScheduleId, Version = 0,
            StatusDate = result.StatusDate, CreatedUtc = DateTime.UtcNow, CreatedBy = "t",
            Result = result, SHA256 = string.Empty
        };
        var h1 = EarnedValueSnapshotHasher.ComputeHash(snap);
        var h2 = EarnedValueSnapshotHasher.ComputeHash(snap);
        Assert.Equal(h1, h2);
        var sealedSnap = new EarnedValueSnapshot
        {
            SnapshotId = snap.SnapshotId, ScheduleId = snap.ScheduleId, Version = 0,
            StatusDate = snap.StatusDate, CreatedUtc = snap.CreatedUtc, CreatedBy = "t",
            Result = result, SHA256 = h1
        };
        Assert.True(EarnedValueSnapshotHasher.Verify(sealedSnap));
    }

    [Fact]
    public void Comparison_detects_cost_worsening()
    {
        var left = EarnedValueEngine.Calculate(BaseInput(ev: 400m, ac: 400m, pv: 400m));
        var right = EarnedValueEngine.Calculate(BaseInput(ev: 400m, ac: 500m, pv: 400m));
        var cmp = EarnedValueComparisonEngine.Compare("A", "B", left, right);
        Assert.True(cmp.CostVarianceDelta < 0m);
        Assert.Contains(cmp.Findings, f => f.Contains("costo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Input_builder_aggregates_from_activities()
    {
        var sid = ScheduleId.New();
        var cal = CalendarId.New();
        var schedule = new Schedule(sid, "S", "UTC", cal, "t");
        var act = new Activity(ActivityId.New(), sid, WbsPlanningNodeId.New(), "A1", "A",
            ActivityType.Task, new PlanningDuration(10, DurationUnit.WorkDays), "t")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 21)
        };
        var prog = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = sid, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 11), PercentComplete = 50m, PhysicalProgress = 50m,
            ActualStart = new DateTime(2026, 1, 1), ActualCost = 200m,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var input = EarnedValueInputBuilder.Build(schedule, new DateTime(2026, 1, 11),
            new[] { act }, new Dictionary<string, ActivityProgressUpdate> { [act.Id.Value] = prog },
            projectBac: 1000m, hasOfficialBaseline: true);
        Assert.Equal(1000m, input.Bac);
        Assert.True(input.Ev > 0m);
        Assert.Equal(200m, input.Ac);
        Assert.True(input.HasBaseline);
    }
}
