using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class LockLostPendingExitTests
{
    public LockLostPendingExitTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task PrepareForAppExitAsync_WhenLockLostAndDirty_SkipsQce_WritesSidecar()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"lockexit_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("exit-lock");
        host.Session.ProjectInfo.Nombre = "Exit LockLost";
        await host.ProjectWorkspace.SaveAsAsync(path);

        try
        {
            host.Session.MarkDirty("itemizado");
            host.Session.SetLockReadOnly(true, "Otro usuario tomó el control de edición. Solo lectura.");

            var beforeUtc = File.GetLastWriteTimeUtc(path);
            var svm = new ShellViewModel(host);
            svm.PostCloseNavigate = () => { };

            var ok = await svm.PrepareForAppExitAsync();
            Assert.True(ok);
            Assert.True(host.Session.IsDirty);
            Assert.Equal(beforeUtc, File.GetLastWriteTimeUtc(path));
            Assert.True(File.Exists(path + ".bak"));
        }
        finally
        {
            host.Session.SetLockReadOnly(false);
            foreach (var p in new[] { path, path + ".bak", path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task FlushPersistAsync_WhenReadOnly_IsNoOp()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"flushro_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("flush-ro");
        host.Session.ProjectInfo.Nombre = "Flush RO";
        await host.ProjectWorkspace.SaveAsAsync(path);

        try
        {
            host.Session.MarkDirty("itemizado");
            host.Session.SetLockReadOnly(true, "solo lectura");
            var beforeUtc = File.GetLastWriteTimeUtc(path);

            using var autoSave = new AutoSaveService(host.Session, host.ProjectWorkspace);
            await autoSave.FlushPersistAsync();

            Assert.True(host.Session.IsDirty);
            Assert.Equal(beforeUtc, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            host.Session.SetLockReadOnly(false);
            foreach (var p in new[] { path, path + ".bak", path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }
}
