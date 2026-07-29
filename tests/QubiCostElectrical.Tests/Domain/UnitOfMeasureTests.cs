using Xunit;

namespace QubiCostElectrical.Tests;

public class UnitOfMeasureTests
{
    [Fact]
    public void Catalog_ContainsAllStandardUnits()
    {
        Assert.NotNull(UnitOfMeasureCatalog.Find("UN"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("ML"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("M2"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("M3"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("KG"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("HH"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("GL"));
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        Assert.NotNull(UnitOfMeasureCatalog.Find("ml"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("mL"));
        Assert.NotNull(UnitOfMeasureCatalog.Find("Ml"));
    }

    [Fact]
    public void Find_TrimsWhitespace()
    {
        Assert.NotNull(UnitOfMeasureCatalog.Find("  KG  "));
    }

    [Fact]
    public void Find_InvalidCode_ReturnsNull()
    {
        Assert.Null(UnitOfMeasureCatalog.Find("INVALID"));
        Assert.Null(UnitOfMeasureCatalog.Find(""));
        Assert.Null(UnitOfMeasureCatalog.Find(null));
    }

    [Fact]
    public void IsValid_ReturnsTrueForKnown()
    {
        Assert.True(UnitOfMeasureCatalog.IsValid("UN"));
        Assert.True(UnitOfMeasureCatalog.IsValid("KG"));
    }

    [Fact]
    public void IsValid_ReturnsFalseForUnknown()
    {
        Assert.False(UnitOfMeasureCatalog.IsValid("FAKE"));
    }

    [Fact]
    public void GetDefault_ReturnsUnidad()
    {
        Assert.Equal("UN", UnitOfMeasureCatalog.GetDefault().Code);
    }

    [Fact]
    public void All_ContainsAllEntries()
    {
        Assert.Equal(12, UnitOfMeasureCatalog.All.Count);
    }

    [Fact]
    public void UnitOfMeasure_HasCorrectCategory()
    {
        Assert.Equal(UnitCategory.Length, UnitOfMeasureCatalog.Find("ML")!.Category);
        Assert.Equal(UnitCategory.Area, UnitOfMeasureCatalog.Find("M2")!.Category);
        Assert.Equal(UnitCategory.Volume, UnitOfMeasureCatalog.Find("M3")!.Category);
        Assert.Equal(UnitCategory.Mass, UnitOfMeasureCatalog.Find("KG")!.Category);
        Assert.Equal(UnitCategory.Time, UnitOfMeasureCatalog.Find("HH")!.Category);
        Assert.Equal(UnitCategory.Percentage, UnitOfMeasureCatalog.Find("PCT")!.Category);
        Assert.Equal(UnitCategory.Global, UnitOfMeasureCatalog.Find("GL")!.Category);
    }
}
