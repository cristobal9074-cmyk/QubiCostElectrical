using System.IO;
using System.Reflection;
using QubiCostElectrical.Wpf.Navigation;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Puerta de calidad Fase 1 (semanas 1–4): confianza antes de iniciar semana 5.
/// </summary>
public sealed class WpfPhase1GateTests
{
    private static string FindRepoRoot()
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

    private static string WpfSourceRoot =>
        Path.Combine(FindRepoRoot(), "src", "QubiCostElectrical.Wpf");

    [Fact]
    public void WpfSource_HasNoMessageBox()
    {
        Assert.True(Directory.Exists(WpfSourceRoot), $"No se encontró {WpfSourceRoot}");

        foreach (var file in Directory.EnumerateFiles(WpfSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            Assert.DoesNotContain("MessageBox", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeliveryLegacyView_IsRetired()
    {
        var wpfAssembly = typeof(ShellRoute).Assembly;
        Assert.Null(wpfAssembly.GetType("QubiCostElectrical.Wpf.Features.Delivery.DeliveryView"));
        Assert.Null(wpfAssembly.GetType("QubiCostElectrical.Wpf.Features.Delivery.DeliveryViewModel"));
    }

    [Fact]
    public void ShellRoute_HasOfferDocuments_NotLegacyDelivery()
    {
        var names = Enum.GetNames(typeof(ShellRoute));
        Assert.Contains(nameof(ShellRoute.OfferDocuments), names);
        Assert.DoesNotContain("Delivery", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeliverySupportTypes_StillPresentForExport()
    {
        var wpfAssembly = typeof(ShellRoute).Assembly;
        Assert.NotNull(wpfAssembly.GetType("QubiCostElectrical.Wpf.Features.Delivery.DeliveryPreviewService"));
        Assert.NotNull(wpfAssembly.GetType("QubiCostElectrical.Wpf.Features.Delivery.OfferSettingsEditorVm"));
    }

    [Fact]
    public void PlaceholderViews_AreRetiredFromSource()
    {
        var placeholderDir = Path.Combine(WpfSourceRoot, "Views", "Placeholder");
        if (Directory.Exists(placeholderDir))
        {
            var files = Directory.EnumerateFiles(placeholderDir, "*.*", SearchOption.AllDirectories).ToList();
            Assert.Empty(files);
        }
    }

    [Fact]
    public void ShellWindow_HasSingleOfferExportNavEntry()
    {
        var xaml = File.ReadAllText(Path.Combine(WpfSourceRoot, "Shell", "ShellWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(WpfSourceRoot, "Shell", "ShellWindow.xaml.cs"));
        var codeBehindMethods = File.ReadAllText(Path.Combine(WpfSourceRoot, "Shell", "ShellWindow.xaml.Methods.cs"));
        Assert.DoesNotContain("OfferDocumentsButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Oferta\"", xaml, StringComparison.Ordinal);
        Assert.True(
            codeBehind.Contains("Oferta y exportación", StringComparison.Ordinal) ||
            codeBehindMethods.Contains("Oferta y exportación", StringComparison.Ordinal),
            "Expected 'Oferta y exportación' in ShellWindow.xaml.cs or ShellWindow.xaml.Methods.cs");
    }

    [Fact]
    public void OfferDocumentsRoute_UsesCierreGroupAndExportTitle()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.OfferDocuments);
        Assert.Equal("Cierre", info.Group);
        Assert.Equal("Oferta y exportación", info.Title);
    }
}
