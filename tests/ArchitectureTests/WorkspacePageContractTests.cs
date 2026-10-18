using System.IO;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Pantallas core deben incluir empty + skeleton (contrato docs/UI_SCREEN_CONTRACT.md).
/// </summary>
public sealed class WorkspacePageContractTests
{
    private static readonly string[] RequiredViews =
    [
        "src/QubiCostElectrical.Wpf/Features/Costo/CostoView.xaml",
        "src/QubiCostElectrical.Wpf/Features/ManoDeObra/ManoDeObraView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Apu/ApuView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Epp/EppView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoView.xaml",
        "src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Export/ExportView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Documents/DocumentsView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Project/ProjectView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Prices/PriceBankView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Review/ReviewView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Quotes/QuotesView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Versions/VersionsView.xaml",
        "src/QubiCostElectrical.Wpf/Features/EconomicEditor/EconomicEditorView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Contracts/ContractWorkspaceView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Contracts/ContractEditorView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Contracts/ContractVersionsView.xaml",
        "src/QubiCostElectrical.Wpf/Features/Preferences/PreferencesView.xaml",
        "src/QubiCostElectrical.Wpf/Features/EstadosPago/EstadosPagoView.xaml"
    ];

    [Fact]
    public void CoreWorkspaceViews_IncludeEmptyAndSkeleton()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        foreach (var relative in RequiredViews)
        {
            var path = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Falta vista: {relative}");
            var xaml = File.ReadAllText(path);
            var usesChrome = xaml.Contains("WorkspacePageChrome", StringComparison.Ordinal);
            var hasEmpty = xaml.Contains("EmptyStateControl", StringComparison.Ordinal);
            var hasSkeleton = xaml.Contains("SkeletonOverlayControl", StringComparison.Ordinal);
            Assert.True(usesChrome || (hasEmpty && hasSkeleton),
                $"{relative} debe incluir WorkspacePageChrome o EmptyStateControl + SkeletonOverlayControl");
        }
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
