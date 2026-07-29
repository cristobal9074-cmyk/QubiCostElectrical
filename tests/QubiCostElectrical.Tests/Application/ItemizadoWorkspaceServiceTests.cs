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
            SourceIndex = 0, Id = "1", ParentId = "1.0", Item = "1", Descripcion = "A", Unidad = "UN", Cantidad = 1,
            EsTitulo = false, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100, UnitPriceUf = 1, TotalUf = 1, HasPrice = true, StatusLabel = "Completo", StatusKind = "ok"
        };
        var incomplete = new ItemizadoRowPresentation
        {
            SourceIndex = 0, Id = "2", ParentId = "1.0", Item = "2", Descripcion = "B", Unidad = "UN", Cantidad = 1,
            EsTitulo = false, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 50, UnitPriceUf = 0, TotalUf = 0, HasPrice = false, StatusLabel = "Incompleto", StatusKind = "warn"
        };

        Assert.False(workspace.MatchesFilter(completed, ItemizadoFilterMode.Incompletos));
        Assert.True(workspace.MatchesFilter(incomplete, ItemizadoFilterMode.Incompletos));
    }

    [Fact]
    public void MatchesSearch_finds_by_description()
    {
        var workspace = new ItemizadoWorkspaceService();
        var row = new ItemizadoRowPresentation
        {
            SourceIndex = 0, Id = "1", ParentId = "", Item = "2.1", Descripcion = "Canalización EMT", Unidad = "ML", Cantidad = 1,
            EsTitulo = false, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0, UnitPriceUf = 0, TotalUf = 0, HasPrice = false, StatusLabel = "Sin cubicar", StatusKind = "info"
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
            SourceIndex = 0, Id = "1.0", ParentId = "", Item = "1", Descripcion = "Capítulo 1", Unidad = "", Cantidad = 0,
            EsTitulo = true, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Empty,
            CompletionPct = 0, UnitPriceUf = 0, TotalUf = 0, HasPrice = false, StatusLabel = "Capítulo", StatusKind = "neutral"
        };
        var incomplete = new ItemizadoRowPresentation
        {
            SourceIndex = 1, Id = "1.0.00001", ParentId = "1.0", Item = "1.1", Descripcion = "Cable", Unidad = "M", Cantidad = 1,
            EsTitulo = false, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Incomplete,
            CompletionPct = 50, UnitPriceUf = 0, TotalUf = 0, HasPrice = false, StatusLabel = "Incompleto", StatusKind = "warn"
        };
        var complete = new ItemizadoRowPresentation
        {
            SourceIndex = 2, Id = "1.0.00002", ParentId = "1.0", Item = "1.2", Descripcion = "Tablero", Unidad = "UN", Cantidad = 1,
            EsTitulo = false, ExcluidoCubicacion = false, CompletionState = PartidaCompletionState.Completed,
            CompletionPct = 100, UnitPriceUf = 1, TotalUf = 1, HasPrice = true, StatusLabel = "Completo", StatusKind = "ok"
        };
        var all = new[] { chapter, incomplete, complete };

        Assert.False(workspace.ShouldDisplayInGrid(chapter, new[] { chapter, incomplete }, ItemizadoFilterMode.Completos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Completos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(chapter, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.True(workspace.ShouldDisplayInGrid(incomplete, all, ItemizadoFilterMode.Incompletos, ""));
        Assert.False(workspace.ShouldDisplayInGrid(complete, all, ItemizadoFilterMode.Incompletos, ""));
    }
}
