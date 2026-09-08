using System;
using System.IO;
using System.Threading.Tasks;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectLockMidWriteTests
{
    private static CollaborationUserContext TestUser(string name = "Estimador Test")
        => CollaborationUserContext.FromEnvironment("user-test", name, "test@qubicost.com", "Estimador");

    private static ProjectLockData ForeignLock(string path) => new()
    {
        Version = 2,
        MachineName = "REMOTE-PC",
        ProcessId = 999999,
        InstanceId = "foreign-instance",
        UserDisplayName = "Otro",
        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
        LastHeartbeatUtc = DateTime.UtcNow,
        NormalizedPath = Path.GetFullPath(path)
    };

    [Fact]
    public void OwnsActiveLock_False_AfterForeignPayloadOnDisk()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            Assert.True(svc.TryAcquireLock(path, TestUser()));
            Assert.True(svc.OwnsActiveLock(path));

            svc.SimulateForeignLockOnDiskForTests(path, ForeignLock(path));
            Assert.False(svc.OwnsActiveLock(path));
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".lock");
        }
    }

    [Fact]
    public void WriteLease_DefersLockLost_UntilDisposed()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            Assert.True(svc.TryAcquireLock(path, TestUser()));

            var lost = 0;
            svc.LockLost += (_, _) => lost++;

            using (var lease = svc.TryBeginWriteLease(path))
            {
                Assert.NotNull(lease);
                svc.SimulateForeignLockOnDiskForTests(path, ForeignLock(path));
                svc.RunHeartbeatForTests();
                Assert.Equal(0, lost);
            }

            Assert.Equal(1, lost);
            Assert.False(svc.OwnsActiveLock(path));
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".lock");
        }
    }

    [Fact]
    public void Heartbeat_WithoutLease_RaisesLockLostOnce()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            Assert.True(svc.TryAcquireLock(path, TestUser()));

            var lost = 0;
            svc.LockLost += (_, _) => lost++;

            svc.SimulateForeignLockOnDiskForTests(path, ForeignLock(path));
            svc.RunHeartbeatForTests();
            svc.RunHeartbeatForTests();

            Assert.Equal(1, lost);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".lock");
        }
    }

    [Fact]
    public async Task SaveAsync_WhenLockLostBeforeWrite_Throws_DoesNotMarkSaved()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("lock-lost");
        session.ProjectInfo.Nombre = "Lock Lost Save";

        using var lockSvc = new ProjectCollaborationLockService();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation, lockSvc);
        var path = Path.Combine(Path.GetTempPath(), $"locklost_{Guid.NewGuid():N}.qce");

        try
        {
            await workspace.SaveAsAsync(path);
            Assert.True(lockSvc.TryAcquireLock(path, TestUser()));

            session.MarkDirty("itemizado");
            Assert.True(session.IsDirty);

            lockSvc.SimulateForeignLockOnDiskForTests(path, ForeignLock(path));
            lockSvc.RunHeartbeatForTests();
            Assert.True(lockSvc.RequiresExclusiveLease(path));
            Assert.False(lockSvc.OwnsActiveLock(path));

            await Assert.ThrowsAsync<ProjectLockLostException>(() => workspace.SaveAsync());
            Assert.True(session.IsDirty);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".autosave");
            TryDelete(path + ".lock");
        }
    }

    [Fact]
    public async Task SaveAsync_WhenForeignOnDisk_Throws_KeepsDirty()
    {
        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        session.NewProject("mid-lease");
        session.ProjectInfo.Nombre = "Mid Lease";

        using var lockSvc = new ProjectCollaborationLockService();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation, lockSvc);
        var path = Path.Combine(Path.GetTempPath(), $"midlease_{Guid.NewGuid():N}.qce");

        try
        {
            await workspace.SaveAsAsync(path);
            Assert.True(lockSvc.TryAcquireLock(path, TestUser()));
            session.MarkDirty("itemizado");

            lockSvc.SimulateForeignLockOnDiskForTests(path, ForeignLock(path));

            await Assert.ThrowsAsync<ProjectLockLostException>(() => workspace.SaveAsync());
            Assert.True(session.IsDirty);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".autosave");
            TryDelete(path + ".lock");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
