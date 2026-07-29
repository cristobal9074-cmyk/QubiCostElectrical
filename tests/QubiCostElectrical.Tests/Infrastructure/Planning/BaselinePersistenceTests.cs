using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Planning;

public sealed class BaselinePersistenceTests : IDisposable
{
    private readonly string _dbPath;

    public BaselinePersistenceTests()
    {
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_baselines_{Guid.NewGuid():N}.db");
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        
        using var conn = ProfessionalDbHelpers.Open(_dbPath);
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task BaselineRepository_saves_and_retrieves_correctly()
    {
        var repo = new SqliteScheduleBaselineRepository(_dbPath);

        var baseline = new ScheduleBaseline
        {
            BaselineId = "BASE-001",
            ScheduleId = new ScheduleId("SCH-1"),
            Version = 0,
            Name = "Initial Baseline",
            Description = "Snapshot description",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "Tester",
            Status = "Draft",
            SHA256 = "dummy-hash",
            SnapshotDate = DateTime.UtcNow,
            IsOfficial = false
        };

        await repo.SaveAsync(baseline);

        var exists = await repo.ExistsAsync("BASE-001");
        Assert.True(exists);

        var retrieved = await repo.GetByIdAsync("BASE-001");
        Assert.NotNull(retrieved);
        Assert.Equal(baseline.BaselineId, retrieved.BaselineId);
        Assert.Equal(baseline.Name, retrieved.Name);
        Assert.Equal(baseline.Version, retrieved.Version);
        Assert.Equal("Draft", retrieved.Status);
        Assert.False(retrieved.IsOfficial);

        var bySched = await repo.GetByScheduleAsync(new ScheduleId("SCH-1"));
        Assert.Single(bySched);
        Assert.Equal(baseline.BaselineId, bySched[0].BaselineId);

        // Test delete
        await repo.DeleteAsync("BASE-001");
        exists = await repo.ExistsAsync("BASE-001");
        Assert.False(exists);
    }
    [Fact]
    public async Task BaselineSnapshotRepository_saves_and_retrieves_correctly()
    {
        var repo = new SqliteScheduleBaselineSnapshotRepository(_dbPath);

        var baselineRepo = new SqliteScheduleBaselineRepository(_dbPath);
        await baselineRepo.SaveAsync(new ScheduleBaseline
        {
            BaselineId = "BASE-001",
            ScheduleId = new ScheduleId("SCH-1"),
            Version = 0,
            Name = "Initial Baseline",
            SHA256 = "dummy-hash"
        });

        var schedule = new Schedule(new ScheduleId("SCH-1"), "My Schedule", "Pacific/Easter", new CalendarId("CAL-1"), "Tester");
        var snapshot = new ScheduleBaselineSnapshot
        {
            Schedule = schedule,
            Activities = new() 
            {
                new Activity(new ActivityId("A1"), new ScheduleId("SCH-1"), new WbsPlanningNodeId("W1"), "A1", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Tester")
            },
            Dependencies = new(),
            Calendars = new()
        };

        await repo.SaveAsync("BASE-001", snapshot);

        var retrieved = await repo.GetByBaselineIdAsync("BASE-001");
        Assert.NotNull(retrieved);
        Assert.Equal("My Schedule", retrieved.Schedule.Name);
        Assert.Single(retrieved.Activities);
        Assert.Equal("A1", retrieved.Activities[0].Code);

        // Delete
        await repo.DeleteAsync("BASE-001");
        retrieved = await repo.GetByBaselineIdAsync("BASE-001");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetNextVersionAsync_calculates_correct_version()
    {
        var repo = new SqliteScheduleBaselineRepository(_dbPath);
        var schedId = new ScheduleId("SCH-2");

        var ver1 = await repo.GetNextVersionAsync(schedId);
        Assert.Equal(0, ver1);

        var baseline1 = new ScheduleBaseline
        {
            BaselineId = "B1",
            ScheduleId = schedId,
            Version = 0,
            Name = "Ver 0"
        };
        await repo.SaveAsync(baseline1);

        var ver2 = await repo.GetNextVersionAsync(schedId);
        Assert.Equal(1, ver2);
    }
}
