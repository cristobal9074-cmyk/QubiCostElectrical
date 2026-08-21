using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Project;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ProjectViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Project;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) { CurrentRoute = route; return true; }
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => RequestNavigate(route, force);
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

    public ProjectViewModelTests() => WpfTestHost.EnsureInitialized();

    // Regresión: el IVA % no se podía editar desde la pantalla de Datos del proyecto (el
    // snapshot ni siquiera tenía el campo). Verifica que ahora se carga, se puede escribir
    // y queda pendiente de aplicar como cualquier otro campo comercial.
    [Fact]
    public async Task IvaPctText_LoadsFromProjectAndIsEditable()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("iva-editable");
        host.Session.ProjectInfo.IvaPct = 19m;
        var path = Path.Combine(Path.GetTempPath(), $"project-iva-{Guid.NewGuid():N}.qce");
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ProjectViewModel(new FakeShell(), host);
        vm.Load();

        Assert.Equal(19m.ToString("N1", CultureInfo.CurrentCulture), vm.IvaPctText);

        vm.IvaPctText = "0";

        Assert.True(vm.HasPendingChanges);

        vm.SaveCommand.Execute(null);

        Assert.Equal(0m, host.Session.ProjectInfo.IvaPct);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void Load_WithoutOpenProject_ShowsEmptyState()
    {
        var host = WpfAppHost.Current;
        // NewProject() deja CurrentProjectPath vacío hasta que se guarda por primera vez;
        // HasOpenProject depende de esa ruta, así que esto simula "sin proyecto abierto".
        host.Session.NewProject("temp-to-close");

        var vm = new ProjectViewModel(new FakeShell(), host);
        vm.Load();

        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.HasPendingChanges);
    }
}
