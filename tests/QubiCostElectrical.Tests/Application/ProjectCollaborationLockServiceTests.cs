using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectCollaborationLockServiceTests
{
    private static CollaborationUserContext TestUser()
        => CollaborationUserContext.FromEnvironment("user-test", "Estimador Test", "test@qubicost.com", "Estimador");

    [Fact]
    public void TryAcquireLock_CreatesLockFileWithUserIdentity()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            var user = TestUser();
            var acquired = svc.TryAcquireLock(path, user);
            Assert.True(acquired);
            Assert.True(File.Exists(path + ".lock"));

            var inspection = svc.InspectLock(path);
            Assert.Equal(ProjectLockState.OwnLock, inspection.State);
            Assert.Equal("Estimador Test", inspection.Data?.UserDisplayName);
            Assert.Equal("test@qubicost.com", inspection.Data?.UserEmail);
        }
        finally
        {
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }

    [Fact]
    public void CheckLock_ReturnsNotLocked_WhenNoLockFile()
    {
        var path = @"C:\__nonexistent__\test.qce";
        using var svc = new ProjectCollaborationLockService();
        var result = svc.CheckLock(path);
        Assert.Equal(LockCheckResult.NotLocked, result);
    }

    [Fact]
    public void CheckLock_ReturnsLockedBySameMachineAlive()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            svc.TryAcquireLock(path, TestUser());
            var result = svc.CheckLock(path);
            Assert.Equal(LockCheckResult.LockedBySameMachineAlive, result);
        }
        finally
        {
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }

    [Fact]
    public void ReleaseLock_RemovesLockFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var svc = new ProjectCollaborationLockService();
            svc.TryAcquireLock(path, TestUser());
            Assert.True(File.Exists(path + ".lock"));
            svc.ReleaseLock();
            Assert.False(File.Exists(path + ".lock"));
        }
        finally
        {
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }

    [Fact]
    public void TryAcquireLock_ForAnotherProject_ReleasesPreviousLock()
    {
        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        try
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
            using var svc = new ProjectCollaborationLockService();

            Assert.True(svc.TryAcquireLock(firstPath, TestUser()));
            Assert.True(svc.TryAcquireLock(secondPath, TestUser()));

            Assert.False(File.Exists(firstPath + ".lock"));
            Assert.True(File.Exists(secondPath + ".lock"));
        }
        finally
        {
            if (File.Exists(firstPath + ".lock")) File.Delete(firstPath + ".lock");
            if (File.Exists(secondPath + ".lock")) File.Delete(secondPath + ".lock");
        }
    }

    [Fact]
    public void ForceAcquireExpiredLock_AllowsTakeOverAfterHeartbeatExpiry()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            var stale = new ProjectLockData
            {
                Version = 2,
                MachineName = "REMOTE-PC",
                ProcessId = 999999,
                InstanceId = "stale-instance",
                UserDisplayName = "Otro Estimador",
                UserEmail = "otro@empresa.cl",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-20),
                NormalizedPath = Path.GetFullPath(path)
            };
            File.WriteAllText(path + ".lock", System.Text.Json.JsonSerializer.Serialize(stale));

            using var svc = new ProjectCollaborationLockService();
            var inspection = svc.InspectLock(path);
            Assert.Equal(ProjectLockState.HeartbeatExpired, inspection.State);
            Assert.True(inspection.CanTakeOver);

            var acquired = svc.ForceAcquireExpiredLock(path, TestUser());
            Assert.True(acquired);
            Assert.Contains("Estimador Test", svc.InspectLock(path).Data?.UserDisplayName ?? "");
        }
        finally
        {
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }

    [Fact]
    public void InspectLock_BlocksRemoteRecentLock()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            var remote = new ProjectLockData
            {
                Version = 2,
                MachineName = "REMOTE-PC",
                ProcessId = 999999,
                InstanceId = "remote-instance",
                UserDisplayName = "Ana Estimadora",
                CreatedAtUtc = DateTime.UtcNow,
                LastHeartbeatUtc = DateTime.UtcNow,
                NormalizedPath = Path.GetFullPath(path)
            };
            File.WriteAllText(path + ".lock", System.Text.Json.JsonSerializer.Serialize(remote));

            using var svc = new ProjectCollaborationLockService();
            var inspection = svc.InspectLock(path);
            Assert.Equal(ProjectLockState.RemoteRecent, inspection.State);
            Assert.False(inspection.CanTakeOver);
            Assert.Contains("Ana Estimadora", inspection.HolderSummary);
        }
        finally
        {
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }
}
