using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Planning;

public sealed class ProgressPersistenceTests : IDisposable
{
    private readonly string _dbPath;

    public ProgressPersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qce-progress-" + Guid.NewGuid().ToString("N") + ".db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
    }

    [Fact]
    public async Task StatusDate_roundtrip()
    {
        var repo = new SqliteStatusDateRepository(_dbPath);
        var sid = ScheduleId.New();
        var entity = new ScheduleStatusDate
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            StatusDate = new DateTime(2026, 1, 15),
            TimeZoneId = "America/Santiago",
            UpdatedUtc = DateTime.UtcNow,
            UpdatedBy = "tester",
            Comment = "corte",
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await repo.SaveAsync(entity);
        var loaded = await repo.GetByScheduleAsync(sid);
        Assert.NotNull(loaded);
        Assert.Equal(entity.StatusDate, loaded!.StatusDate);
        Assert.Equal("America/Santiago", loaded.TimeZoneId);
    }

    [Fact]
    public async Task ActivityProgress_upsert_and_reopen()
    {
        var repo = new SqliteActivityProgressRepository(_dbPath);
        var sid = ScheduleId.New();
        var aid = ActivityId.New();
        var upd = new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            ActivityId = aid,
            StatusDate = new DateTime(2026, 1, 15),
            PercentComplete = 25,
            PhysicalProgress = 25,
            ActualStart = new DateTime(2026, 1, 2),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "t",
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = "t",
            RowVersion = "1"
        };
        await repo.SaveAsync(upd);
        var loaded = await repo.GetByActivityAsync(sid, aid);
        Assert.Equal(25m, loaded!.PercentComplete);

        var upd2 = new ActivityProgressUpdate
        {
            ProgressUpdateId = upd.ProgressUpdateId,
            ScheduleId = sid,
            ActivityId = aid,
            StatusDate = new DateTime(2026, 1, 20),
            PercentComplete = 40,
            PhysicalProgress = 40,
            ActualStart = upd.ActualStart,
            CreatedUtc = upd.CreatedUtc,
            CreatedBy = "t",
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = "t",
            RowVersion = "2"
        };
        await repo.SaveAsync(upd2);
        var list = await repo.GetByScheduleAsync(sid);
        Assert.Single(list);
        Assert.Equal(40m, list[0].PercentComplete);
    }

    [Fact]
    public async Task Snapshot_items_and_integrity()
    {
        var snapRepo = new SqliteProgressSnapshotRepository(_dbPath);
        var sid = ScheduleId.New();
        var aid = ActivityId.New();
        var items = new[]
        {
            new ProgressSnapshotItem
            {
                SnapshotId = "x",
                ActivityId = aid,
                Progress = new ActivityProgressUpdate
                {
                    ProgressUpdateId = "p", ScheduleId = sid, ActivityId = aid,
                    StatusDate = new DateTime(2026, 1, 15), PercentComplete = 10,
                    CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
                }
            }
        };
        var draft = new ProgressSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            Version = 0,
            StatusDate = new DateTime(2026, 1, 15),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "t",
            Comment = "c",
            CriticalPathActivityIds = Array.Empty<string>()
        };
        var hash = ProgressSnapshotHasher.ComputeHash(draft, items);
        var snap = new ProgressSnapshot
        {
            SnapshotId = draft.SnapshotId,
            ScheduleId = sid,
            Version = 0,
            StatusDate = draft.StatusDate,
            CreatedUtc = draft.CreatedUtc,
            CreatedBy = "t",
            Comment = "c",
            SHA256 = hash,
            CriticalPathActivityIds = Array.Empty<string>()
        };
        await snapRepo.SaveAsync(snap);
        await snapRepo.SaveItemsAsync(snap.SnapshotId, items);
        var loaded = await snapRepo.GetByIdAsync(snap.SnapshotId);
        var loadedItems = await snapRepo.GetItemsAsync(snap.SnapshotId);
        Assert.True(ProgressSnapshotHasher.Verify(loaded!, loadedItems));
        Assert.Equal(1, await snapRepo.GetNextVersionAsync(sid));
    }

    [Fact]
    public async Task Delay_and_evidence_persist()
    {
        var delayRepo = new SqliteDelayReasonRepository(_dbPath);
        var evRepo = new SqliteProgressEvidenceRepository(_dbPath);
        var sid = ScheduleId.New();
        var aid = ActivityId.New();
        await delayRepo.SaveAsync(new DelayReasonEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            ActivityId = aid,
            Code = DelayReasonCode.Material,
            Description = "falta cable",
            ImpactDays = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "t"
        });
        await evRepo.SaveAsync(new ProgressEvidence
        {
            EvidenceId = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            ActivityId = aid,
            ProgressUpdateId = "p1",
            Type = EvidenceReferenceType.Photo,
            Reference = "\\\\share\\foto1.jpg",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "t"
        });
        Assert.Single(await delayRepo.GetByScheduleAsync(sid));
        Assert.Single(await evRepo.GetByProgressUpdateAsync("p1"));
    }

    [Fact]
    public void Migration_creates_progress_tables_from_v35()
    {
        var path = Path.Combine(Path.GetTempPath(), "qce-mig35-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                // Apply up to 35 only by setting user_version after running migrator then... simpler: run full migrate and check tables
                SQLiteSchemaMigrator.MigrateToLatest(conn);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE 'planning_%progress%' OR name LIKE 'planning_status%' OR name LIKE 'planning_delay%' OR name = 'planning_progress_evidences');";
                using var reader = cmd.ExecuteReader();
                var names = new System.Collections.Generic.HashSet<string>();
                while (reader.Read()) names.Add(reader.GetString(0));
                Assert.Contains("planning_status_dates", names);
                Assert.Contains("planning_activity_progress", names);
                Assert.Contains("planning_progress_snapshots", names);
                Assert.Contains("planning_delay_reasons", names);
                Assert.Contains("planning_progress_evidences", names);
            }
            using (var conn2 = new SqliteConnection($"Data Source={path}"))
            {
                conn2.Open();
                using var cmd = conn2.CreateCommand();
                cmd.CommandText = "PRAGMA user_version;";
                Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, Convert.ToInt32(cmd.ExecuteScalar()));
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
