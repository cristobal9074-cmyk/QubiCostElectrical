using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class TakeoffEstimationAcceleratorsTests
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

    public TakeoffEstimationAcceleratorsTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void PartidaListItemVm_IsTagged_TogglesCorrectly()
    {
        var row = new ItemizadoRow
        {
            Id = "P1",
            Item = "1.01",
            Descripcion = "Enchufe doble 10A",
            Unidad = "un",
            Cantidad = 25m
        };

        var vm = new PartidaListItemVm(row);
        Assert.False(vm.IsTagged);

        vm.IsTagged = true;
        Assert.True(vm.IsTagged);

        vm.IsTagged = false;
        Assert.False(vm.IsTagged);
    }

    [Fact]
    public async Task Cubicacion_EditPartidaCodigoAndDescripcion_PropagatesToItemizado()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"accel-partida-edit-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("accel-partida-edit");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.01",
            Descripcion = "Arranque Malla",
            Unidad = "un",
            Cantidad = 10m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.Equal("1.01", vm.PartidaCodigo);
        Assert.Equal("Arranque Malla", vm.PartidaDescripcion);

        // Edit Item code
        vm.PartidaCodigo = "1.02";
        Assert.Equal("1.02", vm.PartidaCodigo);
        Assert.Equal("1.02", host.Session.Itemizado.Single(x => x.Id == "P1").Item);

        // Edit Description
        vm.PartidaDescripcion = "Arranque Malla Modificado";
        Assert.Equal("Arranque Malla Modificado", vm.PartidaDescripcion);
        Assert.Equal("Arranque Malla Modificado", host.Session.Itemizado.Single(x => x.Id == "P1").Descripcion);
    }

    [Fact]
    public async Task AutocompleteService_FuzzyMultiTokenAndProjectPriority_Works()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"accel-auto-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("accel-auto");

        // Material en catálogo pero no usado
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "CND-EVA-25",
            Descripcion = "Conductor H07Z1-K (EVA) 2.5 mm2 Verde",
            Unidad = "m",
            MatClp = 450m
        });

        // Material usado en el proyecto
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "CND-EVA-15",
            Descripcion = "Conductor H07Z1-K (EVA) 1.5 mm2 Rojo",
            Unidad = "m",
            MatClp = 320m
        });

        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "P1",
            Codigo = "CND-EVA-15",
            Descripcion = "Conductor H07Z1-K (EVA) 1.5 mm2 Rojo",
            Unidad = "m",
            Cantidad = 100m
        });

        await host.ProjectWorkspace.SaveAsAsync(path);

        var pageVm = new CubicacionPageVm(new FakeShell(), host);
        var autoService = new CubicacionAutocompleteService(
            pageVm,
            host.Session,
            new MaterialSearchService(),
            new InMemoryPriceBankStore());

        // Búsqueda multi-token desordenada
        await autoService.SearchMaterialsAsync("eva cnd");

        Assert.True(pageVm.AutocompleteVisible);
        Assert.NotEmpty(pageVm.AutocompleteResults);

        // El material en uso en el proyecto (CND-EVA-15) debe tener prioridad o aparecer primero
        var first = pageVm.AutocompleteResults[0];
        Assert.Equal("CND-EVA-15", first.Codigo);
        Assert.Equal("En proyecto", first.MatchReason);
    }

    [Fact]
    public async Task ApuWorkspace_UnifiedResources_CalculatesSubtotalsCorrectly()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"accel-apu-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("accel-apu");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.0",
            Descripcion = "Tablero General",
            Unidad = "un",
            Cantidad = 1m
        });

        var apu = new ApuRow
        {
            Id = "APU-1",
            ItemId = "P1",
            Unidad = "un",
            Recursos = new List<ApuRecursoRow>
            {
                new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = "MAT-1", Descripcion = "Gabinete Metálico", Unidad = "un", Rendimiento = 1m, PrecioUf = 5.0m },
                new() { Tipo = ApuTipoRecurso.MO, CodigoMaterial = "MO-1", Descripcion = "Maestro Electricista", Unidad = "dia", Rendimiento = 0.5m, PrecioUf = 2.0m },
                new() { Tipo = ApuTipoRecurso.EQ, CodigoMaterial = "EQ-1", Descripcion = "Herramientas Especiales", Unidad = "un", Rendimiento = 1m, PrecioUf = 0.5m }
            }
        };
        host.Session.Apus.Add(apu);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new ApuWorkspaceVm(new FakeShell(), host);
        vm.SelectedPartida = vm.Partidas.FirstOrDefault(p => p.ItemId == "P1");

        Assert.True(vm.TieneApu);
        Assert.Equal(3, vm.Recursos.Count);
        Assert.True(vm.CostoMat > 0m);
        Assert.True(vm.CostoMoTotal > 0m);
        Assert.True(vm.CostoEquipo > 0m);
        Assert.True(vm.CostoDirecto > 0m);
    }

    [Fact]
    public void QuantityFormulaService_FastFormulasWithPlusAndDollar_EvaluatesProperly()
    {
        // Leading '+' from numeric keypad
        var r1 = QuantityFormulaService.Evaluate("+15+20*7");
        Assert.True(r1.Success);
        Assert.Equal(155m, r1.Value);

        // Leading '=+' and Excel absolute reference '$' symbols
        var r2 = QuantityFormulaService.Evaluate("+$D$4+6+15+15+15*7", new Dictionary<string, decimal> { ["D4"] = 10m });
        Assert.True(r2.Success);
        Assert.Equal(151m, r2.Value);

        // Standard formula like check
        Assert.True(QuantityFormulaService.IsFormulaLike("+15+20*7"));
        Assert.True(QuantityFormulaService.IsFormulaLike("=D4*2"));
    }

    [Fact]
    public async Task CubicacionViewModel_ToggleSectorMatrixAndFillActions_Work()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"accel-toggle-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("accel-toggle");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.01",
            Descripcion = "Alimentador Fuerza",
            Unidad = "ml",
            Cantidad = 50m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        // Alt+A toggles IsSectorMatrixMode
        bool initialMode = vm.IsSectorMatrixMode;
        bool handled = vm.TryExecuteShortcut(CubicacionShortcutCatalog.SectorMatrix);
        Assert.True(handled);
        Assert.NotEqual(initialMode, vm.IsSectorMatrixMode);

        bool fillDownTriggered = false;
        vm.RequestFillDown += () => fillDownTriggered = true;
        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.FillDown));
        Assert.True(fillDownTriggered);

        bool fillRightTriggered = false;
        vm.RequestFillRight += () => fillRightTriggered = true;
        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.FillRight));
        Assert.True(fillRightTriggered);

        bool repeatTriggered = false;
        vm.RequestRepeatMeasurement += () => repeatTriggered = true;
        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.RepeatMeasurement));
        Assert.True(repeatTriggered);
    }

    [Fact]
    public void ObservationMemoryStore_RememberAndSuggestions_Work()
    {
        var prefs = new UserPreferences();
        var initial = ObservationMemoryStore.GetSuggestions(prefs);
        Assert.Contains("Por compañía eléctrica", initial);
        Assert.Contains("Suministrado por Mandante", initial);

        // Remember custom observation
        ObservationMemoryStore.Remember(prefs, "Cámara de paso provista por contratista civil");
        var updated = ObservationMemoryStore.GetSuggestions(prefs, "Cámara");
        Assert.Contains("Cámara de paso provista por contratista civil", updated);
    }

    [Fact]
    public async Task CubicacionViewModel_ObservationChipsAndOfferFallback_Work()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"accel-obs-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("accel-obs");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.01",
            Descripcion = "Medidor Trifásico",
            Unidad = "un",
            Cantidad = 1m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        // Apply quick chip
        vm.ApplyObservationChip("Por compañía eléctrica");
        Assert.Equal("Por compañía eléctrica", vm.MotivoNoCubicarText);
        Assert.True(vm.HasPartidaNote);

        // Verify OfferClienteRowBuilder fallback
        var offerRows = OfferClienteRowBuilder.Build(
            host.Session.Itemizado,
            new CostBreakdown(),
            ventaNetaUf: 0m,
            fallbackProjectTitle: "Test Project");

        var p1Offer = offerRows.Single(r => r.Item == "1.01");
        Assert.Equal("Por compañía eléctrica", p1Offer.Observaciones);

        // Clear observation
        vm.ClearObservation();
        Assert.Equal(string.Empty, vm.MotivoNoCubicarText);
        Assert.False(vm.HasPartidaNote);
    }

    private sealed class InMemoryPriceBankStore : IPriceBankStore
    {
        public IReadOnlyList<PriceBankEntry> GetAll() => Array.Empty<PriceBankEntry>();
        public PriceBankEntry? GetByCodigo(string codigo) => null;
        public IReadOnlyList<PriceBankEntry> Search(string text) => Array.Empty<PriceBankEntry>();
        public void Upsert(PriceBankEntry entry) { }
        public bool Delete(string codigo) => true;
        public int DeleteAll() => 0;
        public int UpsertMany(IEnumerable<PriceBankEntry> entries) => 0;
        public int Count() => 0;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries) => 0;
    }
}
