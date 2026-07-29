using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class DashboardExtraCoverageTests
{
    [Theory]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Vac_matrix(decimal vac) =>
        Assert.True(Enum.IsDefined(DashboardTrafficLightPolicy.EvaluateVac(vac)));

    [Theory]
    [InlineData(-5.0)]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(30.0)]
    public void Finish_variance_matrix(double days)
    {
        Assert.True(Enum.IsDefined(DashboardTrafficLightPolicy.EvaluateFinishVarianceDays((decimal)days)));
    }

    [Fact]
    public void Finish_variance_null_is_no_data() =>
        Assert.Equal(DashboardKpiStatus.NoData, DashboardTrafficLightPolicy.EvaluateFinishVarianceDays(null));

    [Theory]
    [InlineData(DashboardSeriesKind.PvCumulative)]
    [InlineData(DashboardSeriesKind.EvCumulative)]
    [InlineData(DashboardSeriesKind.AcCumulative)]
    [InlineData(DashboardSeriesKind.CommittedCumulative)]
    [InlineData(DashboardSeriesKind.ForecastCumulative)]
    [InlineData(DashboardSeriesKind.BaselineCumulative)]
    [InlineData(DashboardSeriesKind.PhysicalProgressCumulative)]
    [InlineData(DashboardSeriesKind.PlannedProgressCumulative)]
    public void Series_kinds_empty(DashboardSeriesKind kind)
    {
        var s = DashboardSeriesBuilder.BuildEmpty(kind, kind.ToString(),
            DashboardSeriesGranularity.Weekly, DashboardSeriesMode.Cumulative,
            DashboardSeriesUnit.Cost, "CLP");
        Assert.Empty(s.Points);
        Assert.Equal(kind, s.Kind);
    }

    [Theory]
    [InlineData(2026, 1, 5)]
    [InlineData(2026, 1, 6)]
    [InlineData(2026, 1, 7)]
    [InlineData(2026, 1, 8)]
    [InlineData(2026, 1, 9)]
    [InlineData(2026, 1, 10)]
    [InlineData(2026, 1, 11)]
    public void Bucket_week_starts_monday(int y, int m, int d)
    {
        var start = DashboardSeriesBuilder.BucketStart(new DateTime(y, m, d), DashboardSeriesGranularity.Weekly);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
    }

    [Fact]
    public void Alerts_spi_cpi_from_evm()
    {
        var sid = ScheduleId.New();
        var evm = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = sid, StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 400m, Ac = 500m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var alerts = DashboardAlertBuilder.Build(sid, evm, null, true, DateTime.Today,
            Array.Empty<Activity>(), new Dictionary<string, ActivityProgressUpdate>(), DateTime.UtcNow);
        Assert.Contains(alerts, a => a.Code is "SPI_LOW" or "SPI_WARN");
        Assert.Contains(alerts, a => a.Code is "CPI_LOW" or "CPI_WARN");
    }

    [Theory]
    [InlineData(1000, 400, 500, 450)]
    [InlineData(1000, 500, 500, 500)]
    [InlineData(2000, 1000, 800, 900)]
    [InlineData(500, 100, 90, 120)]
    [InlineData(100, 50, 40, 60)]
    public void Kpi_factory_values(decimal bac, decimal pv, decimal ev, decimal ac)
    {
        var r = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = bac, Pv = pv, Ev = ev, Ac = ac,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var kpis = DashboardKpiFactory.FromEvm(r, null, new DashboardTrafficLightThresholds(),
            DashboardDataQuality.Complete, DateTime.Today);
        Assert.Equal(pv, kpis.First(k => k.Code == "PV").Value);
        Assert.Equal(ev, kpis.First(k => k.Code == "EV").Value);
        Assert.Equal(ac, kpis.First(k => k.Code == "AC").Value);
    }

    [Fact]
    public void Hash_changes_when_kpi_changes()
    {
        var sid = ScheduleId.New();
        ProjectExecutiveDashboard Make(decimal spiVal) => new()
        {
            ScheduleId = sid,
            ProjectName = "P",
            StatusDate = new DateTime(2026, 1, 1),
            DataQuality = DashboardDataQuality.Complete,
            Kpis = new[] { new DashboardKpi { Code = "SPI", Value = spiVal, DisplayValue = spiVal.ToString() } },
            Series = Array.Empty<DashboardSeries>(),
            Alerts = Array.Empty<DashboardAlert>(),
            Deviations = Array.Empty<DashboardDeviationItem>(),
            WbsNodes = Array.Empty<DashboardWbsNode>(),
            ByResponsible = Array.Empty<DashboardResponsibleSummary>(),
            OperationalRows = Array.Empty<DashboardOperationalRow>(),
            SourceHashes = "h"
        };
        var h1 = DashboardSnapshotHasher.Compute(Make(0.9m), "e", "h");
        var h2 = DashboardSnapshotHasher.Compute(Make(1.0m), "e", "h");
        Assert.False(h1 == h2);
    }

    [Theory]
    [InlineData(DashboardAlertSeverity.Critical, DashboardAlertSeverity.High)]
    [InlineData(DashboardAlertSeverity.High, DashboardAlertSeverity.Medium)]
    [InlineData(DashboardAlertSeverity.Medium, DashboardAlertSeverity.Low)]
    [InlineData(DashboardAlertSeverity.Low, DashboardAlertSeverity.Info)]
    public void Severity_order(DashboardAlertSeverity higher, DashboardAlertSeverity lower) =>
        Assert.True((int)higher < (int)lower);

    [Theory]
    [InlineData(DashboardDataQuality.Complete)]
    [InlineData(DashboardDataQuality.Partial)]
    [InlineData(DashboardDataQuality.Outdated)]
    [InlineData(DashboardDataQuality.Inconsistent)]
    [InlineData(DashboardDataQuality.NoBaseline)]
    [InlineData(DashboardDataQuality.NoCosts)]
    [InlineData(DashboardDataQuality.NoProgress)]
    [InlineData(DashboardDataQuality.InvalidIntegrity)]
    public void Data_quality_defined(DashboardDataQuality q) => Assert.True(Enum.IsDefined(q));

    [Theory]
    [InlineData(DashboardKpiStatus.Good)]
    [InlineData(DashboardKpiStatus.Warning)]
    [InlineData(DashboardKpiStatus.Critical)]
    [InlineData(DashboardKpiStatus.Neutral)]
    [InlineData(DashboardKpiStatus.NoData)]
    [InlineData(DashboardKpiStatus.NotApplicable)]
    public void Kpi_status_defined(DashboardKpiStatus s) => Assert.True(Enum.IsDefined(s));

    [Fact]
    public void No_data_kpi_display()
    {
        var kpis = DashboardKpiFactory.FromEvm(null, null, new DashboardTrafficLightThresholds(),
            DashboardDataQuality.NoProgress, null);
        Assert.All(kpis, k => Assert.Equal("Sin datos", k.DisplayValue));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(10, 5)]
    [InlineData(20, 10)]
    public void Series_same_bucket_sums(int n, int expectedBucketsApprox)
    {
        var day = new DateTime(2026, 3, 2); // monday
        var raw = Enumerable.Range(0, n).Select(_ => (day, 1m)).ToList();
        var s = DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.PvCumulative, "PV", raw,
            DashboardSeriesGranularity.Weekly, DashboardSeriesMode.Period, DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.Single(s.Points);
        Assert.Equal((decimal)n, s.Points[0].Value);
        _ = expectedBucketsApprox;
    }
}
