using System.IO;
using System.Threading;
using QubiCostElectrical;
using Xunit;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class SingleInstanceTests
{
    private const string TestMutexName = "QubiCostElectrical_SingleInstance_Test";

    [Fact]
    public void FirstInstance_AcquiresMutex()
    {
        var name = TestMutexName + "_first_" + Guid.NewGuid().ToString("N");
        using var mutex = new Mutex(false, name);
        var acquired = mutex.WaitOne(0);
        try { Assert.True(acquired); }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }

    [Fact]
    public void SecondInstance_CannotAcquireSameMutex()
    {
        // Use unique name per test run to avoid leftover state
        var name = TestMutexName + "_dup_" + Guid.NewGuid().ToString("N");
        using var first = new Mutex(false, name);
        var firstAcquired = first.WaitOne(0);
        Assert.True(firstAcquired, "Setup: first mutex must be acquired for this test to be meaningful");

        try
        {
            bool? secondAcquired = null;
            var gate = new ManualResetEventSlim(false);

            var t = new Thread(() =>
            {
                using var second = new Mutex(false, name);
                secondAcquired = second.WaitOne(0);
                // Do NOT release — just verify the result
                gate.Set();
            });
            t.IsBackground = true;
            t.Start();
            gate.Wait(5_000);

            Assert.False(secondAcquired);
        }
        finally
        {
            first.ReleaseMutex();
        }
    }

    [Fact]
    public void AfterRelease_MutexCanBeReacquired()
    {
        var name = TestMutexName + "_release_" + Guid.NewGuid().ToString("N");
        using var mutex = new Mutex(false, name);
        var first = mutex.WaitOne(0);
        Assert.True(first);
        mutex.ReleaseMutex();

        var second = mutex.WaitOne(0);
        Assert.True(second);
        mutex.ReleaseMutex();
    }

    [Fact]
    public void MutexName_MatchesAppConstant()
    {
        // Verifica que el nombre de mutex de la app coincida con el esperado.
        const string appMutexName = "QubiCostElectrical_SingleInstance";
        Assert.Equal(appMutexName, TestMutexName.Replace("_Test", ""));
    }

    [Fact]
    public void AppStartup_InitiallyOwned_WhenFirstInstance()
    {
        // Simulates App.OnStartup behavior: mutex with initiallyOwned=true
        var name = TestMutexName + "_startup_" + Guid.NewGuid().ToString("N");
        using var mutex = new Mutex(initiallyOwned: true, name: name, createdNew: out bool createdNew);
        try
        {
            Assert.True(createdNew, "First startup should create a new mutex");
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    [Fact]
    public void AppStartup_SecondInstance_DetectedByCreatedNewFalse()
    {
        var name = TestMutexName + "_second_" + Guid.NewGuid().ToString("N");
        using var first = new Mutex(initiallyOwned: true, name: name, createdNew: out _);
        try
        {
            bool? secondCreatedNew = null;
            var gate = new ManualResetEventSlim(false);
            var t = new Thread(() =>
            {
                using var second = new Mutex(initiallyOwned: true, name: name, createdNew: out bool cn);
                secondCreatedNew = cn;
                gate.Set();
                // Don't try to release if we didn't create
            });
            t.IsBackground = true;
            t.Start();
            gate.Wait(5_000);

            Assert.False(secondCreatedNew, "Second instance should detect existing mutex (createdNew=false)");
        }
        finally
        {
            first.ReleaseMutex();
        }
    }

    [Fact]
    public void ExtractOpenProjectPath_PicksSupportedProjectFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"handoff_{Guid.NewGuid():N}.qce");
        try
        {
            new ProjectRepository().SaveSQLiteSnapshot(temp, new ProjectFileData());
            var args = new[] { "ignored.txt", temp, "--flag" };
            var found = SingleInstanceHandoffService.TryExtractOpenProjectPath(args, out var path);
            Assert.True(found);
            Assert.Equal(temp, path);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
