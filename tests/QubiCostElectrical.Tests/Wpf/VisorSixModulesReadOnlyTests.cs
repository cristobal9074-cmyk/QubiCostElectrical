using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Demo-proof Visor: un solo archivo que prueba que <c>SetRoleReadOnly(true)</c>
/// bloquea mutación en Itemizado, Cubicación, SC, APU, Costo y Oferta/Export.
/// Detalle SC adicional en <see cref="CubicacionScReadOnlyTests"/>.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class VisorSixModulesReadOnlyTests
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

    public VisorSixModulesReadOnlyTests() => WpfTestHost.EnsureInitialized();

    private static void OpenNamedProject(WpfAppHost host, string name)
    {
        host.Session.NewProject(name);
        host.Session.CurrentProjectPath = $"{name}.qce";
    }

    [Fact]
    public void Itemizado_ReadOnly_CannotAddAndRowSettersDoNotMutate()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-itemizado");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización original",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();
        var row = Assert.Single(vm.Rows, r => !r.EsTitulo);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            Assert.False(vm.AddPartidaCommand.CanExecute(null));

            row.Descripcion = "hackeado por visor";
            Assert.Equal("Canalización original", row.Descripcion);
            Assert.Equal("Canalización original", row.Model.Descripcion);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Cubicacion_ReadOnly_RowSettersDoNotMutate()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-cubicacion");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t12");

        var row = Assert.Single(vm.Rows, r => r.IsSessionBacked && r.HasContent);
        Assert.Equal("PVC20", row.Codigo);
        Assert.Equal(12m, row.Cantidad);
        Assert.Equal("PVC20", row.Source.Codigo);
        Assert.Equal(12m, row.Source.Cantidad);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            row.Codigo = "HACK99";
            row.Cantidad = 999m;

            Assert.Equal("PVC20", row.Codigo);
            Assert.Equal(12m, row.Cantidad);
            Assert.Equal("PVC20", row.Source.Codigo);
            Assert.Equal(12m, row.Source.Cantidad);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Sc_ReadOnly_CanEditFalse()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-sc");
        var sc = new CubicacionScViewModel(new FakeShell(), host);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            Assert.False(sc.CanEdit);
            Assert.False(sc.AddCircuitoCommand.CanExecute(null));
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Apu_ReadOnly_CannotAddAndMarkupsDoNotMutate()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-apu");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1",
            Descripcion = "Partida",
            Unidad = "GL",
            Cantidad = 1m
        });
        host.Session.Apus.Add(new ApuRow
        {
            ItemId = "P1",
            Descripcion = "Partida",
            Unidad = "GL",
            Recursos =
            [
                new ApuRecursoRow { Tipo = ApuTipoRecurso.MAT, Descripcion = "Cable", Rendimiento = 1m, PrecioUf = 1m }
            ]
        });

        var apu = new ApuWorkspaceVm(new FakeShell(), host);
        var originalMarkup = apu.MarkupConductoresText;
        host.Session.MarkSaved();
        Assert.False(host.Session.IsDirty);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            Assert.False(apu.CanEdit);
            Assert.False(apu.AddRecursoCommand.CanExecute(null));

            apu.MarkupConductoresText = "9,99";
            Assert.Equal(originalMarkup, apu.MarkupConductoresText);
            Assert.False(host.Session.IsDirty);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Costo_ReadOnly_CanEditFalseAndGoItemDoesNotMutate()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-costo");
        var costo = new CostoViewModel(new FakeShell(), host);
        costo.Refresh();
        Assert.True(costo.CanEdit);

        var item = costo.GastosOperacionales
            .SelectMany(c => c.Items)
            .FirstOrDefault();
        Assert.NotNull(item);
        var originalConcepto = item!.Concepto;

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");
            // CanEdit de Costo es campo cacheado; Refresh sincroniza tras cambio de rol.
            costo.Refresh();

            Assert.False(costo.CanEdit);

            item = costo.GastosOperacionales.SelectMany(c => c.Items).FirstOrDefault();
            Assert.NotNull(item);
            originalConcepto = item!.Concepto;

            item.Concepto = "concepto hackeado por visor";
            Assert.Equal(originalConcepto, item.Concepto);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void Export_ReadOnly_CannotSaveOfferSettings()
    {
        var host = WpfAppHost.Current;
        OpenNamedProject(host, "visor6-export");
        var export = new ExportViewModel(host, new FakeShell());

        Assert.True(export.HasOpenProject);
        Assert.True(export.SaveOfferSettingsCommand.CanExecute(null));

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");

            Assert.False(export.SaveOfferSettingsCommand.CanExecute(null));
            Assert.False(export.ExportOfferPdfCommand.CanExecute(null));
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }
}
