using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectStoragePathsTests
{
    // ── IsSqliteProjectFile ───────────────────────────────────────────────────

    [Theory]
    [InlineData(@"C:\obra\presupuesto.db", true)]
    [InlineData(@"C:\obra\presupuesto.DB", true)]
    // Legacy .pepro sidecar and extension
    [InlineData(@"C:\obra\presupuesto.pepro.db", true)]
    [InlineData(@"C:\obra\presupuesto.PEPRO.DB", true)]
    [InlineData(@"C:\obra\presupuesto.pepro", true)]
    [InlineData(@"C:\obra\presupuesto.PEPRO", true)]
    // Current .qce sidecar and extension
    [InlineData(@"C:\obra\presupuesto.qce.db", true)]
    [InlineData(@"C:\obra\presupuesto.QCE.DB", true)]
    [InlineData(@"C:\obra\presupuesto.qce", true)]
    [InlineData(@"C:\obra\presupuesto.QCE", true)]
    // Non-project formats
    [InlineData(@"C:\obra\presupuesto.json", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsSqliteProjectFile_DetectsDatabaseExtensions(string path, bool expected)
    {
        Assert.Equal(expected, ProjectStoragePaths.IsSqliteProjectFile(path));
    }

    // ── IsJsonProjectFile ────────────────────────────────────────────────────

    [Theory]
    [InlineData(@"C:\obra\presupuesto.json", true)]
    [InlineData(@"C:\obra\presupuesto.JSON", true)]
    [InlineData(@"C:\obra\presupuesto.pepro", false)]
    [InlineData(@"C:\obra\presupuesto.PEPRO", false)]
    [InlineData(@"C:\obra\presupuesto.qce", false)]
    [InlineData(@"C:\obra\presupuesto.QCE", false)]
    [InlineData(@"C:\obra\presupuesto.db", false)]
    [InlineData(@"C:\obra\presupuesto.pepro.db", false)]
    [InlineData("", false)]
    public void IsJsonProjectFile_DetectsJsonExtensions(string path, bool expected)
    {
        Assert.Equal(expected, ProjectStoragePaths.IsJsonProjectFile(path));
    }

    // ── GetAutoSavePathForProject ────────────────────────────────────────────

    [Fact]
    public void GetAutoSavePathForProject_QceProject_UsesQceAutosave()
    {
        string path = ProjectStoragePaths.GetAutoSavePathForProject(@"D:\clientes\Edificio Norte.qce", "Proyecto");

        Assert.EndsWith("Edificio Norte.qce.autosave", path);
        Assert.StartsWith(ProductPaths.AutoSaveDirectory(), path);
    }

    [Fact]
    public void GetAutoSavePathForProject_PeproProject_PreservesLegacyExtension()
    {
        string path = ProjectStoragePaths.GetAutoSavePathForProject(@"D:\clientes\Edificio Norte.pepro", "Proyecto");

        Assert.EndsWith("Edificio Norte.pepro.autosave", path);
        Assert.StartsWith(ProductPaths.AutoSaveDirectory(), path);
    }

    [Fact]
    public void GetAutoSavePathForProject_UsesFallbackNameWhenPathIsEmpty()
    {
        string result = ProjectStoragePaths.GetAutoSavePathForProject("", "mi-proyecto");

        Assert.EndsWith(".autosave", result);
        Assert.Contains("mi-proyecto", result);
    }

    [Fact]
    public void GetAutoSavePathForProject_AutosaveExtensionAlwaysPresent()
    {
        string result = ProjectStoragePaths.GetAutoSavePathForProject(@"C:\a\b.db", "fallback");

        Assert.EndsWith(".autosave", result);
    }
}
