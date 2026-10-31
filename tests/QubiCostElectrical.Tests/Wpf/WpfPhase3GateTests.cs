using System.IO;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Puerta Fase 3 parcial (semanas 9 y 11): export golden-file + banco corporativo UNC.
/// </summary>
public sealed class WpfPhase3GateTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void GoldenFixture_DefinesElevenSheets()
    {
        Assert.Equal(11, QubiCostElectrical.Tests.Infrastructure.ExcelExportGoldenFixture.ExpectedSheetNames.Length);
        Assert.Equal("00_CONTROL", QubiCostElectrical.Tests.Infrastructure.ExcelExportGoldenFixture.ExpectedSheetNames[0]);
        Assert.Equal("11_FORMATO_COMPRA", QubiCostElectrical.Tests.Infrastructure.ExcelExportGoldenFixture.ExpectedSheetNames[^1]);
    }

    [Fact]
    public void ProductPaths_HasUncHelpers()
    {
        Assert.True(ProductPaths.IsUncPath(@"\\servidor\share\banco.db"));
        Assert.True(ProductPaths.IsAbsolutePriceBankPath(@"\\servidor\share\banco.db"));
        Assert.False(ProductPaths.IsUncPath(@"C:\local\banco.db"));
    }

    [Fact]
    public void PriceBankSharedFolder_DocumentationExists()
    {
        var doc = ReadRepoFile("docs/PRICE_BANK_SHARED_FOLDER.md");
        Assert.Contains("UNC", doc, StringComparison.Ordinal);
        Assert.Contains("banco_precios.db", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase3Report_DocumentsWeeks9And11()
    {
        var doc = ReadRepoFile("docs/PHASE3_WEEKS_9_11_REPORT.md");
        Assert.Contains("S9", doc, StringComparison.Ordinal);
        Assert.Contains("S11", doc, StringComparison.Ordinal);
        Assert.Contains("Golden", doc, StringComparison.OrdinalIgnoreCase);
    }
}
