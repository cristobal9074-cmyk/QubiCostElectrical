using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// P0 Visor SC: setters de circuito/tablero respetan CanEdit y marcan dirty al editar.
/// AddCircuito/AddCuadro CanExecute en RO ya está en
/// <see cref="CommercialReadinessSmokeTests.ReadOnlySession_ApuAndSc_CannotEdit"/>.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionScReadOnlyTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.CubicacionSc;
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

    public CubicacionScReadOnlyTests() => WpfTestHost.EnsureInitialized();

    private static CubicacionScViewModel CreateWithProject(string projectName)
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject(projectName);
        host.Session.CurrentProjectPath = $"{projectName}.qce";
        return new CubicacionScViewModel(new FakeShell(), host);
    }

    [Fact]
    public void ReadOnly_CircuitoSetters_DoNotMutateTipoOrUbicacion()
    {
        var host = WpfAppHost.Current;
        var sc = CreateWithProject("csc-ro-setters");
        sc.AddCircuitoCommand.Execute(null);
        var circuito = sc.SelectedCircuito;
        Assert.NotNull(circuito);
        Assert.Equal("A", circuito!.Tipo);
        Assert.Equal("", circuito.Ubicacion);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");
            Assert.False(sc.CanEdit);

            circuito.Tipo = "E";
            circuito.Ubicacion = "piso-visor";

            Assert.Equal("A", circuito.Tipo);
            Assert.Equal("", circuito.Ubicacion);
            Assert.Equal("A", circuito.Source.Tipo);
            Assert.Equal("", circuito.Source.Ubicacion);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Estimator_CircuitoPropertyChange_MarksSessionDirty()
    {
        var host = WpfAppHost.Current;
        var sc = CreateWithProject("csc-estimador-dirty");
        sc.AddCircuitoCommand.Execute(null);
        var circuito = sc.SelectedCircuito;
        Assert.NotNull(circuito);

        Assert.True(sc.CanEdit);
        Assert.True(sc.TryApplyChanges());
        Assert.False(sc.HasPendingChanges);

        // Simula "guardado" para poder afirmar IsDirty de falso → verdadero tras editar.
        host.Session.MarkSaved();
        Assert.False(host.Session.IsDirty);

        circuito!.Tipo = "E";

        Assert.Equal("E", circuito.Tipo);
        Assert.True(sc.HasPendingChanges);
        Assert.True(host.Session.IsDirty);
    }

    [Fact]
    public void ReadOnly_CuadroNombre_DoesNotRename()
    {
        var host = WpfAppHost.Current;
        var sc = CreateWithProject("csc-ro-cuadro-nombre");
        var cuadro = Assert.Single(sc.Cuadros);
        var nombreOriginal = cuadro.Nombre;
        Assert.False(string.IsNullOrWhiteSpace(nombreOriginal));

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");
            Assert.False(sc.CanEdit);

            cuadro.Nombre = "Tablero hackeado por visor";

            Assert.Equal(nombreOriginal, cuadro.Nombre);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }
}
