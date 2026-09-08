using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Project;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ProjectLifecycleTests : IDisposable
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) =>
            RequestNavigate(route, force);

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    private readonly string tempPrefs;
    private readonly IDisposable prefsScope;

    public ProjectLifecycleTests()
    {
        WpfTestHost.EnsureInitialized();
        tempPrefs = Path.Combine(Path.GetTempPath(), $"lifecycle_prefs_{Guid.NewGuid():N}.json");
        prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(tempPrefs);
    }

    public void Dispose() => prefsScope.Dispose();

    [Fact]
    public async Task Close_WhenNoProjectOpen_ReturnsTrue()
    {
        WpfTestHost.EnsureInitialized();
        var svm = MakeVm();
        var result = await svm.CloseProjectAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task Close_WithNoChanges_ReturnsTrueWithoutDialog()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            var svm = MakeVm();
            var dialogCalled = false;
            svm.ShowCloseProjectDialog = () => { dialogCalled = true; return ThreeWayResult.Cancel; };

            var result = await svm.CloseProjectAsync();
            Assert.True(result);
            Assert.False(dialogCalled);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Close_WithDirtyState_SilentSave_ReturnsTrueWithoutDialog()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            host.Session.MarkDirty();
            var svm = MakeVm();
            svm.ShowCloseProjectDialog = () =>
                throw new InvalidOperationException("ShowCloseProjectDialog no debe llamarse en cierre silencioso.");

            var result = await svm.CloseProjectAsync();
            Assert.True(result);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Close_WithDirtyState_SilentSave_ClosesProject()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            host.Session.MarkDirty();
            var svm = MakeVm();
            svm.ShowCloseProjectDialog = () =>
                throw new InvalidOperationException("ShowCloseProjectDialog no debe llamarse en cierre silencioso.");

            var result = await svm.CloseProjectAsync();
            Assert.True(result);
            Assert.False(host.ProjectWorkspace.HasOpenProject);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void RecentProjects_StartsEmpty_WithFreshPrefs()
    {
        var svc = new RecentProjectsService();
        Assert.Empty(svc.GetProjects());
    }

    [Fact]
    public async Task ProjectViewModel_ApplyChanges_PersistsOnWorkspaceSave()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            var projectVm = CreateProjectVm();
            projectVm.Cliente = "Cliente Persistido";
            projectVm.Ubicacion = "Santiago Centro";
            Assert.True(projectVm.HasPendingChanges);
            Assert.True(projectVm.TryApplyChanges());

            await host.ProjectWorkspace.SaveAsync();

            var reload = new ProjectWorkspaceService(new BudgetProjectSession(), host.Services.ProjectRepository, host.Services.Validation);
            await reload.OpenAsync(path);

            Assert.Equal("Cliente Persistido", reload.Session.ProjectInfo.Cliente);
            Assert.Equal("Santiago Centro", reload.Session.ProjectInfo.Ubicacion);
            Assert.False(projectVm.HasPendingChanges);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Close_Save_PersistsProjectViewChanges()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            var projectVm = CreateProjectVm();
            projectVm.Cliente = "Cliente Cierre Guardado";
            Assert.True(projectVm.TryApplyChanges());
            await host.ProjectWorkspace.SaveAsync();

            var reload = new ProjectWorkspaceService(new BudgetProjectSession(), host.Services.ProjectRepository, host.Services.Validation);
            await reload.OpenAsync(path);
            Assert.Equal("Cliente Cierre Guardado", reload.Session.ProjectInfo.Cliente);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Close_Discard_DoesNotPersistProjectViewChanges()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var path = await SetupOpenProject(host);
        try
        {
            var projectVm = CreateProjectVm();
            projectVm.Cliente = "Cliente Cierre Descartado";
            Assert.True(projectVm.TryDiscardChanges());

            var reload = new ProjectWorkspaceService(new BudgetProjectSession(), host.Services.ProjectRepository, host.Services.Validation);
            await reload.OpenAsync(path);
            Assert.NotEqual("Cliente Cierre Descartado", reload.Session.ProjectInfo.Cliente);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static ShellViewModel MakeVm()
    {
        var svm = new ShellViewModel(WpfAppHost.Current);
        svm.PostCloseNavigate = () => { };
        return svm;
    }

    private static ProjectViewModel CreateProjectVm()
    {
        var vm = new ProjectViewModel(new FakeShell(), WpfAppHost.Current);
        vm.Load();
        return vm;
    }

    private static async Task<string> SetupOpenProject(WpfAppHost host)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lifecycle_{Guid.NewGuid():N}.qce");
        host.Session.NewProject("lifecycle-user");
        host.Session.ProjectInfo.Nombre = "Proyecto Prueba Lifecycle";
        await host.ProjectWorkspace.SaveAsAsync(path);
        return path;
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
            }
        }
    }
}
