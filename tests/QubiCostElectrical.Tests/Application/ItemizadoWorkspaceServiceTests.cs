using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoWorkspaceServiceTests
{
    private static (ItemizadoPresenter Presenter, BudgetProjectSession Session) Create()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        var presenter = ItemizadoPresenterFactory.CreatePresenter(new AppServicesBuilder().Build(), session);
        return (presenter, session);
    }

    [Fact]
    public void BuildPageState_returns_empty_summary_for_new_project()
    {
        var (presenter, session) = Create();
        var workspace = new ItemizadoWorkspaceService();
        var state = workspace.BuildPageState(presenter, session);
        Assert.Empty(state.Rows);
        Assert.Equal(0, state.Summary.TotalPartidas);
    }

    [Fact]
    public void MatchesFilter_incompletos_hides_completed_partidas()
    {
        var workspace = new ItemizadoWorkspaceService();
        var completed = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "1",
            ParentId = "1.0",
            Item = "1",
            Descripcion = "A",
            Unidad = "UN",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 1,
            TotalUf = 1,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        };
        var incomplete = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "2",
            ParentId = "1.0",
            Item = "2",
            Descripcion = "B",
            Unidad = "UN",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 50,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Incompleto",
            StatusKind = "warn"
        };

        Assert.False(workspace.MatchesFilter(completed, ItemizadoFilterMode.Incompletos));
        Assert.True(workspace.MatchesFilter(incomplete, ItemizadoFilterMode.Incompletos));
    }

    [Fact]
    public void MatchesFilter_excluido_no_aparece_en_sin_cubicar()
    {
        var workspace = new ItemizadoWorkspaceService();
        var excluida = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "3",
            ParentId = "1.0",
            Item = "3",
            Descripcion = "Fuera de alcance",
            Unidad = "GL",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = true,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Excluida",
            StatusKind = "neutral"
        };

        Assert.False(workspace.MatchesFilter(excluida, ItemizadoFilterMode.SinCubicar));
        Assert.False(workspace.MatchesFilter(excluida, ItemizadoFilterMode.Incompletos));
        Assert.False(workspace.MatchesFilter(excluida, ItemizadoFilterMode.Completos));
        Assert.False(workspace.MatchesFilter(excluida, ItemizadoFilterMode.SinPrecio));
        Assert.True(workspace.MatchesFilter(excluida, ItemizadoFilterMode.Todos));
    }

    [Fact]
    public void BuildPageState_excluida_con_motivo_en_status()
    {
        var (presenter, session) = Create();
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1.1",
            Descripcion = "No proyectada",
            Unidad = "GL",
            Cantidad = 1,
            ExcluidoCubicacion = true,
            MotivoNoCubicar = "Fuera de alcance del contrato"
        });

        var workspace = new ItemizadoWorkspaceService();
        var state = workspace.BuildPageState(presenter, session);
        var row = Assert.Single(state.Rows, r => !r.EsTitulo);
        Assert.Equal("Excluida · Fuera de alcance del contra…", row.StatusLabel);
        Assert.Equal("neutral", row.StatusKind);
    }

    [Fact]
    public void MatchesSearch_finds_by_description()
    {
        var workspace = new ItemizadoWorkspaceService();
        var row = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "1",
            ParentId = "",
            Item = "2.1",
            Descripcion = "Canalización EMT",
            Unidad = "ML",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Sin cubicar",
            StatusKind = "info"
        };

        Assert.True(workspace.MatchesSearch(row, "canal"));
        Assert.False(workspace.MatchesSearch(row, "tablero"));
    }

    [Fact]
    public void BuildPageState_maps_rows_from_session()
    {
        var (presenter, session) = Create();
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Capítulo", EsTitulo = true });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1.1", Descripcion = "Partida", Unidad = "UN" });

        var workspace = new ItemizadoWorkspaceService();
        var state = workspace.BuildPageState(presenter, session);

        Assert.Equal(2, state.Rows.Count);
        Assert.Equal(1, state.Summary.TotalPartidas);
    }

    [Fact]
    public void ShouldDisplayInGrid_shows_chapter_when_child_matches_filter()
    {
        var workspace = new ItemizadoWorkspaceService();
        var chapter = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "1.0",
            ParentId = "",
            Item = "1",
            Descripcion = "Capítulo 1",
            Unidad = "",
            Cantidad = 0,
            EsTitulo = true,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Capítulo",
            StatusKind = "neutral"
        };
        var incomplete = new ItemizadoRowPresentation
        {
            SourceIndex = 1,
            Id = "1.0.00001",
            ParentId = "1.0",
            Item = "1.1",
            Descripcion = "Cable",
            Unidad = "M",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 50,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Incompleto",
            StatusKind = "warn"
        };
        var complete = new ItemizadoRowPresentation
        {
            SourceIndex = 2,
            Id = "1.0.00002",
            ParentId = "1.0",
            Item = "1.2",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 1,
            TotalUf = 1,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        };
        var all = new[] { chapter, incomplete, complete };

        Assert.False(workspace.ShouldDisplayInGrid(chapter, new[] { chapter, incomplete }, ItemizadoFilterMode.Completos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Completos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(incomplete, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.False(workspace.ShouldDisplayInGrid(complete, all, ItemizadoFilterMode.Incompletos, ""));
    }

    [Fact]
    public void ShouldDisplayInGrid_preserves_chapter_with_guid_ids_and_wbs_item_codes()
    {
        var workspace = new ItemizadoWorkspaceService();
        // Escenario del mundo real: IDs son GUIDs generados, ParentId está vacío
        var chapterGuid = Guid.NewGuid().ToString("N");
        var childGuid1 = Guid.NewGuid().ToString("N");
        var childGuid2 = Guid.NewGuid().ToString("N");

        var chapter = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = chapterGuid,
            ParentId = "",
            Item = "1",
            Descripcion = "ALUMBRADO",
            Unidad = "",
            Cantidad = 0,
            EsTitulo = true,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Capítulo",
            StatusKind = "neutral"
        };
        var child1 = new ItemizadoRowPresentation
        {
            SourceIndex = 1,
            Id = childGuid1,
            ParentId = "",
            Item = "1.1",
            Descripcion = "Focos LED",
            Unidad = "UN",
            Cantidad = 10,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 1.5m,
            TotalUf = 15m,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        };
        var child2 = new ItemizadoRowPresentation
        {
            SourceIndex = 2,
            Id = childGuid2,
            ParentId = "",
            Item = "1.2",
            Descripcion = "Interruptor",
            Unidad = "UN",
            Cantidad = 5,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 0,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Sin cubicar",
            StatusKind = "info"
        };

        var all = new[] { chapter, child1, child2 };

        // Al filtrar por Completos: el capítulo DEBE verse porque tiene al hijo 1.1 completo
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Completos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(child1, all, ItemizadoFilterMode.Completos, ""));
        Assert.False(workspace.ShouldDisplayInGrid(child2, all, ItemizadoFilterMode.Completos, ""));

        // Al filtrar por Incompletos: el capítulo DEBE verse porque tiene al hijo 1.2 pendiente
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.False(workspace.ShouldDisplayInGrid(child1, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(child2, all, ItemizadoFilterMode.Incompletos, ""));
    }

    [Fact]
    public void ShouldDisplayInGrid_shows_chapter_and_children_when_chapter_name_matches_search()
    {
        var workspace = new ItemizadoWorkspaceService();
        var chapter = new ItemizadoRowPresentation
        {
            SourceIndex = 0,
            Id = "c1",
            ParentId = "",
            Item = "1",
            Descripcion = "SUBESTACION ELECTRICA",
            Unidad = "",
            Cantidad = 0,
            EsTitulo = true,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0,
            UnitPriceUf = 0,
            TotalUf = 0,
            HasPrice = false,
            StatusLabel = "Capítulo",
            StatusKind = "neutral"
        };
        var child = new ItemizadoRowPresentation
        {
            SourceIndex = 1,
            Id = "p1",
            ParentId = "",
            Item = "1.1",
            Descripcion = "Transformador 500kVA",
            Unidad = "UN",
            Cantidad = 1,
            EsTitulo = false,
            ExcluidoCubicacion = false,
            CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100,
            UnitPriceUf = 100m,
            TotalUf = 100m,
            HasPrice = true,
            StatusLabel = "Completo",
            StatusKind = "ok"
        };

        var all = new[] { chapter, child };

        // Buscar "subestacion": tanto el capítulo como su partida deben aparecer
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Todos, "subestacion"));
        Assert.True(workspace.ShouldDisplayInGrid(child, all, ItemizadoFilterMode.Todos, "subestacion"));
    }

    [Fact]
    public void BuildPageState_calculates_chapter_subtotals_live()
    {
        var (presenter, session) = Create();
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1.0", Descripcion = "Obras Civiles", EsTitulo = true });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.1", Item = "1.1", Descripcion = "Excavación", Unidad = "M3", Cantidad = 10 });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.2", Item = "1.2", Descripcion = "Hormigón", Unidad = "M3", Cantidad = 5 });

        var cachedTotals = new Dictionary<string, decimal>
        {
            ["1.1"] = 12.5m,
            ["1.2"] = 7.5m
        };

        var workspace = new ItemizadoWorkspaceService();
        var state = workspace.BuildPageState(presenter, session, cachedTotals);

        var chapterRow = state.Rows[0];
        Assert.True(chapterRow.EsTitulo);
        Assert.Equal(20.0m, chapterRow.TotalUf);
        Assert.True(chapterRow.HasPrice);
    }

    [Theory]
    [InlineData("1.0", 1, 16)]
    [InlineData("1", 0, 0)]
    [InlineData("1.1", 1, 16)]
    [InlineData("1.1.1", 2, 32)]
    [InlineData("1.1.1.1", 3, 48)]
    public void ItemizadoRowVm_WbsLevel_calculates_correct_indentation(string item, int expectedLevel, int expectedLeftMargin)
    {
        var row = new ItemizadoRow { Item = item, Descripcion = "Test" };
        var vm = new QubiCostElectrical.Wpf.Features.Itemizado.ItemizadoRowVm(row);

        Assert.Equal(expectedLevel, vm.WbsLevel);
        Assert.Equal(expectedLeftMargin, vm.IndentMargin.Left);
    }
}
