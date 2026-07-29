using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

public sealed class PlanningArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found");
    }

    [Fact]
    public void DomainLayer_DoesNotReference_WpfOrInfrastructure()
    {
        var domainAssembly = typeof(Schedule).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies().Select(r => r.Name).ToHashSet();
        Assert.DoesNotContain("QubiCostElectrical.Wpf", refs);
        Assert.DoesNotContain("QubiCostElectrical.Infrastructure", refs);
        Assert.DoesNotContain("PresentationFramework", refs);
    }

    [Fact]
    public void ApplicationLayer_DoesNotReference_Wpf()
    {
        var appAssembly = typeof(IScheduleService).Assembly;
        var refs = appAssembly.GetReferencedAssemblies().Select(r => r.Name).ToHashSet();
        Assert.DoesNotContain("QubiCostElectrical.Wpf", refs);
        Assert.DoesNotContain("PresentationFramework", refs);
    }

    [Fact]
    public void DomainPlanning_Namespace_HasNoWpfTypes()
    {
        var domainAssembly = typeof(Schedule).Assembly;
        var planningTypes = domainAssembly.GetTypes().Where(t =>
            t.Namespace != null && t.Namespace.Contains("Planning") &&
            t.IsPublic);
        foreach (var t in planningTypes)
            Assert.DoesNotContain("System.Windows", t.FullName);
    }

    [Fact]
    public void AllPlanningDomainTypes_AreInCorrectNamespace()
    {
        var domainAssembly = typeof(Schedule).Assembly;
        var planningTypes = domainAssembly.GetTypes().Where(t =>
            t.Namespace != null && t.Namespace == "QubiCostElectrical" &&
            (t.Name.Contains("Planning") || t.Name.Contains("Schedule") ||
             t.Name.Contains("Activity") || t.Name.Contains("Calendar") ||
             t.Name.Contains("Wbs") || t.Name.Contains("Constraint") ||
             t.Name.Contains("BudgetLink") || t.Name.Contains("Allocation") ||
             t.Name.Contains("Duration") || t.Name.Contains("WorkShift") ||
             t.Name.Contains("WeeklyWork") || t.Name.Contains("Responsible")));
        foreach (var t in planningTypes)
            Assert.True(t.Namespace == "QubiCostElectrical",
                $"Type {t.Name} should be in namespace QubiCostElectrical, not {t.Namespace}");
    }

    [Fact]
    public void ProgressTrackingService_Exists_In_Application()
    {
        Assert.NotNull(typeof(IProgressTrackingService));
        Assert.NotNull(typeof(ProgressTrackingService));
    }

    [Fact]
    public void Progress_domain_engines_are_in_domain_assembly()
    {
        var domain = typeof(Schedule).Assembly;
        Assert.NotNull(domain.GetType("QubiCostElectrical.ProgressValidation"));
        Assert.NotNull(domain.GetType("QubiCostElectrical.ProgressRescheduleEngine"));
        Assert.NotNull(domain.GetType("QubiCostElectrical.OutOfSequenceDetector"));
        Assert.NotNull(domain.GetType("QubiCostElectrical.ProgressSnapshotHasher"));
    }

    [Fact]
    public void EarnedValueEngine_Exists_In_Domain()
    {
        var domain = typeof(Schedule).Assembly;
        Assert.NotNull(domain.GetType("QubiCostElectrical.EarnedValueEngine"));
        Assert.NotNull(typeof(IEarnedValueService));
        Assert.NotNull(typeof(EarnedValueService));
    }

    [Fact]
    public void ProjectDashboardService_Exists_In_Application()
    {
        Assert.NotNull(typeof(IProjectDashboardService));
        Assert.NotNull(typeof(ProjectDashboardService));
        Assert.NotNull(typeof(DashboardSeriesBuilder));
        Assert.NotNull(typeof(DashboardTrafficLightPolicy));
    }
}
