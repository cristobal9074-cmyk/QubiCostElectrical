using System.Windows;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter converter = new();

    [Fact]
    public void Default_NullIsVisible_NonNullIsCollapsed()
    {
        Assert.Equal(Visibility.Visible, converter.Convert(null, typeof(Visibility), null, null!));
        Assert.Equal(Visibility.Collapsed, converter.Convert("algo", typeof(Visibility), null, null!));
    }

    [Fact]
    public void Inverse_NullIsCollapsed_NonNullIsVisible()
    {
        Assert.Equal(Visibility.Collapsed, converter.Convert(null, typeof(Visibility), "Inverse", null!));
        Assert.Equal(Visibility.Visible, converter.Convert("algo", typeof(Visibility), "Inverse", null!));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Hay un error", true)]
    public void NonEmpty_StringContent_ControlsVisibility(string? value, bool expectedVisible)
    {
        var result = converter.Convert(value, typeof(Visibility), "NonEmpty", null!);
        Assert.Equal(expectedVisible ? Visibility.Visible : Visibility.Collapsed, result);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, true)]
    public void NonEmpty_NumericCount_ControlsVisibility(int value, bool expectedVisible)
    {
        var result = converter.Convert(value, typeof(Visibility), "NonEmpty", null!);
        Assert.Equal(expectedVisible ? Visibility.Visible : Visibility.Collapsed, result);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Contenido", false)]
    public void Empty_IsInverseOfNonEmpty(string? value, bool expectedVisible)
    {
        var result = converter.Convert(value, typeof(Visibility), "Empty", null!);
        Assert.Equal(expectedVisible ? Visibility.Visible : Visibility.Collapsed, result);
    }
}
