using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Architecture;

public sealed class AppServicesCompositionTests
{
    [Fact]
    public void Build_default_produces_all_core_services()
    {
        var services = new AppServicesBuilder().Build();

        Assert.NotNull(services.BudgetCalculation);
        Assert.NotNull(services.Itemizado);
        Assert.NotNull(services.Cubicacion);
        Assert.NotNull(services.ProjectRepository);
        Assert.NotNull(services.PriceBankStore);
    }

    [Fact]
    public void CreateDefault_matches_builder_default()
    {
        var fromBuilder = new AppServicesBuilder().Build();
        var fromFactory = AppServices.CreateDefault();

        Assert.Equal(fromBuilder.Itemizado.GetType(), fromFactory.Itemizado.GetType());
        Assert.Equal(fromBuilder.Cubicacion.GetType(), fromFactory.Cubicacion.GetType());
    }

    [Fact]
    public void WithItemizado_override_is_used_by_page_services()
    {
        var custom = new ItemizadoService();
        var services = new AppServicesBuilder().WithItemizado(custom).Build();
        var pageServices = ItemizadoPageServices.From(services);

        Assert.Same(custom, pageServices.Itemizado);
        Assert.Same(custom, services.Itemizado);
    }

    [Fact]
    public void WithCubicacion_override_persists_in_services()
    {
        var custom = new CubicacionService();
        var services = new AppServicesBuilder().WithCubicacion(custom).Build();

        Assert.Same(custom, services.Cubicacion);
    }
}
