using QubiCostElectrical.Wpf.Features.ManoDeObra;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class MoCargoNameNormalizerTests
{
    [Theory]
    [InlineData("e9e13026fee74936a6827d38a6911e7f", true)]
    [InlineData("ab568c7d3d754da5a047ea92543a2982", true)]
    [InlineData("e9e13026-fee7-4936-a682-7d38a6911e7f", true)]
    [InlineData("Maestro eléctrico", false)]
    [InlineData("D1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsGuidLike_DetectsHexIdentifiers(string? value, bool expected)
    {
        Assert.Equal(expected, MoCargoNameNormalizer.IsGuidLike(value));
    }

    [Fact]
    public void ResolveDirectaNombre_GuidRows_MapPositionallyToMaestroAyudante()
    {
        Assert.Equal("Maestro", MoCargoNameNormalizer.ResolveDirectaNombre("e9e13026fee74936a6827d38a6911e7f", 0));
        Assert.Equal("Ayudante", MoCargoNameNormalizer.ResolveDirectaNombre("ab568c7d3d754da5a047ea92543a2982", 1));
        Assert.Equal("Ayudante 2", MoCargoNameNormalizer.ResolveDirectaNombre("ab568c7d3d754da5a047ea92543a2982", 2));
    }

    [Fact]
    public void ResolveDirectaNombre_KeepsRealNamesAndEmptyValues()
    {
        Assert.Equal("Maestro eléctrico", MoCargoNameNormalizer.ResolveDirectaNombre("Maestro eléctrico", 0));
        Assert.Equal("", MoCargoNameNormalizer.ResolveDirectaNombre("", 0));
        Assert.Equal("", MoCargoNameNormalizer.ResolveDirectaNombre(null, 1));
    }
}
