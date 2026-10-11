using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CommandPalette;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CommandPaletteEngineTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "Proyecto Test";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
        public void RefreshCurrentPage() { }
    }

    private sealed class FakePriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> items = new();

        public FakePriceBankStore(IEnumerable<PriceBankEntry>? initial = null)
        {
            if (initial != null) items.AddRange(initial);
        }

        public IReadOnlyList<PriceBankEntry> GetAll() => items;
        public PriceBankEntry? GetByCodigo(string codigo) => items.FirstOrDefault(i => i.Codigo == codigo);
        public IReadOnlyList<PriceBankEntry> Search(string text) => items.Where(i => i.Descripcion.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        public void Upsert(PriceBankEntry entry) => items.Add(entry);
        public int UpsertMany(IEnumerable<PriceBankEntry> entries) { var list = entries.ToList(); items.AddRange(list); return list.Count; }
        public bool Delete(string codigo) => items.RemoveAll(i => i.Codigo == codigo) > 0;
        public int DeleteAll() { var c = items.Count; items.Clear(); return c; }
        public int Count() => items.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries) => 0;
    }

    private sealed class FakeWorkspaceService : IProjectWorkspaceService
    {
        public FakeWorkspaceService(BudgetProjectSession? session = null) => Session = session ?? new BudgetProjectSession();
        public BudgetProjectSession Session { get; }
        public bool HasOpenProject => !string.IsNullOrWhiteSpace(Session.CurrentProjectPath);
        public Task OpenAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public BudgetValidationResult ValidateCurrent() => new();
        public void DiscardChanges() { }
    }

    public CommandPaletteEngineTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void FuzzyMatcher_DirectMatch_ScoresHigh()
    {
        var score = FuzzyMatcher.Score("Tablero General Fuerza y Alumbrado", "tablero");
        Assert.True(score >= 800, $"Score should be high, was {score}");
    }

    [Fact]
    public void FuzzyMatcher_WordBoundary_ScoresHigherThanRandom()
    {
        var scoreBoundary = FuzzyMatcher.Score("Cable Libre Halogeno", "Libre");
        var scoreSubseq = FuzzyMatcher.Score("Alimentador General", "adr");
        Assert.True(scoreBoundary > scoreSubseq);
    }

    [Fact]
    public void FuzzyMatcher_EmptyOrUnrelated_ScoresExpected()
    {
        Assert.Equal(100, FuzzyMatcher.Score("Tablero General", ""));
        Assert.Equal(0, FuzzyMatcher.Score("Tablero General", "xyz123"));
    }

    [Fact]
    public async Task NaturalLanguage_Margen14Percent_AppliesUtilidad()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Edificio Los Andes";
        session.ProjectInfo.UtilidadPct = 10m;
        var shell = new FakeShell();

        string? toast = null;
        var actions = NaturalLanguageActionParser.Parse("margen 14%", session, shell, t => toast = t).ToList();

        Assert.NotEmpty(actions);
        var hero = actions.First();
        Assert.True(hero.IsHeroAction);
        Assert.Contains("14", hero.Title);

        await hero.ExecuteAsync();

        Assert.Equal(14m, session.ProjectInfo.UtilidadPct);
        Assert.NotNull(toast);
        Assert.Contains("utilidad actualizado", toast, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NaturalLanguage_GastosGenerales10Percent_AppliesGg()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Torre Norte";
        session.ProjectInfo.GastosGeneralesPct = 5m;
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("gastos generales 10%", session, shell).ToList();
        Assert.NotEmpty(actions);

        await actions.First().ExecuteAsync();
        Assert.Equal(10m, session.ProjectInfo.GastosGeneralesPct);
    }

    [Fact]
    public async Task NaturalLanguage_DescuentoYRisk_ParseCorrectly()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Proyecto Mall";
        var shell = new FakeShell();

        var descAction = NaturalLanguageActionParser.Parse("descuento 3.5%", session, shell).FirstOrDefault();
        Assert.NotNull(descAction);
        await descAction.ExecuteAsync();
        Assert.Equal(3.5m, session.ProjectInfo.DescuentoPct);

        var riesgoAction = NaturalLanguageActionParser.Parse("riesgo 4%", session, shell).FirstOrDefault();
        Assert.NotNull(riesgoAction);
        await riesgoAction.ExecuteAsync();
        Assert.Equal(4m, session.ProjectInfo.RiesgoPct);
    }

    [Fact]
    public async Task NaturalLanguage_IrATablero_NavigatesToCubicacionSc()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Hospital Regional";
        session.CubicacionSC.Add(new CuadroCargaSCRow { Nombre = "TDF-1", Descripcion = "Tablero Distribución Fuerza Piso 1" });
        session.CubicacionSC.Add(new CuadroCargaSCRow { Nombre = "TDA-2", Descripcion = "Tablero Distribución Alumbrado Piso 2" });
        var shell = new FakeShell();

        var items = NaturalLanguageActionParser.Parse("ir a tablero tdf-1", session, shell).ToList();
        Assert.NotEmpty(items);

        var jumpItem = items.First();
        Assert.Contains("TDF-1", jumpItem.Title);

        await jumpItem.ExecuteAsync();
        Assert.Equal(ShellRoute.CubicacionSc, shell.CurrentRoute);
    }

    [Fact]
    public async Task NaturalLanguage_RevisarPartidasEnCero_NavigatesToOfferDocuments()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Planta Solar";
        var shell = new FakeShell();

        var items = NaturalLanguageActionParser.Parse("revisar partidas en $0", session, shell).ToList();
        Assert.NotEmpty(items);

        var reviewItem = items.First();
        Assert.Contains("Revisar Partidas", reviewItem.Title);

        await reviewItem.ExecuteAsync();
        Assert.Equal(ShellRoute.OfferDocuments, shell.CurrentRoute);
    }

    [Fact]
    public void NaturalLanguage_ReadOnlySession_DoesNotYieldFinancialMutations()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Proyecto Bloqueado";
        session.SetRoleReadOnly(true, "Solo lectura");
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("margen 15%", session, shell).ToList();
        Assert.Empty(actions);

        var ggActions = NaturalLanguageActionParser.Parse("gg 8%", session, shell).ToList();
        Assert.Empty(ggActions);
    }

    [Fact]
    public void CommandPaletteEngine_EmptyQuery_ReturnsStaticNavigation()
    {
        var session = new BudgetProjectSession();
        var shell = new FakeShell();
        var priceBank = new FakePriceBankStore();
        var ws = new FakeWorkspaceService();
        var undo = new WpfUndoService();

        var engine = new CommandPaletteEngine(session, shell, priceBank, ws, undo);
        var items = engine.Search(string.Empty);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Title.Contains("Dashboard"));
        Assert.Contains(items, i => i.Title.Contains("Itemizado Contractual"));
        Assert.Contains(items, i => i.Title.Contains("Cubicación SC"));
        Assert.Contains(items, i => i.Title.Contains("Estructura de Costos"));
    }

    [Fact]
    public void CommandPaletteEngine_CategoryFiltering_RestrictsResults()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Test Project";
        session.Itemizado.Add(new ItemizadoRow { Item = "1.1", Descripcion = "Canalización embutida tubería 20mm" });

        var shell = new FakeShell();
        var priceBank = new FakePriceBankStore(new[]
        {
            new PriceBankEntry { Codigo = "CAB-01", Descripcion = "Cable Libre Halógeno 2.5mm", MatClp = 1200m }
        });
        var ws = new FakeWorkspaceService();
        var undo = new WpfUndoService();

        var engine = new CommandPaletteEngine(session, shell, priceBank, ws, undo);

        // Search with Partidas category only
        var partidaResults = engine.Search("tubería", CommandPaletteCategory.Partidas);
        Assert.All(partidaResults, r => Assert.Equal(CommandPaletteCategory.Partidas, r.Category));

        // Search with Materiales category only
        var materialResults = engine.Search("cable", CommandPaletteCategory.Materiales);
        Assert.All(materialResults, r => Assert.Equal(CommandPaletteCategory.Materiales, r.Category));
    }

    [Fact]
    public async Task CommandPaletteEngine_SearchCubicacionAcrossPartidas_FindsResourceAndNavigatesToTakeoff()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Edificio Costanera";
        var partida = new ItemizadoRow { Id = "partida-123", Item = "2.1", Descripcion = "Circuitos de Enchufes" };
        session.Itemizado.Add(partida);

        var cubicacionRow = new CubicacionRow
        {
            Id = "cub-row-99",
            ItemId = "partida-123",
            Codigo = "CAB-EVA-4",
            Descripcion = "Conductor EVA 4mm Libre Halógeno",
            Cantidad = 250m,
            Unidad = "m",
            MatClp = 1850m
        };
        session.Cubicacion.Add(cubicacionRow);

        var shell = new FakeShell();
        var priceBank = new FakePriceBankStore();
        var ws = new FakeWorkspaceService(session);
        var undo = new WpfUndoService();

        var engine = new CommandPaletteEngine(session, shell, priceBank, ws, undo);
        var results = engine.Search("EVA 4mm");

        Assert.NotEmpty(results);
        var match = results.FirstOrDefault(r => r.Category == CommandPaletteCategory.Cubicacion);
        Assert.NotNull(match);
        Assert.Contains("CAB-EVA-4", match.Title);
        Assert.Contains("Partida 2.1", match.Subtitle);
        Assert.Contains("250", match.Subtitle);

        await match.ExecuteAsync();

        Assert.Equal(ShellRoute.Takeoff, shell.CurrentRoute);
        Assert.NotNull(session.PendingTakeoffNavigation);
        Assert.Equal("partida-123", session.PendingTakeoffNavigation.ItemId);
        Assert.Equal("2.1", session.PendingTakeoffNavigation.ItemCode);
        Assert.Equal("cub-row-99", session.PendingTakeoffNavigation.TargetRowId);
        Assert.Equal("CAB-EVA-4", session.PendingTakeoffNavigation.TargetCode);
    }

    [Fact]
    public async Task CommandPaletteEngine_SearchApuAcrossPartidas_FindsApuResourceAndNavigatesToApu()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Centro de Distribución";
        var partida = new ItemizadoRow { Id = "partida-456", Item = "3.2", Descripcion = "Luminarias Industriales" };
        session.Itemizado.Add(partida);

        var apu = new ApuRow
        {
            Id = "apu-456",
            ItemId = "partida-456",
            Descripcion = "Luminarias Industriales"
        };
        apu.Recursos.Add(new ApuRecursoRow
        {
            CodigoMaterial = "CAMP-LED-150W",
            Descripcion = "Campana LED High Bay 150W IP65",
            Unidad = "un",
            Rendimiento = 1m,
            PrecioUf = 2.45m
        });
        session.Apus.Add(apu);

        var shell = new FakeShell();
        var priceBank = new FakePriceBankStore();
        var ws = new FakeWorkspaceService(session);
        var undo = new WpfUndoService();

        var engine = new CommandPaletteEngine(session, shell, priceBank, ws, undo);
        var results = engine.Search("Campana LED");

        Assert.NotEmpty(results);
        var match = results.FirstOrDefault(r => r.Category == CommandPaletteCategory.Apu);
        Assert.NotNull(match);
        Assert.Contains("CAMP-LED-150W", match.Title);
        Assert.Contains("Partida 3.2", match.Subtitle);

        await match.ExecuteAsync();

        Assert.Equal(ShellRoute.Apu, shell.CurrentRoute);
        Assert.NotNull(shell.LastContext);
        var apuCtx = Assert.IsType<QubiCostElectrical.Wpf.Features.Apu.ApuNavContext>(shell.LastContext);
        Assert.Equal("partida-456", apuCtx.ItemId);
        Assert.Equal("CAMP-LED-150W", apuCtx.CodigoItem);
    }

    [Fact]
    public void NaturalLanguage_CargarPlantillaIndustrial_YieldsHeroAction()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Bodega Central";
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("cargar plantilla industrial", session, shell).ToList();
        Assert.NotEmpty(actions);

        var templateAction = actions.FirstOrDefault(a => a.Title.Contains("Industrial", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(templateAction);
        Assert.True(templateAction.IsHeroAction);
        Assert.Equal("PLANTILLA RIC", templateAction.HeroBadge);
    }

    [Fact]
    public void NaturalLanguage_ReemplazarPvcPorLshf_YieldsHeroActionWithSourceAndTarget()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Centro de Datos";
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("reemplazar pvc por lshf", session, shell).ToList();
        Assert.NotEmpty(actions);

        var replaceAction = actions.FirstOrDefault(a => a.Title.Contains("PVC ➔ LSHF"));
        Assert.NotNull(replaceAction);
        Assert.True(replaceAction.IsHeroAction);
        Assert.Equal("REEMPLAZO MASIVO", replaceAction.HeroBadge);
    }

    [Fact]
    public async Task NaturalLanguage_FiltrarPendientes_NavigatesToItemizedWithIncompleteFilter()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Edificio Torre";
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("filtrar pendientes", session, shell).ToList();
        Assert.NotEmpty(actions);

        var filterAction = actions.First();
        Assert.Contains("Filtrar Partidas Pendientes", filterAction.Title);

        await filterAction.ExecuteAsync();
        Assert.Equal(ShellRoute.Itemized, shell.CurrentRoute);
        Assert.NotNull(shell.LastContext);
        var itemCtx = Assert.IsType<ItemizadoNavigationContext>(shell.LastContext);
        Assert.Equal(ItemizadoFilterMode.Incompletos, itemCtx.FilterMode);
    }

    [Fact]
    public async Task NaturalLanguage_ExportarOferta_NavigatesToOfferDocuments()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.Nombre = "Oficinas Providencia";
        var shell = new FakeShell();

        var actions = NaturalLanguageActionParser.Parse("exportar oferta a pdf", session, shell).ToList();
        Assert.NotEmpty(actions);

        var exportAction = actions.First();
        Assert.Contains("Exportar Dossier", exportAction.Title);

        await exportAction.ExecuteAsync();
        Assert.Equal(ShellRoute.OfferDocuments, shell.CurrentRoute);
    }

    [Fact]
    public void QuickJumpMultiEntity_ItemsIncludePartidasCapitulosTablerosCircuitos()
    {
        var itemPartida = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
        {
            Id = "part-1",
            Codigo = "1.2.1",
            Descripcion = "Interruptor Termomagnético",
            Kind = QubiCostElectrical.Wpf.Controls.QuickJumpItemKind.Partida
        };

        var itemCapitulo = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
        {
            Id = "cap-1",
            Codigo = "1.0",
            Descripcion = "ALIMENTADORES",
            Kind = QubiCostElectrical.Wpf.Controls.QuickJumpItemKind.Capitulo,
            EsTitulo = true
        };

        var itemTablero = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
        {
            Id = "tab-1",
            Codigo = "TDA",
            Descripcion = "Tablero Distribución Alumbrado",
            Kind = QubiCostElectrical.Wpf.Controls.QuickJumpItemKind.TableroSc
        };

        var itemCircuito = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
        {
            Id = "tab-1",
            SecondaryId = "cto-1",
            Codigo = "C1",
            Descripcion = "Alumbrado Pasillos",
            Kind = QubiCostElectrical.Wpf.Controls.QuickJumpItemKind.CircuitoSc
        };

        Assert.Equal("PARTIDA", itemPartida.CategoryBadgeText);
        Assert.Equal("CAPÍTULO", itemCapitulo.CategoryBadgeText);
        Assert.Equal("TABLERO SC", itemTablero.CategoryBadgeText);
        Assert.Equal("CIRCUITO", itemCircuito.CategoryBadgeText);
        Assert.True(itemCapitulo.IsSelectable);
        Assert.True(itemTablero.IsSelectable);
        Assert.True(itemCircuito.IsSelectable);
    }
}
