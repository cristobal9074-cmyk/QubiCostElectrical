using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class QuickJumpPartidaTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
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

    public QuickJumpPartidaTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void QuickJumpShortcut_IsRegisteredWithCtrlG()
    {
        var def = CubicacionShortcutCatalog.Find(CubicacionShortcutCatalog.QuickJump);
        Assert.NotNull(def);
        Assert.Equal("Ctrl+G", def.DefaultGesture);
        Assert.Equal("cubicacion", def.Scope);
    }

    [Fact]
    public void QuickJumpPartidaItem_DisplayProperties_FormattedCorrectly()
    {
        var item = new QuickJumpPartidaItem
        {
            Id = "P1",
            Codigo = "1.02.04",
            Descripcion = "Alimentador TDF-1",
            Unidad = "m",
            Cantidad = 45.5m,
            EsTitulo = false,
            BadgeText = "4 recursos",
            Capitulo = "1.0 ALIMENTADORES"
        };

        Assert.Equal("1.02.04  Alimentador TDF-1", item.DisplayLabel);
        Assert.Equal($"{45.5m:N2} m", item.CantidadUnidadText);
        Assert.True(item.HasBadge);
        Assert.True(item.HasCapitulo);
    }

    [Fact]
    public void QuickJumpPartidaItem_ForSectionTitle_HasNoQuantity()
    {
        var item = new QuickJumpPartidaItem
        {
            Id = "T1",
            Codigo = "1.0",
            Descripcion = "ALIMENTADORES Y FUERZA",
            EsTitulo = true
        };

        Assert.Equal("1.0  ALIMENTADORES Y FUERZA", item.DisplayLabel);
        Assert.Equal(string.Empty, item.CantidadUnidadText);
        Assert.False(item.HasBadge);
        Assert.False(item.HasCapitulo);
    }

    [Fact]
    public void CubicacionViewModel_HasQuickJumpCommand()
    {
        var thread = new Thread(() =>
        {
            var host = WpfAppHost.Current;
            var path = Path.Combine(Path.GetTempPath(), $"quickjump-cub-{Guid.NewGuid():N}.qce");
            host.Session.NewProject("quickjump-cub");
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P1",
                Item = "1.1",
                Descripcion = "Canalización EMT",
                Unidad = "m",
                Cantidad = 100m
            });
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P2",
                Item = "1.2",
                Descripcion = "Conductor Eva 2.5",
                Unidad = "m",
                Cantidad = 300m
            });

            var vm = new CubicacionPageVm(new FakeShell(), host);

            Assert.NotNull(vm.OpenQuickJumpCommand);
            Assert.True(vm.OpenQuickJumpCommand.CanExecute(null));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ApuWorkspaceVm_HasQuickJumpCommand()
    {
        var thread = new Thread(() =>
        {
            var host = WpfAppHost.Current;
            var path = Path.Combine(Path.GetTempPath(), $"quickjump-apu-{Guid.NewGuid():N}.qce");
            host.Session.NewProject("quickjump-apu");
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P1",
                Item = "2.1",
                Descripcion = "Tablero General Alumbrado",
                Unidad = "un",
                Cantidad = 1m
            });

            var vm = new ApuWorkspaceVm(new FakeShell(), host);

            Assert.NotNull(vm.OpenQuickJumpCommand);
            Assert.True(vm.OpenQuickJumpCommand.CanExecute(null));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
