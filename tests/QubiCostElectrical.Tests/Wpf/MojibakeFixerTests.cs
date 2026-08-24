using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class MojibakeFixerTests
{
    [Fact]
    public void Fix_RepairsUtf8ReadAsCp1252Mojibake()
    {
        var result = MojibakeFixer.Fix("Edificio El Canelo - 23.000mÂ²");

        Assert.Equal("Edificio El Canelo - 23.000m²", result);
    }

    [Theory]
    [InlineData("Proyecto normal")]
    [InlineData("Clínica Meds")]
    [InlineData("")]
    public void Fix_LeavesCleanTextUntouched(string clean)
    {
        Assert.Equal(clean, MojibakeFixer.Fix(clean));
    }

    [Fact]
    public void Fix_NullInput_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, MojibakeFixer.Fix(null));
    }
}
