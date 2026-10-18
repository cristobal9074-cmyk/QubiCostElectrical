using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CostoViewModelPolicyTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public CostoViewModelPolicyTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    private static CostoViewModel CreateVm(string projectName)
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject(projectName);
        host.Session.CurrentProjectPath = @"C:\test\dummy.qce";
        return new CostoViewModel(new FakeShellHost(), host);
    }

    [Fact]
    public void ChangeClosingPolicy_WhenCancelled_RevertsSelectionAndDoesNotChangePolicy()
    {
        var vm = CreateVm("TestPolicyCancel");

        var initialPolicy = vm.CurrentPolicy;
        var newOption = vm.ClosingPolicySection.Options.First(o => o.Value != initialPolicy);

        // Simular que el usuario cancela la confirmación
        vm.ConfirmAction = (title, msg) => false;
        vm.ClosingPolicySection.SelectedOption = newOption;

        Assert.Equal(initialPolicy, vm.CurrentPolicy);
        Assert.Equal(initialPolicy, vm.ClosingPolicySection.SelectedOption?.Value);
    }

    [Fact]
    public void ChangeClosingPolicy_WhenConfirmed_ChangesPolicyAndPushesUndo()
    {
        var vm = CreateVm("TestPolicyConfirm");
        var host = WpfAppHost.Current;

        var targetPolicy = ClosingPolicyType.TargetPrice;
        var targetOption = vm.ClosingPolicySection.Options.First(o => o.Value == targetPolicy);

        bool confirmed = false;
        vm.ConfirmAction = (title, msg) => { confirmed = true; return true; };
        vm.ClosingPolicySection.SelectedOption = targetOption;

        Assert.True(confirmed);
        Assert.Equal(targetPolicy, vm.CurrentPolicy);
        Assert.True(host.UndoService.CanUndo);
    }

    [Fact]
    public void ResetCoeficientes_WhenCancelled_KeepsCustomFactors()
    {
        var vm = CreateVm("TestResetCancel");
        var host = WpfAppHost.Current;

        var firstFactor = vm.CoefFactors.First();
        firstFactor.ValueText = "1.45";

        vm.ConfirmAction = (title, msg) => false;
        vm.ResetCoeficientesCommand.Execute(null);

        Assert.Equal(1.45m, host.Session.GetFamilyMarkup(firstFactor.FamilyKey));
    }

    [Fact]
    public void ResetCoeficientes_WhenConfirmed_ResetsFactorsAndPushesUndo()
    {
        var vm = CreateVm("TestResetConfirm");
        var host = WpfAppHost.Current;

        var firstFactor = vm.CoefFactors.First();
        firstFactor.ValueText = "1.45";

        vm.ConfirmAction = (title, msg) => true;
        vm.ResetCoeficientesCommand.Execute(null);

        Assert.Equal(1.00m, host.Session.GetFamilyMarkup(firstFactor.FamilyKey));
        Assert.True(host.UndoService.CanUndo);
    }

    [Fact]
    public void LoadCoeficientesEstandar_WhenCancelled_DoesNotApplyPreset()
    {
        var vm = CreateVm("TestPresetCancel");
        var host = WpfAppHost.Current;

        var firstFactor = vm.CoefFactors.First();
        firstFactor.ValueText = "1.99";

        vm.ConfirmAction = (title, msg) => false;
        vm.LoadCoeficientesEstandarCommand.Execute(null);

        Assert.Equal(1.99m, host.Session.GetFamilyMarkup(firstFactor.FamilyKey));
    }

    [Fact]
    public void LoadCoeficientesEstandar_WhenConfirmed_AppliesPresetAndPushesUndo()
    {
        var vm = CreateVm("TestPresetConfirm");
        var host = WpfAppHost.Current;

        var firstFactor = vm.CoefFactors.First();
        firstFactor.ValueText = "1.00";

        vm.ConfirmAction = (title, msg) => true;
        vm.LoadCoeficientesEstandarCommand.Execute(null);

        Assert.NotEqual(1.00m, host.Session.GetFamilyMarkup(firstFactor.FamilyKey));
        Assert.True(host.UndoService.CanUndo);
    }
}
