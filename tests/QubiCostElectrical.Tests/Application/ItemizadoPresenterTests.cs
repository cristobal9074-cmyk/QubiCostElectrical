using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoPresenterTests
{
    private static ItemizadoPresenter CreatePresenter(BudgetProjectSession session)
        => ItemizadoPresenterFactory.CreatePresenter(new AppServicesBuilder().Build(), session);

    [Fact]
    public void CommitReplace_clears_cubicacion_and_qty_headers()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.1", Item = "1.1", Descripcion = "Vieja" });
        session.Cubicacion.Add(new CubicacionRow { ItemId = "1.1", Codigo = "MAT" });
        session.QtyHeadersByItem["1.1"] = new[] { "A", "B", "C" };

        var presenter = CreatePresenter(session);
        presenter.CommitReplace(new[]
        {
            new ItemizadoRow { Id = "2.1", Item = "2.1", Descripcion = "Nueva" }
        });

        Assert.Single(session.Itemizado);
        Assert.Equal("Nueva", session.Itemizado[0].Descripcion);
        Assert.Empty(session.Cubicacion);
        Assert.Empty(session.QtyHeadersByItem);
    }

    [Fact]
    public void ValidateCubicacionNavigation_rejects_title_rows()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Capítulo", EsTitulo = true });

        var presenter = CreatePresenter(session);
        Assert.NotNull(presenter.ValidateCubicacionNavigation("1.0"));
        Assert.Null(presenter.TryBuildCubicacionNavigation("1.0"));
    }

    [Fact]
    public void ValidateCubicacionNavigation_rejects_excluded_partida()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Partida excluida",
            ExcluidoCubicacion = true
        });

        var presenter = CreatePresenter(session);
        Assert.NotNull(presenter.ValidateCubicacionNavigation("P1"));
        Assert.Null(presenter.TryBuildCubicacionNavigation("P1"));
    }

    [Fact]
    public void TryBuildImportPlanFromTable_returns_error_for_invalid_headers()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        var presenter = CreatePresenter(session);

        var (plan, error) = presenter.TryBuildImportPlanFromTable(new List<IReadOnlyList<string>>
        {
            new[] { "FOO", "BAR" }
        });

        Assert.Null(plan);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void BuildGridState_uses_partida_completion_service()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.1", Item = "1.1", Descripcion = "Partida" });
        session.Cubicacion.Add(new CubicacionRow { ItemId = "1.1", Codigo = "X", Cantidad = 1m });

        var presenter = CreatePresenter(session);
        var state = presenter.BuildGridState();

        Assert.Single(state.Rows);
        Assert.Equal(1, state.Completion.TotalPartidas);
    }

    [Fact]
    public void DuplicateAt_creates_copy_below_source()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Cap", EsTitulo = true });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "Partida", Unidad = "UN", Cantidad = 2m });

        var presenter = CreatePresenter(session);
        var result = presenter.DuplicateAt(1);

        Assert.Equal(3, session.Itemizado.Count);
        Assert.Equal(2, result.SelectedIndex);
        Assert.Contains("(copia)", session.Itemizado[2].Descripcion);
        Assert.NotEqual(session.Itemizado[1].Item, session.Itemizado[2].Item);
    }

    [Fact]
    public void DeleteAt_removes_orphan_apus()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "Partida" });
        session.Apus.Add(new ApuRow { ItemId = "1.0.00001", Descripcion = "APU" });

        var presenter = CreatePresenter(session);
        presenter.DeleteAt(0);

        Assert.Empty(session.Itemizado);
        Assert.Empty(session.Apus);
    }

    [Fact]
    public void ClearAll_removes_all_apus()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "Partida" });
        session.Apus.Add(new ApuRow { ItemId = "1.0.00001" });

        var presenter = CreatePresenter(session);
        presenter.ClearAll();

        Assert.Empty(session.Itemizado);
        Assert.Empty(session.Apus);
    }

    [Fact]
    public void CommitReplace_clears_apus()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.1", Item = "1.1", Descripcion = "Vieja" });
        session.Apus.Add(new ApuRow { ItemId = "1.1" });

        var presenter = CreatePresenter(session);
        presenter.CommitReplace(new[] { new ItemizadoRow { Id = "2.1", Item = "2.1", Descripcion = "Nueva" } });

        Assert.Empty(session.Apus);
    }

    [Fact]
    public void DeleteAt_title_block_removes_orphan_apus()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Capítulo", EsTitulo = true });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00001", Item = "1.1", Descripcion = "Partida A" });
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0.00002", Item = "1.2", Descripcion = "Partida B" });
        session.Apus.Add(new ApuRow { ItemId = "1.0.00001" });
        session.Apus.Add(new ApuRow { ItemId = "1.0.00002" });

        var presenter = CreatePresenter(session);
        presenter.DeleteAt(0);

        Assert.Empty(session.Itemizado);
        Assert.Empty(session.Apus);
    }

    [Fact]
    public void DeleteAt_preserves_normalized_layers_for_other_partidas()
    {
        var session = new BudgetProjectSession();
        session.NewProject("test", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida A" });
        session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Partida B" });
        var row1 = new CubicacionRow { Id = "R1", ItemId = "P1", Codigo = "A", Cantidad = 1m };
        var row2 = new CubicacionRow { Id = "R2", ItemId = "P2", Codigo = "B", Cantidad = 2m };
        session.Cubicacion.Add(row1);
        session.Cubicacion.Add(row2);
        // La capa normalizada (CubicacionRecursos) no se espeja automáticamente al agregar a
        // Cubicacion: se sincroniza explícitamente por fila, igual que hace la UI real en cada
        // edición (ver CubicacionPresenter.cs y CubicacionViewModel.Sectors.cs).
        session.SyncCubicacionRowComposition(row1);
        session.SyncCubicacionRowComposition(row2);
        Assert.Equal(2, session.CubicacionRecursos.Count);

        var presenter = CreatePresenter(session);
        presenter.DeleteAt(0);

        Assert.Single(session.Itemizado);
        Assert.Equal("P2", session.Itemizado[0].Id);
        Assert.DoesNotContain(session.Cubicacion, r => r.ItemId == "P1");
        Assert.Single(session.CubicacionRecursos);
        Assert.Equal("R2", session.CubicacionRecursos[0].Id);
    }
}
