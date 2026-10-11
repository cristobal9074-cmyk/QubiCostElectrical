using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ItemizadoBreadcrumbCoordinatorTests
{
    [Fact]
    public void UpdateBreadcrumb_WithParentIdChain_BuildsFullAncestry()
    {
        var cap1 = new ItemizadoRowVm(new ItemizadoRow { Id = "cap1", Item = "1", Descripcion = "OBRAS CIVILES", EsTitulo = true });
        var sub1 = new ItemizadoRowVm(new ItemizadoRow { Id = "sub1", ParentId = "cap1", Item = "1.2", Descripcion = "ALIMENTADORES", EsTitulo = true });
        var part1 = new ItemizadoRowVm(new ItemizadoRow { Id = "part1", ParentId = "sub1", Item = "1.2.4", Descripcion = "BANDEJAS PORTACONDUCTORES", EsTitulo = false });

        var allRows = new List<ItemizadoRowVm> { cap1, sub1, part1 };
        var dict = new Dictionary<string, ItemizadoRowVm>
        {
            ["cap1"] = cap1,
            ["sub1"] = sub1,
            ["part1"] = part1
        };

        ItemizadoRowVm? selected = null;
        var coordinator = new ItemizadoBreadcrumbCoordinator(row => selected = row);

        coordinator.UpdateBreadcrumb(part1, dict, allRows);

        Assert.Equal(3, coordinator.Nodes.Count);
        Assert.Equal("1 OBRAS CIVILES", coordinator.Nodes[0].DisplayText);
        Assert.False(coordinator.Nodes[0].IsCurrent);

        Assert.Equal("1.2 ALIMENTADORES", coordinator.Nodes[1].DisplayText);
        Assert.False(coordinator.Nodes[1].IsCurrent);

        Assert.Equal("1.2.4 BANDEJAS PORTACONDUCTORES", coordinator.Nodes[2].DisplayText);
        Assert.True(coordinator.Nodes[2].IsCurrent);
    }

    [Fact]
    public void UpdateBreadcrumb_WithWbsFallback_BuildsAncestryWithoutExplicitParentId()
    {
        // Partidas sin ParentId asignado (ej. importación plana)
        var cap = new ItemizadoRowVm(new ItemizadoRow { Id = "c1", Item = "1", Descripcion = "FUERZA Y CONTROL", EsTitulo = true });
        var sub = new ItemizadoRowVm(new ItemizadoRow { Id = "s1", Item = "1.1", Descripcion = "TABLEROS GENERALES", EsTitulo = true });
        var part = new ItemizadoRowVm(new ItemizadoRow { Id = "p1", Item = "1.1.1", Descripcion = "TGBT-01", EsTitulo = false });

        var allRows = new List<ItemizadoRowVm> { cap, sub, part };
        var dict = new Dictionary<string, ItemizadoRowVm>
        {
            ["c1"] = cap,
            ["s1"] = sub,
            ["p1"] = part
        };

        var coordinator = new ItemizadoBreadcrumbCoordinator(_ => { });

        coordinator.UpdateBreadcrumb(part, dict, allRows);

        Assert.Equal(3, coordinator.Nodes.Count);
        Assert.Equal("1 FUERZA Y CONTROL", coordinator.Nodes[0].DisplayText);
        Assert.Equal("1.1 TABLEROS GENERALES", coordinator.Nodes[1].DisplayText);
        Assert.Equal("1.1.1 TGBT-01", coordinator.Nodes[2].DisplayText);
    }

    [Fact]
    public void NavigateToNodeCommand_SelectsTargetRow()
    {
        var cap = new ItemizadoRowVm(new ItemizadoRow { Id = "cap1", Item = "1", Descripcion = "CAPITULO 1", EsTitulo = true });
        var part = new ItemizadoRowVm(new ItemizadoRow { Id = "p1", ParentId = "cap1", Item = "1.1", Descripcion = "PARTIDA 1", EsTitulo = false });

        var allRows = new List<ItemizadoRowVm> { cap, part };
        var dict = new Dictionary<string, ItemizadoRowVm> { ["cap1"] = cap, ["p1"] = part };

        ItemizadoRowVm? navigatedTarget = null;
        var coordinator = new ItemizadoBreadcrumbCoordinator(row => navigatedTarget = row);

        coordinator.UpdateBreadcrumb(part, dict, allRows);

        var ancestorNode = coordinator.Nodes[0];
        Assert.True(coordinator.NavigateToNodeCommand.CanExecute(ancestorNode));

        coordinator.NavigateToNodeCommand.Execute(ancestorNode);

        Assert.Same(cap, navigatedTarget);
    }

    [Fact]
    public void UpdateBreadcrumb_NullSelection_ClearsNodes()
    {
        var cap = new ItemizadoRowVm(new ItemizadoRow { Id = "cap1", Item = "1", Descripcion = "CAPITULO 1", EsTitulo = true });
        var allRows = new List<ItemizadoRowVm> { cap };
        var dict = new Dictionary<string, ItemizadoRowVm> { ["cap1"] = cap };

        var coordinator = new ItemizadoBreadcrumbCoordinator(_ => { });
        coordinator.UpdateBreadcrumb(cap, dict, allRows);
        Assert.Single(coordinator.Nodes);

        coordinator.UpdateBreadcrumb(null, dict, allRows);
        Assert.Empty(coordinator.Nodes);
        Assert.False(coordinator.HasNodes);
    }
}
