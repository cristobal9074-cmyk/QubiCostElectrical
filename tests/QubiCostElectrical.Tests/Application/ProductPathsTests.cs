using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProductPathsTests
{
    [Fact]
    public void ResolvePriceBankDatabaseFile_TrimsOverride()
    {
        var path = @"  C:\Datos\banco_precios.db  ";
        Assert.Equal(@"C:\Datos\banco_precios.db", ProductPaths.ResolvePriceBankDatabaseFile(path));
    }

    [Fact]
    public void ResolvePriceBankDatabaseFile_EmptyUsesDefault()
    {
        var resolved = ProductPaths.ResolvePriceBankDatabaseFile("");
        Assert.Equal(ProductPaths.PriceBankDatabaseFile(), resolved);
    }

    [Theory]
    [InlineData(@"\\servidor\Presupuestos\banco_precios.db", true)]
    [InlineData(@"C:\local\banco_precios.db", false)]
    [InlineData("relative.db", false)]
    public void IsUncPath_DetectsServerShare(string path, bool expected)
    {
        Assert.Equal(expected, ProductPaths.IsUncPath(path));
    }

    [Theory]
    [InlineData(@"\\servidor\share\banco.db", true)]
    [InlineData(@"C:\local\banco.db", true)]
    [InlineData("banco.db", false)]
    public void IsAbsolutePriceBankPath_RequiresRootedPath(string path, bool expected)
    {
        Assert.Equal(expected, ProductPaths.IsAbsolutePriceBankPath(path));
    }

    [Theory]
    [InlineData(@"C:\Users\ana\OneDrive\Presupuestos\banco.db", true)]
    [InlineData(@"C:\Users\ana\Dropbox\Presupuestos\banco.db", true)]
    [InlineData(@"C:\Users\ana\Google Drive\banco.db", true)]
    [InlineData(@"C:\Users\ana\iCloudDrive\banco.db", true)]
    [InlineData(@"C:\Datos\Presupuestos\banco.db", false)]
    [InlineData(@"\\servidor\OneDrive\banco.db", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLikelyCloudSyncPath_DetectsKnownSyncFolders(string? path, bool expected)
    {
        Assert.Equal(expected, ProductPaths.IsLikelyCloudSyncPath(path));
    }
}
