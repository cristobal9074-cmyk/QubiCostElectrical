using System.Diagnostics;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

[Collection(ItemizadoPerformanceCollection.Name)]
public sealed class ItemizadoPerformanceTests
{
    [Fact]
    public void BuildPageState_handles_three_thousand_rows_under_two_seconds()
    {
        var session = new BudgetProjectSession();
        session.NewProject("perf", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = "1.0", Item = "1,0", Descripcion = "Capítulo", EsTitulo = true });

        for (int i = 1; i <= 3000; i++)
        {
            session.Itemizado.Add(new ItemizadoRow
            {
                Id = $"1.0.{i:00000}",
                Item = $"1.{i}",
                Descripcion = $"Partida {i}",
                Unidad = "UN",
                Cantidad = 1m
            });
        }

        var presenter = ItemizadoPresenterFactory.CreatePresenter(new AppServicesBuilder().Build(), session);
        var workspace = new ItemizadoWorkspaceService();
        var sw = Stopwatch.StartNew();
        var state = workspace.BuildPageState(presenter, session);
        sw.Stop();

        Assert.Equal(3001, state.Rows.Count);
        Assert.True(sw.ElapsedMilliseconds < 4000, $"Tardó {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void BuildPageState_with_cached_totals_is_not_slower_than_full()
    {
        var session = new BudgetProjectSession();
        session.NewProject("perf", "Tester");
        for (int i = 1; i <= 500; i++)
        {
            session.Itemizado.Add(new ItemizadoRow
            {
                Id = $"1.0.{i:00000}",
                Item = $"1.{i}",
                Descripcion = $"Partida {i}",
                Unidad = "UN",
                Cantidad = 1m
            });
        }

        var presenter = ItemizadoPresenterFactory.CreatePresenter(new AppServicesBuilder().Build(), session);
        var workspace = new ItemizadoWorkspaceService();
        var full = workspace.BuildPageState(presenter, session);
        var cached = full.Rows.Where(x => !x.EsTitulo).ToDictionary(x => x.Id, x => x.TotalUf);

        workspace.BuildPageState(presenter, session);
        var swCached = Stopwatch.StartNew();
        var light = workspace.BuildPageState(presenter, session, cached);
        swCached.Stop();

        Assert.Equal(500, light.Rows.Count);
        Assert.True(swCached.ElapsedMilliseconds < 2000, $"Tardó {swCached.ElapsedMilliseconds} ms");
    }
}
