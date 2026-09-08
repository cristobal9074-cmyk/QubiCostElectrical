using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Al cambiar de pestaña ya no se muestra Guardar/Descartar/Cancelar:
/// se aplica el draft y se persiste el .qce en background.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class SilentLeavePageNavigationTests
{
    public SilentLeavePageNavigationTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task RequestNavigate_WhenSessionDirty_DoesNotCallShowLeavePageDialog()
    {
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host, "silent-leave-dirty");
        try
        {
            RunOnSta(() =>
            {
                host.Session.MarkSaved();
                host.Session.MarkDirty("silent-leave");

                using var shell = MakeShell();
                shell.ShowLeavePageDialog = () =>
                    throw new InvalidOperationException(
                        "ShowLeavePageDialog no debe llamarse al cambiar de pestaña.");

                shell.SeedCachedPageForTests(ShellRoute.Dashboard, viewModel: null);
                shell.SeedCachedPageForTests(ShellRoute.Project, viewModel: null);
                shell.ActivateSeededPageForTests(ShellRoute.Dashboard);

                Assert.True(host.Session.IsDirty);
                Assert.True(shell.RequestNavigate(ShellRoute.Project));
                Assert.Equal(ShellRoute.Project, shell.CurrentRoute);
            });

            await WaitUntilSavedOrTimeout(host);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task RequestNavigate_WithPendingCommitPage_AppliesChanges_AndDoesNotPrompt()
    {
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host, "silent-leave-commit");
        try
        {
            RunOnSta(() =>
            {
                host.Session.MarkSaved();

                using var shell = MakeShell();
                shell.ShowLeavePageDialog = () =>
                    throw new InvalidOperationException(
                        "ShowLeavePageDialog no debe llamarse al cambiar de pestaña.");

                var pageVm = new CubicacionScViewModel(shell, host);
                Assert.False(pageVm.HasPendingChanges);

                pageVm.AddCircuitoCommand.Execute(null);
                Assert.True(pageVm.HasPendingChanges);
                Assert.True(host.Session.IsDirty);

                shell.SeedCachedPageForTests(ShellRoute.CubicacionSc, pageVm);
                shell.SeedCachedPageForTests(ShellRoute.Project, viewModel: null);
                shell.ActivateSeededPageForTests(ShellRoute.CubicacionSc);

                Assert.True(shell.RequestNavigate(ShellRoute.Project));
                Assert.Equal(ShellRoute.Project, shell.CurrentRoute);
                Assert.False(pageVm.HasPendingChanges);
                Assert.IsAssignableFrom<IPageWithCommitChanges>(pageVm);
            });

            await WaitUntilSavedOrTimeout(host);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task TryLeaveCurrentPage_WhenDirty_ReturnsTrue_WithoutDialog()
    {
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host, "silent-leave-direct");
        try
        {
            host.Session.MarkSaved();
            host.Session.MarkDirty("direct-leave");

            using var shell = MakeShell();
            shell.ShowLeavePageDialog = () =>
                throw new InvalidOperationException(
                    "ShowLeavePageDialog no debe llamarse al cambiar de pestaña.");

            Assert.True(shell.TryLeaveCurrentPage());

            await WaitUntilSavedOrTimeout(host);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static ShellViewModel MakeShell()
    {
        var shell = new ShellViewModel(WpfAppHost.Current);
        shell.PostCloseNavigate = () => { };
        return shell;
    }

    private static async Task<string> SetupOpenProject(WpfAppHost host, string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"silent_leave_{Guid.NewGuid():N}.qce");
        host.Session.NewProject(name);
        host.Session.ProjectInfo.Nombre = name;
        await host.ProjectWorkspace.SaveAsAsync(path);
        return path;
    }

    private static async Task WaitUntilSavedOrTimeout(WpfAppHost host, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (host.Session.IsDirty && DateTime.UtcNow < deadline)
            await Task.Delay(50);
    }

    private static void Cleanup(string path)
    {
        foreach (var p in new[] { path, path + ".autosave", path + ".lock" })
        {
            try
            {
                if (File.Exists(p))
                    File.Delete(p);
            }
            catch
            {
                // best-effort: el guardado en background puede retener el archivo un instante
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        captured?.Throw();
    }
}
