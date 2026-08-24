using System.IO;
using System.Reflection;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Puerta Fase 2 (semanas 5–8): percepción — design system, stepper, versiones A vs B.
/// </summary>
public sealed class WpfPhase2GateTests
{
    private static string WpfSourceRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "QubiCostElectrical.Wpf"));

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(WpfSourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void ShellWorkflowLogic_ProductionLine_HasSixMainStepsPlusSc()
    {
        var review = new FinalReviewSummary();
        var project = new ProjectInfoRow { Nombre = "Gate" };

        var steps = ShellWorkflowLogic.BuildSteps(
            ShellRoute.Project,
            review,
            project,
            [],
            []);

        var main = steps.Where(s => !s.IsAuxiliary).Select(s => s.Title).ToList();
        Assert.Equal(["Datos", "Itemizado", "Cubicación", "Precios", "Revisión", "Exportar"], main);
        Assert.Contains(steps, s => s.Title == "C.SC" && s.IsAuxiliary);
    }

    [Fact]
    public void ShellWindow_ContainsProductionLineStepper()
    {
        var xaml = ReadSource("Shell/ShellWindow.xaml");
        Assert.Contains("ShellWorkflowStepper", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowWorkflowStepper", xaml, StringComparison.Ordinal);
        Assert.Contains("BudgetNavTabs", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellViewModel_WorkstationMode_HidesChromeOnTakeoffRoutes()
    {
        var workflowSource = ReadSource("Shell/ShellViewModel.Workflow.cs");
        Assert.Contains("IsWorkstationMode", workflowSource, StringComparison.Ordinal);
        Assert.Contains("ShellRoute.Takeoff", workflowSource, StringComparison.Ordinal);
        Assert.Contains("!IsWorkstationMode", workflowSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Features/Currency/CurrencyView.xaml")]
    [InlineData("Features/Purchases/PurchaseListView.xaml")]
    [InlineData("Features/Quotes/QuotesView.xaml")]
    [InlineData("Features/Prices/PriceBankView.xaml")]
    public void CoreFlowViews_UseFormComboBox(string relativePath)
    {
        var xaml = ReadSource(relativePath);
        Assert.Contains("Form.ComboBox", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Features/Templates/PlantillasView.xaml")]
    [InlineData("Features/Prices/PriceBankView.xaml")]
    public void CoreFlowViews_UseFormTabControl(string relativePath)
    {
        var xaml = ReadSource(relativePath);
        Assert.Contains("Form.TabControl", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Features/Apu/ApuView.xaml")]
    [InlineData("Features/CubicacionSc/CubicacionScView.xaml")]
    [InlineData("Features/ChangeOrders/ChangeOrdersView.xaml")]
    public void CoreGrids_UseDataGridWorkspace(string relativePath)
    {
        var xaml = ReadSource(relativePath);
        Assert.Contains("DataGrid.Workspace", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionsView_SupportsAvsBComparisonUi()
    {
        var xaml = ReadSource("Features/Versions/VersionsView.xaml");
        Assert.Contains("CompareAgainstCurrent", xaml, StringComparison.Ordinal);
        Assert.Contains("ComparisonVersionOptions", xaml, StringComparison.Ordinal);
        Assert.Contains("ComparisonModeHint", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionsViewModel_ExposesCompareAgainstCurrent()
    {
        var prop = typeof(VersionsViewModel).GetProperty(nameof(VersionsViewModel.CompareAgainstCurrent));
        Assert.NotNull(prop);
        Assert.True(prop!.CanWrite);
    }
}
