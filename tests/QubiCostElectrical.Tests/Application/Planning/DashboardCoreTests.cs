using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class DashboardCoreTests
{
    [Theory]
    [InlineData(1.0, DashboardKpiStatus.Good)]
    [InlineData(1.05, DashboardKpiStatus.Good)]
    [InlineData(0.95, DashboardKpiStatus.Warning)]
    [InlineData(0.90, DashboardKpiStatus.Warning)]
    [InlineData(0.89, DashboardKpiStatus.Critical)]
    [InlineData(0.5, DashboardKpiStatus.Critical)]
    public void Spi_thresholds(decimal spi, DashboardKpiStatus expected) =>
        Assert.Equal(expected, DashboardTrafficLightPolicy.EvaluateSpi(spi));

    [Theory]
    [InlineData(1.0, DashboardKpiStatus.Good)]
    [InlineData(0.95, DashboardKpiStatus.Warning)]
    [InlineData(0.89, DashboardKpiStatus.Critical)]
    public void Cpi_thresholds(decimal cpi, DashboardKpiStatus expected) =>
        Assert.Equal(expected, DashboardTrafficLightPolicy.EvaluateCpi(cpi));

    [Fact]
    public void Spi_null_is_no_data() =>
        Assert.Equal(DashboardKpiStatus.NoData, DashboardTrafficLightPolicy.EvaluateSpi(null));

    [Fact]
    public void Cpi_null_is_no_data() =>
        Assert.Equal(DashboardKpiStatus.NoData, DashboardTrafficLightPolicy.EvaluateCpi(null));

    [Theory]
    [InlineData(-1, DashboardKpiStatus.Critical)]
    [InlineData(0, DashboardKpiStatus.Neutral)]
    [InlineData(10, DashboardKpiStatus.Good)]
    public void Vac_status(decimal vac, DashboardKpiStatus expected) =>
        Assert.Equal(expected, DashboardTrafficLightPolicy.EvaluateVac(vac));

    [Theory]
    [InlineData(1, DashboardKpiStatus.Critical)]
    [InlineData(0, DashboardKpiStatus.Neutral)]
    [InlineData(-2, DashboardKpiStatus.Good)]
    public void Finish_variance(decimal days, DashboardKpiStatus expected) =>
        Assert.Equal(expected, DashboardTrafficLightPolicy.EvaluateFinishVarianceDays(days));

    [Fact]
    public void Custom_spi_thresholds()
    {
        var t = new DashboardTrafficLightThresholds { SpiGreenMin = 1.05m, SpiAmberMin = 0.95m };
        Assert.Equal(DashboardKpiStatus.Warning, DashboardTrafficLightPolicy.EvaluateSpi(1.0m, t));
        Assert.Equal(DashboardKpiStatus.Good, DashboardTrafficLightPolicy.EvaluateSpi(1.05m, t));
    }

    [Theory]
    [InlineData(DashboardSeriesGranularity.Daily)]
    [InlineData(DashboardSeriesGranularity.Weekly)]
    [InlineData(DashboardSeriesGranularity.Monthly)]
    public void Series_builder_orders_and_groups(DashboardSeriesGranularity g)
    {
        var raw = new List<(DateTime, decimal)>
        {
            (new DateTime(2026, 1, 10), 10m),
            (new DateTime(2026, 1, 5), 20m),
            (new DateTime(2026, 1, 20), 5m)
        };
        var s = DashboardSeriesBuilder.BuildFromHistory(
            DashboardSeriesKind.PvCumulative, "PV", raw, g,
            DashboardSeriesMode.Cumulative, DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.True(s.Points.Count >= 1);
        Assert.True(s.Points.All(p => p.HasData));
        for (var i = 1; i < s.Points.Count; i++)
            Assert.True(s.Points[i].PeriodStart >= s.Points[i - 1].PeriodStart);
        Assert.Equal(35m, s.Points[^1].Value);
    }

    [Fact]
    public void Series_period_mode_not_cumulative()
    {
        var raw = new[] { (new DateTime(2026, 2, 1), 10m), (new DateTime(2026, 2, 2), 5m) };
        var s = DashboardSeriesBuilder.BuildFromHistory(
            DashboardSeriesKind.AcCumulative, "AC", raw, DashboardSeriesGranularity.Daily,
            DashboardSeriesMode.Period, DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.Equal(2, s.Points.Count);
        Assert.Equal(10m, s.Points[0].Value);
        Assert.Equal(5m, s.Points[1].Value);
    }

    [Fact]
    public void Series_empty_when_no_points()
    {
        var s = DashboardSeriesBuilder.BuildFromHistory(
            DashboardSeriesKind.EvCumulative, "EV", Array.Empty<(DateTime, decimal)>(),
            DashboardSeriesGranularity.Weekly, DashboardSeriesMode.Cumulative,
            DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.Empty(s.Points);
    }

    [Fact]
    public void Series_range_filter()
    {
        var raw = new[]
        {
            (new DateTime(2026, 1, 1), 1m),
            (new DateTime(2026, 3, 1), 2m),
            (new DateTime(2026, 5, 1), 3m)
        };
        var s = DashboardSeriesBuilder.BuildFromHistory(
            DashboardSeriesKind.PvCumulative, "PV", raw, DashboardSeriesGranularity.Monthly,
            DashboardSeriesMode.Period, DashboardSeriesUnit.Cost, "CLP",
            new DateTime(2026, 2, 1), new DateTime(2026, 4, 1));
        Assert.Single(s.Points);
        Assert.Equal(2m, s.Points[0].Value);
    }

    [Fact]
    public void Alert_priority_deterministic()
    {
        var sid = ScheduleId.New();
        var a = new DashboardAlert
        {
            AlertId = "a", ScheduleId = sid, Code = "B", Severity = DashboardAlertSeverity.High,
            ImpactScore = 10, RaisedUtc = DateTime.UtcNow, EntityId = "2", Title = "b"
        };
        var b = new DashboardAlert
        {
            AlertId = "b", ScheduleId = sid, Code = "A", Severity = DashboardAlertSeverity.Critical,
            ImpactScore = 5, RaisedUtc = DateTime.UtcNow, EntityId = "1", Title = "a"
        };
        var ordered = DashboardAlertBuilder.Prioritize(new[] { a, b });
        Assert.Equal("b", ordered[0].AlertId);
    }

    [Fact]
    public void Alerts_without_baseline()
    {
        var sid = ScheduleId.New();
        var alerts = DashboardAlertBuilder.Build(sid, null, null, false, null,
            Array.Empty<Activity>(), new Dictionary<string, ActivityProgressUpdate>(), DateTime.UtcNow);
        Assert.Contains(alerts, x => x.Code == "NO_BASELINE");
        Assert.Contains(alerts, x => x.Code == "NO_STATUS_DATE");
    }

    [Fact]
    public void Kpi_factory_from_evm()
    {
        var result = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(),
            StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 400m, Ac = 450m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var kpis = DashboardKpiFactory.FromEvm(result, null, new DashboardTrafficLightThresholds(),
            DashboardDataQuality.Complete, DateTime.Today);
        Assert.Contains(kpis, k => k.Code == "SPI");
        Assert.Contains(kpis, k => k.Code == "CPI");
        Assert.Contains(kpis, k => k.Code == "VAC");
        Assert.Equal(DashboardKpiStatus.Critical, kpis.First(k => k.Code == "SPI").Status);
    }

    [Fact]
    public void Snapshot_hash_stable()
    {
        var dash = new ProjectExecutiveDashboard
        {
            ScheduleId = ScheduleId.New(),
            ProjectName = "P",
            StatusDate = new DateTime(2026, 1, 1),
            CurrencyCode = "CLP",
            DataQuality = DashboardDataQuality.Complete,
            Kpis = Array.Empty<DashboardKpi>(),
            Series = Array.Empty<DashboardSeries>(),
            Alerts = Array.Empty<DashboardAlert>(),
            Deviations = Array.Empty<DashboardDeviationItem>(),
            WbsNodes = Array.Empty<DashboardWbsNode>(),
            ByResponsible = Array.Empty<DashboardResponsibleSummary>(),
            OperationalRows = Array.Empty<DashboardOperationalRow>(),
            SourceHashes = "abc"
        };
        var h1 = DashboardSnapshotHasher.Compute(dash, "e1", "abc");
        var h2 = DashboardSnapshotHasher.Compute(dash, "e1", "abc");
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.89)]
    [InlineData(0.9)]
    [InlineData(0.99)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.5)]
    public void Spi_boundary_matrix(decimal spi)
    {
        var s = DashboardTrafficLightPolicy.EvaluateSpi(spi);
        Assert.True(Enum.IsDefined(s));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.89)]
    [InlineData(0.9)]
    [InlineData(0.99)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.5)]
    public void Cpi_boundary_matrix(decimal cpi)
    {
        var s = DashboardTrafficLightPolicy.EvaluateCpi(cpi);
        Assert.True(Enum.IsDefined(s));
    }
}
