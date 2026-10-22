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

    [Theory]
    [InlineData("dev", @"C:\Users\ana\AppData\Local\QubiCostElectrical\current\QubiCostElectrical.exe", true)]
    [InlineData("DEV", null, true)]
    [InlineData("dev", null, true)]
    [InlineData("prod", @"C:\repo\publish\win-x64\QubiCostElectrical.exe", false)]
    [InlineData("", @"C:\repo\publish\win-x64\QubiCostElectrical.exe", true)]
    [InlineData(null, @"C:\repo\publish\win-x64\QubiCostElectrical.exe", true)]
    [InlineData(null, @"C:\Users\ana\AppData\Local\QubiCostElectrical\current\QubiCostElectrical.exe", false)]
    [InlineData(null, @"C:\Program Files\Qubi Cost Electrical\QubiCostElectrical.exe", false)]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    public void ResolveDevelopmentProfile_DetectsDevBuildWithoutTouchingProdData(
        string? profileValue, string? executablePath, bool expected)
    {
        Assert.Equal(expected, ProductPaths.ResolveDevelopmentProfile(profileValue, executablePath));
    }

    [Fact]
    public void ResolveDevelopmentProfile_WinsOverExecutablePath()
    {
        Assert.False(ProductPaths.ResolveDevelopmentProfile("prod", @"C:\repo\publish\win-x64\app.exe"));
        Assert.True(ProductPaths.ResolveDevelopmentProfile("dev", @"C:\Users\ana\AppData\Local\QubiCostElectrical\current\app.exe"));
    }

    [Fact]
    public void InstanceNameSuffix_TestProcessIsNotDevelopmentProfile()
    {
        // Los tests viven en bin\, no en publish\: deben correr en modo Prod para no aislar nada.
        Assert.False(ProductPaths.IsDevelopmentProfile());
        Assert.Equal(string.Empty, ProductPaths.InstanceNameSuffix());
    }
}
