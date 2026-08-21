using Xunit;

namespace QubiCostElectrical.Tests.Application;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ItemizadoPerformanceCollection : ICollectionFixture<object?>
{
    public const string Name = "ItemizadoPerformance";
}
