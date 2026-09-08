using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class AutoSaveFlushTests
{
    public AutoSaveFlushTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task FlushPersistAsync_WhenDirty_PersistsWithoutWaitingDebounce()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"flush_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("flush-user");
        host.Session.ProjectInfo.Nombre = "Flush Test";
        await host.ProjectWorkspace.SaveAsAsync(path);
        try
        {
            host.Session.MarkDirty("itemizado");
            Assert.True(host.Session.IsDirty);

            using var autoSave = new AutoSaveService(host.Session, host.ProjectWorkspace);
            autoSave.Start();
            await autoSave.FlushPersistAsync();

            Assert.False(host.Session.IsDirty);
        }
        finally
        {
            foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task PrepareForAppExitAsync_WhenDirty_SaveChoice_PersistsAndExits()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"exit_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("exit-user");
        host.Session.ProjectInfo.Nombre = "Exit Flush";
        await host.ProjectWorkspace.SaveAsAsync(path);
        try
        {
            host.Session.MarkDirty("itemizado");
            var svm = new ShellViewModel(host);
            svm.PostCloseNavigate = () => { };
            svm.ShowExitAppDialog = () => ThreeWayResult.Save;

            var ok = await svm.PrepareForAppExitAsync();
            Assert.True(ok);
            Assert.False(host.Session.IsDirty);
        }
        finally
        {
            foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task PrepareForAppExitAsync_WhenDirty_CancelChoice_AbortsExit()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"exit_cancel_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("exit-cancel");
        host.Session.ProjectInfo.Nombre = "Exit Cancel";
        await host.ProjectWorkspace.SaveAsAsync(path);
        try
        {
            host.Session.MarkDirty("itemizado");
            var svm = new ShellViewModel(host);
            svm.PostCloseNavigate = () => { };
            svm.ShowExitAppDialog = () => ThreeWayResult.Cancel;

            var ok = await svm.PrepareForAppExitAsync();
            Assert.False(ok);
            Assert.True(host.Session.IsDirty);
        }
        finally
        {
            foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task PrepareForAppExitAsync_WhenDirty_DiscardChoice_ExitsWithoutSaving()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"exit_discard_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("exit-discard");
        host.Session.ProjectInfo.Nombre = "Exit Discard";
        await host.ProjectWorkspace.SaveAsAsync(path);
        try
        {
            host.Session.ProjectInfo.Nombre = "Cambiado sin guardar";
            host.Session.MarkDirty("project_info");
            var svm = new ShellViewModel(host);
            svm.PostCloseNavigate = () => { };
            svm.ShowExitAppDialog = () => ThreeWayResult.Discard;

            var ok = await svm.PrepareForAppExitAsync();
            Assert.True(ok);

            await host.ProjectWorkspace.OpenAsync(path);
            Assert.Equal("Exit Discard", host.Session.ProjectInfo.Nombre);
        }
        finally
        {
            foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
            }
        }
    }
}
