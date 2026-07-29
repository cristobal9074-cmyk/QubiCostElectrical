using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Planning;

public sealed class PlanningPersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PlanningPersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"planning_test_{Guid.NewGuid():N}.db");
        using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        conn.Close();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task CreateSchedule_PersistsAndReopens()
    {
        var repo = new SqliteScheduleRepository(_dbPath);
        var schedule = new Schedule(ScheduleId.New(), "Test Schedule", "America/Santiago", CalendarId.New(), "user1");
        await repo.SaveAsync(schedule, _ct);
        SqliteConnection.ClearAllPools();
        var repo2 = new SqliteScheduleRepository(_dbPath);
        var loaded = await repo2.GetByIdAsync(schedule.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal(schedule.Name, loaded!.Name);
        Assert.Equal(schedule.TimeZoneId, loaded.TimeZoneId);
    }

    [Fact]
    public async Task WbsNode_PersistsAndLoadsBySchedule()
    {
        var schedRepo = new SqliteScheduleRepository(_dbPath);
        var wbsRepo = new SqliteWbsPlanningRepository(_dbPath);
        var scheduleId = ScheduleId.New();
        await schedRepo.SaveAsync(new Schedule(scheduleId, "WBS Test", "UTC", CalendarId.New(), "user1"), _ct);
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, "1", "Proyecto", WbsNodeType.Project, "user1");
        await wbsRepo.SaveAsync(node, _ct);
        var nodes = await wbsRepo.GetByScheduleAsync(scheduleId, _ct);
        Assert.Single(nodes);
        Assert.Equal("1", nodes[0].WbsCode);
    }

    [Fact]
    public async Task Activity_PersistsAndLoads()
    {
        var schedRepo = new SqliteScheduleRepository(_dbPath);
        var wbsRepo = new SqliteWbsPlanningRepository(_dbPath);
        var activityRepo = new SqliteActivityRepository(_dbPath);
        var scheduleId = ScheduleId.New();
        var wbsNodeId = WbsPlanningNodeId.New();
        await schedRepo.SaveAsync(new Schedule(scheduleId, "Act Test", "UTC", CalendarId.New(), "user1"), _ct);
        await wbsRepo.SaveAsync(new WbsPlanningNode(wbsNodeId, scheduleId, "1.1", "Fase", WbsNodeType.Phase, "user1"), _ct);
        var activity = new Activity(ActivityId.New(), scheduleId, wbsNodeId, "A001", "Excavar", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "user1");
        await activityRepo.SaveAsync(activity, _ct);
        var loaded = await activityRepo.GetByIdAsync(activity.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal("A001", loaded!.Code);
    }

    [Fact]
    public async Task Calendar_PersistsAndLoads()
    {
        var calRepo = new SqliteCalendarRepository(_dbPath);
        var cal = new Calendar(CalendarId.New(), "STD", "Estándar", "America/Santiago", "user1", CalendarType.Project);
        cal.WeeklyPattern.Add(new WeeklyWorkDay { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true });
        await calRepo.SaveAsync(cal, _ct);
        var loaded = await calRepo.GetByIdAsync(cal.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal("STD", loaded!.Code);
    }

    [Fact]
    public async Task UpdateSchedule_ModifiedAtUpdated()
    {
        var repo = new SqliteScheduleRepository(_dbPath);
        var schedule = new Schedule(ScheduleId.New(), "Original", "UTC", CalendarId.New(), "user1");
        await repo.SaveAsync(schedule, _ct);
        schedule.Name = "Modified";
        schedule.ModifiedBy = "user2";
        await repo.SaveAsync(schedule, _ct);
        var loaded = await repo.GetByIdAsync(schedule.Id, _ct);
        Assert.NotNull(loaded);
        Assert.Equal("Modified", loaded!.Name);
        Assert.Equal("user2", loaded.ModifiedBy);
    }

    [Fact]
    public async Task Activity_LogicalDelete_ExcludesFromScheduleQuery()
    {
        var schedRepo = new SqliteScheduleRepository(_dbPath);
        var wbsRepo = new SqliteWbsPlanningRepository(_dbPath);
        var activityRepo = new SqliteActivityRepository(_dbPath);
        var scheduleId = ScheduleId.New();
        var wbsNodeId = WbsPlanningNodeId.New();
        await schedRepo.SaveAsync(new Schedule(scheduleId, "Del Test", "UTC", CalendarId.New(), "user1"), _ct);
        await wbsRepo.SaveAsync(new WbsPlanningNode(wbsNodeId, scheduleId, "1.1", "Fase", WbsNodeType.Phase, "user1"), _ct);
        var activity = new Activity(ActivityId.New(), scheduleId, wbsNodeId, "A001", "To delete", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1");
        await activityRepo.SaveAsync(activity, _ct);
        var beforeDelete = await activityRepo.GetByScheduleAsync(scheduleId, _ct);
        Assert.Single(beforeDelete);
        await activityRepo.DeleteAsync(activity.Id, true, _ct);
        var afterDelete = await activityRepo.GetByScheduleAsync(scheduleId, _ct);
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task ActivityBatch_SaveMultiple_AllPersisted()
    {
        var schedRepo = new SqliteScheduleRepository(_dbPath);
        var wbsRepo = new SqliteWbsPlanningRepository(_dbPath);
        var activityRepo = new SqliteActivityRepository(_dbPath);
        var scheduleId = ScheduleId.New();
        var wbsNodeId = WbsPlanningNodeId.New();
        await schedRepo.SaveAsync(new Schedule(scheduleId, "Batch", "UTC", CalendarId.New(), "user1"), _ct);
        await wbsRepo.SaveAsync(new WbsPlanningNode(wbsNodeId, scheduleId, "1", "Root", WbsNodeType.Project, "user1"), _ct);
        var activities = new System.Collections.Generic.List<Activity>();
        for (int i = 1; i <= 5; i++)
            activities.Add(new Activity(ActivityId.New(), scheduleId, wbsNodeId, $"A{i:D4}", $"Act {i}", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1"));
        await activityRepo.SaveBatchAsync(activities, _ct);
        var loaded = await activityRepo.GetByScheduleAsync(scheduleId, _ct);
        Assert.Equal(5, loaded.Count);
    }

    [Fact]
    public async Task Schedule_Delete_RemovesCompletely()
    {
        var repo = new SqliteScheduleRepository(_dbPath);
        var schedule = new Schedule(ScheduleId.New(), "ToDelete", "UTC", CalendarId.New(), "user1");
        await repo.SaveAsync(schedule, _ct);
        await repo.DeleteAsync(schedule.Id, _ct);
        var loaded = await repo.GetByIdAsync(schedule.Id, _ct);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Calendar_ExistsAsync_ReturnsCorrect()
    {
        var repo = new SqliteCalendarRepository(_dbPath);
        var cal = new Calendar(CalendarId.New(), "EXIST", "Exists", "UTC", "user1", CalendarType.Project);
        await repo.SaveAsync(cal, _ct);
        Assert.True(await repo.ExistsAsync(cal.Id, _ct));
        Assert.False(await repo.ExistsAsync(CalendarId.New(), _ct));
    }
}
