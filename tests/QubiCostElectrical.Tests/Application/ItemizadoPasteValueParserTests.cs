using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoPasteValueParserTests
{
    [Theory]
    [InlineData("Sí", true)]
    [InlineData("SI", true)]
    [InlineData("1", true)]
    [InlineData("x", true)]
    [InlineData("No", false)]
    [InlineData("0", false)]
    public void TryParseExcluidoCubicacion_parses_spanish_and_boolean_tokens(string input, bool expected)
    {
        Assert.True(ItemizadoPasteValueParser.TryParseExcluidoCubicacion(input, out bool result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseExcluidoCubicacion_rejects_unknown()
    {
        Assert.False(ItemizadoPasteValueParser.TryParseExcluidoCubicacion("quizás", out _));
    }
}
