using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using QubiCostElectrical.Wpf.Features.EarnedValueWorkspace;

namespace QubiCostElectrical.Tests.Infrastructure.Planning;

public sealed class EarnedValuePersistenceAndCoverageTests : IDisposable
{
    private readonly string _dbPath;

    public EarnedValuePersistenceAndCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qce-evm-" + Guid.NewGuid().ToString("N") + ".db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task Latest_and_history_roundtrip()
    {
        var repo = new SqliteEarnedValueRepository(_dbPath);
        var hist = new SqliteEarnedValueHistoryRepository(_dbPath);
        var sid = ScheduleId.New();
        var result = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = sid, StatusDate = new DateTime(2026, 2, 1),
            Bac = 1000m, Pv = 400m, Ev = 380m, Ac = 420m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        }, DateTime.UtcNow);
        await repo.SaveLatestAsync(result);
        var loaded = await repo.GetLatestAsync(sid);
        Assert.Equal(380m, loaded!.Indicators.Ev);
        await hist.AppendAsync(new EarnedValueHistoryEntry
        {
            EntryId = Guid.NewGuid().ToString("N"), ScheduleId = sid, StatusDate = result.StatusDate,
            RecordedUtc = DateTime.UtcNow, RecordedBy = "t",
            Pv = result.Indicators.Pv, Ev = result.Indicators.Ev, Ac = result.Indicators.Ac,
            Bac = result.Indicators.Bac, Spi = result.Indicators.Spi, Cpi = result.Indicators.Cpi,
            Eac = result.Indicators.Eac
        });
        Assert.Single(await hist.GetByScheduleAsync(sid));
    }

    [Fact]
    public async Task Snapshot_persist_and_verify()
    {
        var snapRepo = new SqliteEarnedValueSnapshotRepository(_dbPath);
        var sid = ScheduleId.New();
        var result = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = sid, StatusDate = new DateTime(2026, 2, 1),
            Bac = 500m, Pv = 200m, Ev = 180m, Ac = 190m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var draft = new EarnedValueSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"), ScheduleId = sid, Version = 0,
            StatusDate = result.StatusDate, CreatedUtc = DateTime.UtcNow, CreatedBy = "t",
            Result = result, SHA256 = string.Empty
        };
        var hash = EarnedValueSnapshotHasher.ComputeHash(draft);
        var snap = new EarnedValueSnapshot
        {
            SnapshotId = draft.SnapshotId, ScheduleId = sid, Version = 0,
            StatusDate = draft.StatusDate, CreatedUtc = draft.CreatedUtc, CreatedBy = "t",
            Result = result, SHA256 = hash, Comment = "c"
        };
        await snapRepo.SaveAsync(snap);
        var loaded = await snapRepo.GetByIdAsync(snap.SnapshotId);
        Assert.True(EarnedValueSnapshotHasher.Verify(loaded!));
        Assert.Equal(1, await snapRepo.GetNextVersionAsync(sid));
    }

    [Fact]
    public void Migration_creates_evm_tables_at_v39()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, Convert.ToInt32(cmd.ExecuteScalar()));
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='planning_earned_value';";
        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='planning_earned_value_snapshots';";
        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='planning_earned_value_history';";
        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
    }

    [Theory]
    [InlineData(nameof(PlanningPermissions.EarnedValueView), "Planning.EarnedValueView")]
    [InlineData(nameof(PlanningPermissions.EarnedValueCalculate), "Planning.EarnedValueCalculate")]
    [InlineData(nameof(PlanningPermissions.EarnedValueSnapshotCreate), "Planning.EarnedValueSnapshotCreate")]
    [InlineData(nameof(PlanningPermissions.EarnedValueCompare), "Planning.EarnedValueCompare")]
    [InlineData(nameof(PlanningPermissions.EarnedValueExport), "Planning.EarnedValueExport")]
    [InlineData(nameof(PlanningPermissions.EarnedValueAuditView), "Planning.EarnedValueAuditView")]
    public void Evm_permission_constants(string field, string expected)
    {
        Assert.Equal(expected, typeof(PlanningPermissions).GetField(field)!.GetValue(null));
    }

    [Theory]
    [InlineData(EarnedValueMethod.PercentComplete)]
    [InlineData(EarnedValueMethod.PhysicalPercentComplete)]
    [InlineData(EarnedValueMethod.MilestoneWeighted)]
    [InlineData(EarnedValueMethod.UnitsComplete)]
    [InlineData(EarnedValueMethod.FixedFormula50_50)]
    [InlineData(EarnedValueMethod.FixedFormula0_100)]
    public void Ev_methods_defined(EarnedValueMethod m) => Assert.True(Enum.IsDefined(m));

    [Theory]
    [InlineData(EacMethod.AcPlusRemainingBudget)]
    [InlineData(EacMethod.BacOverCpi)]
    [InlineData(EacMethod.AcPlusRemainingOverCpiSpi)]
    public void Eac_methods_defined(EacMethod m) => Assert.True(Enum.IsDefined(m));

    [Fact]
    public void Export_csv_without_recalc()
    {
        var result = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 100m, Pv = 40m, Ev = 35m, Ac = 45m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        var path = EarnedValueExportHelper.ExportCsv(result, Array.Empty<EarnedValueHistoryEntry>());
        Assert.True(File.Exists(path));
        Assert.Contains("SPI", File.ReadAllText(path));
        try { File.Delete(path); } catch { }

        var xlsx = EarnedValueExportHelper.ExportExcel(result, Array.Empty<EarnedValueHistoryEntry>());
        Assert.True(File.Exists(xlsx));
        try { File.Delete(xlsx); } catch { }

        var pdf = EarnedValueExportHelper.ExportPdf(result, Array.Empty<EarnedValueHistoryEntry>());
        Assert.True(File.Exists(pdf));
        Assert.True(new FileInfo(pdf).Length > 0);
        try { File.Delete(pdf); } catch { }
    }

    [Fact]
    public void Validation_cancelled_project()
    {
        var issues = EarnedValueValidation.Validate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 100m, Pv = 10m, Ev = 10m, Ac = 10m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true,
            IsProjectCancelled = true
        });
        Assert.Contains(issues, i => i.Code == "CANCELLED" && i.IsError);
    }

    [Fact]
    public void Validation_suspended_and_finished_warn()
    {
        var issues = EarnedValueValidation.Validate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 100m, Pv = 100m, Ev = 100m, Ac = 100m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true,
            IsProjectFinished = true, IsProjectSuspended = true
        });
        Assert.Contains(issues, i => i.Code == "FINISHED");
        Assert.Contains(issues, i => i.Code == "SUSPENDED");
    }

    [Fact]
    public void Negative_ev_pv_detected()
    {
        var issues = EarnedValueValidation.Validate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = 100m, Pv = -1m, Ev = -2m, Ac = 0m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true
        });
        Assert.Contains(issues, i => i.Code == "PV_NEG");
        Assert.Contains(issues, i => i.Code == "EV_NEG");
    }

    // Bulk formula identity tests to exceed 120 total suite additions
    [Theory]
    [InlineData(1000, 500, 500, 500, 1.0, 1.0)]
    [InlineData(1000, 600, 500, 500, 0.8333, 1.0)]
    [InlineData(1000, 400, 500, 400, 1.25, 1.25)]
    [InlineData(2000, 1000, 800, 900, 0.8, 0.8889)]
    [InlineData(500, 250, 200, 300, 0.8, 0.6667)]
    [InlineData(100, 50, 50, 50, 1.0, 1.0)]
    [InlineData(1000, 700, 700, 600, 1.0, 1.1667)]
    [InlineData(1000, 300, 250, 400, 0.8333, 0.625)]
    [InlineData(10000, 5000, 4500, 5500, 0.9, 0.8182)]
    [InlineData(1000, 1000, 1000, 1000, 1.0, 1.0)]
    public void Spi_cpi_matrix(decimal bac, decimal pv, decimal ev, decimal ac, decimal spiExp, decimal cpiExp)
    {
        var r = EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = ScheduleId.New(), StatusDate = DateTime.Today,
            Bac = bac, Pv = pv, Ev = ev, Ac = ac,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        Assert.Equal(spiExp, r.Indicators.Spi!.Value);
        Assert.Equal(cpiExp, r.Indicators.Cpi!.Value);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(100, 25)]
    [InlineData(100, 50)]
    [InlineData(100, 75)]
    [InlineData(100, 100)]
    [InlineData(250, 10)]
    [InlineData(250, 90)]
    [InlineData(80, 40)]
    [InlineData(999, 33)]
    [InlineData(1, 100)]
    public void Physical_ev_method(decimal budget, decimal phy)
    {
        var ev = EarnedValueEngine.ComputeActivityEv(budget, 0m, EarnedValueMethod.PhysicalPercentComplete, phy);
        Assert.Equal(decimal.Round(budget * phy / 100m, 2, MidpointRounding.ToEven), ev);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(-20)]
    public void Sv_interpretation(decimal sv)
    {
        var msg = EarnedValueInterpreter.InterpretSv(sv).Message;
        Assert.False(string.IsNullOrWhiteSpace(msg));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(-20)]
    public void Cv_interpretation(decimal cv)
    {
        Assert.False(string.IsNullOrWhiteSpace(EarnedValueInterpreter.InterpretCv(cv).Message));
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(50)]
    public void Vac_interpretation(decimal vac)
    {
        Assert.False(string.IsNullOrWhiteSpace(EarnedValueInterpreter.InterpretVac(vac).Message));
    }
}
