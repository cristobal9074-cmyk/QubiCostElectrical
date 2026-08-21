using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ShellWorkflowLogicTests
{
    [Theory]
    [InlineData(ShellRoute.Project, ShellProductionStep.Datos)]
    [InlineData(ShellRoute.Documents, ShellProductionStep.Datos)]
    [InlineData(ShellRoute.Itemized, ShellProductionStep.Itemizado)]
    [InlineData(ShellRoute.Takeoff, ShellProductionStep.Cubicacion)]
    [InlineData(ShellRoute.CubicacionSc, ShellProductionStep.Cubicacion)]
    [InlineData(ShellRoute.Prices, ShellProductionStep.Precios)]
    [InlineData(ShellRoute.Apu, ShellProductionStep.Precios)]
    [InlineData(ShellRoute.Review, ShellProductionStep.Revision)]
    [InlineData(ShellRoute.OfferDocuments, ShellProductionStep.Exportar)]
    [InlineData(ShellRoute.Dashboard, null)]
    [InlineData(ShellRoute.Preferences, null)]
    public void ResolveProductionStep_MapsCoreRoutes(ShellRoute route, ShellProductionStep? expected)
    {
        Assert.Equal(expected, ShellWorkflowLogic.ResolveProductionStep(route));
    }

    [Fact]
    public void BuildSteps_IncludesCubicacionScAuxiliaryStep()
    {
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { Nombre = "Edificio Norte" };

        var steps = ShellWorkflowLogic.BuildSteps(
            ShellRoute.Takeoff,
            review,
            project,
            Array.Empty<ItemizadoRow>(),
            Array.Empty<CuadroCargaSCRow>());

        var sc = Assert.Single(steps, s => s.Title == "C.SC");
        Assert.True(sc.IsAuxiliary);
        Assert.Equal(ShellRoute.CubicacionSc, sc.NavigateRoute);
    }

    [Fact]
    public void BuildSteps_MarksActiveStepFromCurrentRoute()
    {
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { Nombre = "Proyecto activo" };
        var itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P1", Item = "1", Descripcion = "Partida", EsTitulo = false }
        };

        var steps = ShellWorkflowLogic.BuildSteps(
            ShellRoute.Itemized,
            review,
            project,
            itemizado,
            Array.Empty<CuadroCargaSCRow>());

        Assert.True(steps.Single(s => s.Title == "Itemizado").IsActive);
        Assert.False(steps.Single(s => s.Title == "Datos").IsActive);
        Assert.True(steps.Single(s => s.Title == "Datos").IsComplete);
        Assert.True(steps.Single(s => s.Title == "Itemizado").IsComplete);
    }

    [Fact]
    public void BuildSteps_CubicacionScRoute_ActivatesAuxiliaryOnly()
    {
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { Nombre = "Proyecto SC" };

        var steps = ShellWorkflowLogic.BuildSteps(
            ShellRoute.CubicacionSc,
            review,
            project,
            Array.Empty<ItemizadoRow>(),
            Array.Empty<CuadroCargaSCRow>());

        var sc = steps.Single(s => s.Title == "C.SC");
        var cubicacion = steps.Single(s => s.Title == "Cubicación");

        Assert.True(sc.IsActive);
        Assert.False(cubicacion.IsActive);
    }

    [Fact]
    public void BuildSteps_ProductionLineOrder_MatchesPlan()
    {
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { Nombre = "Orden" };

        var steps = ShellWorkflowLogic.BuildSteps(
            ShellRoute.Project,
            review,
            project,
            Array.Empty<ItemizadoRow>(),
            Array.Empty<CuadroCargaSCRow>());

        var mainTitles = steps.Where(s => !s.IsAuxiliary).Select(s => s.Title).ToList();
        Assert.Equal(
            ["Datos", "Itemizado", "Cubicación", "Precios", "Revisión", "Exportar"],
            mainTitles);
    }
}
