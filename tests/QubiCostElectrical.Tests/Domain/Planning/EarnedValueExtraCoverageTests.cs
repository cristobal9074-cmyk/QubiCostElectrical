using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class EarnedValueExtraCoverageTests
{
    [Theory]
    [InlineData(1000, 100, 200, 300)]
    [InlineData(1000, 400, 400, 400)]
    [InlineData(1000, 800, 700, 900)]
    [InlineData(500, 100, 150, 120)]
    [InlineData(2000, 1500, 1400, 1600)]
    [InlineData(100, 10, 10, 20)]
    [InlineData(750, 250, 300, 280)]
    [InlineData(1200, 600, 550, 650)]
    [InlineData(3000, 1000, 900, 1100)]
    [InlineData(50, 25, 20, 30)]
    public void Sv_equals_ev_minus_pv(decimal bac, decimal pv, decimal ev, decimal ac)
    {
        var r = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = bac, Pv = pv, Ev = ev, Ac = ac,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        Assert.Equal(ev - pv, r.Indicators.Sv);
        Assert.Equal(ev - ac, r.Indicators.Cv);
    }

    [Theory]
    [InlineData(EacMethod.AcPlusRemainingBudget)]
    [InlineData(EacMethod.BacOverCpi)]
    [InlineData(EacMethod.AcPlusRemainingOverCpiSpi)]
    public void Eac_methods_produce_finite(EacMethod method)
    {
        var r = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 450m, Ac = 500m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true,
            EacMethod = method
        });
        Assert.True(r.Indicators.Eac > 0m);
        Assert.Equal(method, r.Indicators.EacMethodUsed);
    }

    [Theory]
    [InlineData(1, 10, 1)]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 10)]
    [InlineData(15, 10, 10)]
    [InlineData(3, 12, 2.5)]
    [InlineData(7, 14, 5)]
    [InlineData(2, 8, 2.5)]
    [InlineData(9, 9, 10)] // status == finish → PV completo (budget 10)
    [InlineData(4, 20, 2)]
    [InlineData(6, 6, 10)] // status == finish → PV completo
    public void Pv_linear_interpolation(int elapsedDays, int spanDays, double expectedPv)
    {
        // budget 10, start day 0, finish spanDays
        var start = new DateTime(2026, 1, 1);
        var finish = start.AddDays(spanDays);
        var status = start.AddDays(elapsedDays);
        var pv = EarnedValueEngine.ComputePvAtStatus(10m, start, finish, status);
        Assert.Equal((decimal)expectedPv, pv);
    }

    [Fact]
    public void No_status_date_flag_errors()
    {
        var issues = EarnedValueValidation.Validate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 100m, Pv = 10m, Ev = 10m, Ac = 10m,
            HasBaseline = true, HasStatusDate = false, HasBudget = true, HasProgress = true
        });
        Assert.Contains(issues, i => i.Code == "NO_STATUS_DATE");
    }

    [Fact]
    public void Interpret_full_set_has_spi_and_cpi()
    {
        var r = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 400m, Ac = 450m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        Assert.Contains(r.Interpretations, i => i.Indicator == "SPI");
        Assert.Contains(r.Interpretations, i => i.Indicator == "CPI");
        Assert.Contains(r.Interpretations, i => i.Indicator == "TCPI");
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.2, 0.8)]
    [InlineData(0.95, 1.0)]
    public void Tcpi_lights_are_defined(decimal tcpi, decimal cpi)
    {
        var interp = EarnedValueInterpreter.InterpretTcpi(tcpi, cpi);
        Assert.True(interp.Light != EvmTrafficLight.Unknown);
        Assert.False(string.IsNullOrWhiteSpace(interp.Message));
    }

    [Fact]
    public void Comparison_schedule_findings()
    {
        var a = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 500m, Ac = 500m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var b = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = a.ScheduleId, StatusDate = DateTime.Today.AddDays(7),
            Bac = 1000m, Pv = 600m, Ev = 500m, Ac = 500m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var cmp = EarnedValueComparisonEngine.Compare("t1", "t2", a, b);
        Assert.True(cmp.ScheduleVarianceDelta < 0m);
    }

    [Theory]
    [InlineData(EvmTrafficLight.Green)]
    [InlineData(EvmTrafficLight.Amber)]
    [InlineData(EvmTrafficLight.Red)]
    [InlineData(EvmTrafficLight.Neutral)]
    [InlineData(EvmTrafficLight.Unknown)]
    public void Traffic_lights_defined(EvmTrafficLight light) => Assert.True(Enum.IsDefined(light));
}
