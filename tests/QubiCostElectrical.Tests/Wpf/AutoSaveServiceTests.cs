using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class AutoSaveServiceTests
{
    public AutoSaveServiceTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void GetAutoSavePath_QceProject_ReturnsQceAutosave()
    {
        var path = AutoSaveService.GetAutoSavePath(@"C:\proyecto.qce");
        Assert.Equal(@"C:\proyecto.qce.autosave", path);
    }

    [Fact]
    public void GetAutoSavePath_PeproProject_ReturnsPeproAutosave()
    {
        var path = AutoSaveService.GetAutoSavePath(@"C:\proyecto.pepro");
        Assert.Equal(@"C:\proyecto.pepro.autosave", path);
    }

    [Fact]
    public void GetAutoSavePath_ReturnsFallback_WhenProjectPathEmpty()
    {
        var path = AutoSaveService.GetAutoSavePath("");
        Assert.Contains("QubiCostElectrical", path);
        Assert.EndsWith(".autosave", path);
        Assert.Contains(ProjectFileTypes.CurrentExtension, path);
    }

    [Fact]
    public void IsAutoSaveNewer_ReturnsFalse_WhenNoAutoSave()
    {
        var result = AutoSaveService.IsAutoSaveNewer(@"C:\__nonexistent__\test.qce");
        Assert.False(result);
    }

    [Fact]
    public void IsAutoSaveNewer_ReturnsFalse_WhenNoAutoSave_Legacy()
    {
        var result = AutoSaveService.IsAutoSaveNewer(@"C:\__nonexistent__\test.pepro");
        Assert.False(result);
    }

    [Fact]
    public void DeleteAutoSave_RemovesAutoSaveFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "test");
            var autoPath = AutoSaveService.GetAutoSavePath(path);
            File.WriteAllText(autoPath, "autosave");
            Assert.True(File.Exists(autoPath));
            AutoSaveService.DeleteAutoSave(path);
            Assert.False(File.Exists(autoPath));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            var autoPath = AutoSaveService.GetAutoSavePath(path);
            if (File.Exists(autoPath)) File.Delete(autoPath);
        }
    }
}
